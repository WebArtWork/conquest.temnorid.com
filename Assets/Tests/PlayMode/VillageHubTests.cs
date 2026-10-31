using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using ITKamianets.Engine.Net;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Temnorid.Village;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Conquest.Tests
{
	/// <summary>The home village hub (scene Village): the default village at once, a Lords village from a fake save (read only), and the way to the assault and back.</summary>
	public class VillageHubTests
	{
		sealed class Account : IAccountView
		{
			public bool IsSignedIn { get; set; } = true;
			public bool IsGuest { get; set; }
		}

		string _saveFolder;

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "conquest-hub-" + Guid.NewGuid());
			ConquestGame.SaveRootOverride = _saveFolder;
			NetSession.Instance.Leave();
		}

		[TearDown]
		public void TearDown()
		{
			ConquestGame.SaveRootOverride = null;
			VillageGame.VillageLoader = null;
			Time.timeScale = 1f;
			NetSession.Instance.Leave();
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		static IEnumerator Until(Func<bool> condition, string what, float seconds = 15f)
		{
			var end = Time.realtimeSinceStartup + seconds;
			while (!condition())
			{
				if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
				yield return null;
			}
		}

		static IEnumerator LoadHub()
		{
			yield return SceneManager.LoadSceneAsync(ConquestScenes.Village);
			yield return null;
			yield return null;
		}

		static bool InScene(string name) => SceneManager.GetActiveScene().name == name;

		static JObject LordsDoc()
		{
			var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var progress = DefaultVillage.Create(now);
			progress.Lord = new LordInfo { Name = "Ann" };
			progress.Village.Buildings["-1"] = new VillageBuilding { Type = "wall", Level = 2 };
			progress.Stock.Amounts["wood"] = 321;
			var doc = JObject.FromObject(progress);
			doc["hero"] = JObject.Parse("{\"name\":\"Hero\"}");
			doc["updatedAt"] = "2026-10-31T10:00:00Z";
			return doc;
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator The_default_village_is_drawn_in_the_first_frame_with_a_hero_a_gate_and_the_stock()
		{
			yield return SceneManager.LoadSceneAsync(ConquestScenes.Village);
			var game = VillageGame.Instance;
			Assert.IsNotNull(game);
			Assert.IsTrue(game.HasVillage, "no waiting for a guest or a signed-out player");
			Assert.IsFalse(game.Loading);
			Assert.IsFalse(game.Village.IsLords);
			yield return null;
			yield return null;

			Assert.IsNotNull(game.View.BuildingBySlot(0), "the hall stands");
			Assert.Greater(game.View.Plots.Count, 0, "the farm is there");
			Assert.IsNotNull(game.Gate, "the gate to the assault");
			Assert.Less(Vector3.Distance(game.Hero.transform.position, game.View.SpawnPosition), 3f, "the hero starts by the stores");
			var hud = UnityEngine.Object.FindAnyObjectByType<VillageHud>();
			StringAssert.Contains("750 /", hud.StockText);
			Assert.AreEqual("", hud.LoadingText);
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator A_Lords_village_from_a_fake_save_shows_the_lord_and_stock_while_nothing_is_written()
		{
			var store = new InMemoryProgressStore(LordsDoc());
			var original = (JObject)store.Data.DeepClone();
			var pending = new TaskCompletionSource<VillageSession>();
			VillageGame.VillageLoader = local => pending.Task;

			yield return LoadHub();
			var game = VillageGame.Instance;
			var hud = UnityEngine.Object.FindAnyObjectByType<VillageHud>();
			Assert.IsTrue(game.Loading, "waiting for the save");
			Assert.IsFalse(game.HasVillage);
			StringAssert.Contains("Loading your village", hud.LoadingText);

			pending.SetResult(new VillageSource(store, new Account()).LoadAsync().Result);
			yield return Until(() => game.HasVillage, "the village");
			yield return null;
			yield return null;

			Assert.IsTrue(game.Village.IsLords);
			Assert.AreEqual("Ann", game.View.Data.LordName);
			Assert.AreEqual("Ann's village", game.Village.Title);
			StringAssert.Contains("Wood  321 /", hud.StockText);
			Assert.AreEqual("", hud.LoadingText);
			Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<StockSyncer>(), "Conquest never syncs the stock");
			Assert.AreEqual(0, store.Writes.Count, "nothing is written to Lords");
			Assert.IsTrue(JToken.DeepEquals(original, store.Data));
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator Walking_through_the_gate_and_the_Assault_place_both_load_the_assault()
		{
			yield return LoadHub();
			var game = VillageGame.Instance;
			game.Hero.Teleport(game.Gate.transform.position + Vector3.up * 0.1f, 0f);
			yield return Until(() => InScene(ConquestScenes.Assault), "the assault scene (gate)");

			yield return null;
			yield return LoadHub();
			var hud = UnityEngine.Object.FindAnyObjectByType<VillageHud>();
			hud.Chrome.Select(VillageHud.AssaultPlace);
			yield return Until(() => InScene(ConquestScenes.Assault), "the assault scene (place)");
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator Return_to_the_village_from_the_assault_goes_back_to_the_hub_alone()
		{
			yield return SceneManager.LoadSceneAsync(ConquestScenes.Assault);
			yield return null;
			yield return null;
			ConquestGame.Instance.SaveEnabled = false;
			var hud = UnityEngine.Object.FindAnyObjectByType<ConquestHud>();
			Assert.IsNotNull(hud.Chrome);

			hud.Chrome.Select(ConquestShell.VillagePlace);
			yield return Until(() => InScene(ConquestScenes.Village), "the village (place)");
			yield return null;
			Assert.IsTrue(VillageGame.Instance.HasVillage);

			yield return SceneManager.LoadSceneAsync(ConquestScenes.Assault);
			yield return null;
			ConquestGame.Instance.SaveEnabled = false;
			ConquestGame.Instance.SaveAndVillage();
			yield return Until(() => InScene(ConquestScenes.Village), "the village (button)");
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator Online_the_hub_is_skipped_and_the_assault_starts_straight_away()
		{
			var host = NetSession.Instance;
			host.GameId = "conquest";
			host.GameVersion = Application.version;
			host.LocalName = "Hosty";
			Assert.IsTrue(host.HostDirect(7804), host.LastError);

			yield return SceneManager.LoadSceneAsync(ConquestScenes.Village);
			yield return Until(() => InScene(ConquestScenes.Assault), "the assault scene");
			yield return null;
			yield return null;
			Assert.IsTrue(ConquestGame.Instance.Online);
			ConquestGame.Instance.SaveEnabled = false;
			Assert.IsNull(VillageGame.Instance);
		}
	}
}
