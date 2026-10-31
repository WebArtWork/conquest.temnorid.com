using System;
using System.Collections;
using System.Globalization;
using System.IO;
using ITKamianets.Engine.AI;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Conquest.Tests
{
	/// <summary>
	/// The real village with this machine as the host and a second player (a detached session in the same process) as the guest:
	/// the guest hero is shown on the host from its pose, the host defenders reach the guest as entities, a blow the guest lands is
	/// checked and applied by the host, damage to the guest hero is sent to the guest, and the host decides how it ends.
	/// </summary>
	public class OnlineConquestTests
	{
		const int Port = 7803;

		NetSession _guest;
		string _saveFolder;

		static IEnumerator Until(Func<bool> condition, string what, float seconds = 15f)
		{
			var end = Time.realtimeSinceStartup + seconds;
			while (!condition())
			{
				if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
				yield return null;
			}
		}

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "conquest-online-" + Guid.NewGuid());
			ConquestGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			ConquestGame.SaveRootOverride = null;
			Time.timeScale = 1f;
			if (_guest != null) { _guest.Leave(); UnityEngine.Object.Destroy(_guest.gameObject); }
			NetSession.Instance.Leave();
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		IEnumerator HostWithGuest()
		{
			var host = NetSession.Instance;
			host.Leave();
			host.GameId = "conquest";
			host.GameVersion = Application.version;
			host.LocalName = "Hosty";
			host.MaxPlayers = 4;
			Assert.IsTrue(host.HostDirect(Port), host.LastError);

			_guest = NetSession.CreateDetached();
			_guest.GameId = "conquest";
			_guest.GameVersion = Application.version;
			_guest.LocalName = "Guesty";
			_guest.LocalColour = 3;
			Assert.IsTrue(_guest.JoinDirect("127.0.0.1:" + Port), _guest.LastError);
			yield return Until(() => host.Players.Count == 2 && _guest.Players.Count == 2 && !_guest.Connecting, "the guest to join");

			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
		}

		static NetPose Pose(Vector3 at, bool alive = true) => new NetPose { Position = at, Yaw = 20f, Speed = 4f, State = alive ? RemoteHero.AliveBit : 0, A = 150f, B = 200f };

		IEnumerator GuestStands(Vector3 at, float seconds, bool alive = true)
		{
			var end = Time.realtimeSinceStartup + seconds;
			while (Time.realtimeSinceStartup < end)
			{
				_guest.PublishPose(Pose(at, alive));
				yield return new WaitForSeconds(0.1f);
			}
		}

		[UnityTest]
		[Timeout(180000)]
		public IEnumerator A_guest_storms_the_village_with_the_host_and_the_host_decides()
		{
			yield return HostWithGuest();
			var game = ConquestGame.Instance;
			game.SaveEnabled = false;
			var net = game.Net;

			Assert.IsNotNull(net, "an online assault has the link to other players");
			Assert.IsTrue(game.Online);
			Assert.IsFalse(game.IsClient);
			foreach (var soldier in game.StartingSoldiers)
			{
				Assert.IsFalse(soldier.Remote, "the host simulates the defenders");
				Assert.IsTrue(soldier.Agent.Nav.isOnNavMesh, soldier.name + " stands on the NavMesh");
			}

			// Entities: the host defenders and the objective reach the guest.
			yield return Until(() => _guest.Entities.Count >= 8, "the defenders and the objective to reach the guest");
			Assert.IsTrue(_guest.Entities.ContainsKey(ConquestNet.StateId));
			Assert.AreEqual(ConquestNet.KindLord, _guest.Entities[ConquestNet.SoldierBase + 6].Kind, "the last defender is the Lord");
			Assert.AreEqual(250f, _guest.Entities[ConquestNet.SoldierBase + 6].Value, 0.01f);
			Assert.AreEqual(50f, _guest.Entities[ConquestNet.SoldierBase].Value, 0.01f);
			Assert.AreEqual(6f, _guest.Entities[ConquestNet.StateId].Yaw, 0.01f, "six guards stand");

			// Poses: the guest says where their hero is, and the host shows it as a body the guards can see.
			var guard = game.StartingSoldiers[0];
			var spot = guard.transform.position + new Vector3(0f, 0f, -2.5f);
			var damageToGuest = 0f;
			_guest.On(ConquestNet.DamageEvent, (who, text) => damageToGuest += float.Parse(text, CultureInfo.InvariantCulture));
			yield return GuestStands(spot, 0.6f);
			Assert.IsTrue(net.Heroes.TryGetValue(_guest.LocalId, out var hero), "the host shows the hero of the guest");
			Assert.AreEqual("Guesty", hero.PlayerName);
			Assert.Less((hero.transform.position - spot).magnitude, 0.5f, "at the place the guest said");
			Assert.IsTrue(hero.Alive);
			Assert.AreEqual(150f, hero.HealthNow, 0.01f);
			Assert.IsNotNull(hero.GetComponent<AiTarget>(), "the guards can hunt it");

			// A hit the guest reports is checked and applied by the host.
			var before = guard.Health.Current;
			_guest.Send(ConquestNet.HitEvent, new ConquestHit(0, 20f, ConquestHit.Sword).Write(), true);
			yield return Until(() => guard.Health.Current < before, "the host to apply the blow of the guest");
			Assert.AreEqual(before - 20f, guard.Health.Current, 0.01f);
			yield return Until(() => _guest.Entities.TryGetValue(ConquestNet.SoldierBase, out var e) && e.Value < before - 19f, "the new health to be published");

			// Refused: too much damage for the sword, a defender far away, a made-up weapon.
			var refusedBefore = net.HitsRefused;
			var health = guard.Health.Current;
			_guest.Send(ConquestNet.HitEvent, new ConquestHit(0, 999f, ConquestHit.Sword).Write(), true);
			_guest.Send(ConquestNet.HitEvent, new ConquestHit(6, 20f, ConquestHit.Sword).Write(), true);
			_guest.Send(ConquestNet.HitEvent, "0|20|laser", true);
			yield return Until(() => net.HitsRefused >= refusedBefore + 3, "the host to refuse the bad hits");
			Assert.AreEqual(health, guard.Health.Current, 0.01f);
			Assert.AreEqual(250f, game.EnemyLord.Health.Current, 0.01f, "the far Lord was not hurt");

			// A guard that fights the hero of the guest here hurts it there: the damage is sent to the guest.
			var until = Time.realtimeSinceStartup + 25f;
			while (damageToGuest <= 0f && Time.realtimeSinceStartup < until)
			{
				_guest.PublishPose(Pose(spot));
				yield return new WaitForSeconds(0.1f);
			}
			Assert.Greater(damageToGuest, 0f, "the blow of a guard was sent to the guest");

			// A guard that dies is removed on the side of the guest too.
			var gone = false;
			_guest.EntityGone += id => { if (id == ConquestNet.SoldierBase) gone = true; };
			guard.Health.ApplyDamage(new DamageInfo(9999f));
			yield return Until(() => gone, "the guest to be told the guard died");
			Assert.AreEqual(5, game.GuardsLeft);

			// The host decides the end and announces it.
			string ended = null;
			_guest.On(ConquestNet.EndEvent, (who, text) => ended = text);
			game.EnemyLord.Health.ApplyDamage(new DamageInfo(9999f));
			Assert.AreEqual(ConquestPhase.Victory, game.Phase);
			yield return Until(() => ended != null, "the guest to be told of the victory");
			Assert.IsTrue(ended.StartsWith("1|0|"), ended);
		}

		[UnityTest]
		[Timeout(180000)]
		public IEnumerator A_fallen_hero_comes_back_and_only_a_team_wipe_loses()
		{
			yield return HostWithGuest();
			var game = ConquestGame.Instance;
			game.SaveEnabled = false;
			game.RespawnSeconds = 1.5f;
			var safe = new Vector3(0f, 0.1f, -20f);

			// The guest stands far from the guards, alive.
			yield return GuestStands(safe, 0.6f);

			// The host hero falls: the assault goes on (the guest stands) and the hero returns.
			game.PlayerHealth.ApplyDamage(new DamageInfo(9999f));
			Assert.IsTrue(game.Fallen);
			Assert.AreEqual(ConquestPhase.Playing, game.Phase, "one hero down is not the end");
			var until = Time.realtimeSinceStartup + 10f;
			while (game.Fallen && Time.realtimeSinceStartup < until)
			{
				_guest.PublishPose(Pose(safe));
				yield return new WaitForSeconds(0.1f);
			}
			Assert.IsFalse(game.Fallen, "the fallen hero came back");
			Assert.AreEqual(game.PlayerHealth.MaxHealth, game.PlayerHealth.Current, 0.01f);
			Assert.AreEqual(ConquestPhase.Playing, game.Phase);

			// Now both are down at once: the host announces the defeat.
			string ended = null;
			_guest.On(ConquestNet.EndEvent, (who, text) => ended = text);
			game.RespawnSeconds = 30f;
			yield return GuestStands(safe, 0.5f, false);
			game.PlayerHealth.ApplyDamage(new DamageInfo(9999f));
			yield return Until(() => game.Phase == ConquestPhase.Defeat, "the host to call the defeat");
			yield return Until(() => ended != null, "the guest to be told of the defeat");
			Assert.IsTrue(ended.StartsWith("2|"), ended);
		}
	}
}
