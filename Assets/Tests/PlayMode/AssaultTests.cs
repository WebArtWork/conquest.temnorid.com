using System.Collections;
using System.IO;
using ITKamianets.Engine.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Conquest.Tests
{
	public class AssaultTests
	{
		string _saveFolder;

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "conquest-playmode-" + System.Guid.NewGuid());
			ConquestGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			ConquestGame.SaveRootOverride = null;
			Time.timeScale = 1f;
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		static IEnumerator Load()
		{
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
		}

		static void Teleport(Transform who, Vector3 position)
		{
			var controller = who.GetComponent<CharacterController>();
			controller.enabled = false;
			who.position = position;
			controller.enabled = true;
		}

		[UnityTest]
		public IEnumerator Every_defender_stands_on_the_baked_ground_and_the_assault_starts_clean()
		{
			yield return Load();
			var game = ConquestGame.Instance;
			game.SaveEnabled = false;

			Assert.AreEqual(7, game.StartingSoldiers.Length, "six guards and the enemy Lord");
			foreach (var soldier in game.StartingSoldiers)
			{
				Assert.IsTrue(soldier.gameObject.activeSelf, soldier.name + " is awake");
				Assert.IsTrue(soldier.Agent.Nav.isOnNavMesh, soldier.name + " stands on the NavMesh");
			}
			Assert.AreEqual(ConquestPhase.Playing, game.Phase);
			Assert.AreEqual(6, game.GuardsLeft);
			Assert.IsNotNull(game.EnemyLord);
			Assert.IsTrue(game.EnemyLord.IsLord);
		}

		[UnityTest]
		[Timeout(120000)]
		public IEnumerator Guards_notice_the_player_chase_and_hurt_them()
		{
			yield return Load();
			var game = ConquestGame.Instance;
			game.SaveEnabled = false;

			// Just inside the gate, in sight of the first guards.
			Teleport(game.PlayerHealth.transform, new Vector3(0f, 0.1f, 3f));
			Time.timeScale = 4f;

			var deadline = Time.realtimeSinceStartup + 40f;
			while (game.PlayerHealth.Current >= game.PlayerHealth.MaxHealth && Time.realtimeSinceStartup < deadline) yield return null;
			Time.timeScale = 1f;

			Assert.Less(game.PlayerHealth.Current, game.PlayerHealth.MaxHealth, "a guard reached the player and hit them");
		}

		[UnityTest]
		public IEnumerator Defeating_the_enemy_Lord_takes_the_village()
		{
			yield return Load();
			var game = ConquestGame.Instance;
			game.SaveEnabled = false;

			game.EnemyLord.Health.ApplyDamage(new DamageInfo(9999f));

			Assert.AreEqual(ConquestPhase.Victory, game.Phase);
			Assert.AreEqual(VictoryKind.DefeatedLord, game.Won);
			Assert.AreEqual(1, game.Session.Completions);
			Assert.AreEqual(1, game.Session.LordVictories);
			foreach (var soldier in game.StartingSoldiers)
				if (soldier != null && soldier.Health.IsAlive)
					Assert.AreEqual(ITKamianets.Engine.AI.AiState.Dead, soldier.Agent.State, soldier.name + " stops fighting once the village is taken");
		}

		[UnityTest]
		public IEnumerator Getting_into_the_hall_takes_the_village_too()
		{
			yield return Load();
			var game = ConquestGame.Instance;
			game.SaveEnabled = false;

			// Near the hall wall, outside: nothing happens.
			Teleport(game.PlayerHealth.transform, new Vector3(-12f, 0.1f, 38f));
			game.PlayerHealth.Heal(0f);
			yield return new WaitForSeconds(0.3f);
			Assert.AreEqual(ConquestPhase.Playing, game.Phase, "standing beside the hall does not count");

			// Through the doorway and into the hall.
			Teleport(game.PlayerHealth.transform, new Vector3(0f, 0.1f, 38.6f));
			for (var i = 0; i < 20 && game.Phase == ConquestPhase.Playing; i++) yield return new WaitForSeconds(0.1f);

			Assert.AreEqual(ConquestPhase.Victory, game.Phase);
			Assert.AreEqual(VictoryKind.TookHall, game.Won);
			Assert.AreEqual(1, game.Session.HallVictories);
		}

		[UnityTest]
		public IEnumerator Dying_ends_the_assault_in_defeat()
		{
			yield return Load();
			var game = ConquestGame.Instance;
			game.SaveEnabled = false;

			game.PlayerHealth.ApplyDamage(new DamageInfo(9999f));
			Assert.AreEqual(ConquestPhase.Defeat, game.Phase);
		}
	}
}
