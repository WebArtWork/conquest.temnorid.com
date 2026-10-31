using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Temnorid.Village;

namespace Temnorid.Conquest.Tests
{
	/// <summary>The home village hub's data and text, against the package's fake save in memory (no network, nothing written).</summary>
	public class VillageHubTests
	{
		sealed class Account : IAccountView
		{
			public bool IsSignedIn { get; set; } = true;
			public bool IsGuest { get; set; }
		}

		static JObject LordsDoc()
		{
			var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var progress = DefaultVillage.Create(now);
			progress.Lord = new LordInfo { Name = "Ann" };
			progress.Village.Buildings["4"].Level = 4;
			progress.Stock.Amounts["wood"] = 321;
			var doc = JObject.FromObject(progress);
			doc["hero"] = JObject.Parse("{\"name\":\"Hero\"}");
			doc["updatedAt"] = "2026-10-31T10:00:00Z";
			return doc;
		}

		[Test]
		public void The_standard_village_is_there_straight_away_with_the_start_stock()
		{
			var session = VillageSource.CreateDefault(null, "Not signed in: showing the standard village.", SharedStock.SystemClock());
			Assert.IsFalse(session.IsLords);
			StringAssert.StartsWith("Standard village", VillageText.Heading(session));
			var stock = VillageText.Stock(session);
			foreach (var id in Res.All) StringAssert.Contains(Res.Name(id), stock);
			StringAssert.Contains("750 /", stock);
			Assert.AreEqual(6, stock.Split('\n').Length, "one line per resource");
		}

		[Test]
		public void A_google_player_with_a_save_sees_their_lord_and_stock_and_nothing_is_written()
		{
			var store = new InMemoryProgressStore(LordsDoc());
			var original = (JObject)store.Data.DeepClone();

			var session = new VillageSource(store, new Account()).LoadAsync().Result;

			Assert.IsTrue(session.IsLords);
			StringAssert.StartsWith("Ann's village", VillageText.Heading(session));
			StringAssert.Contains("Wood  321 /", VillageText.Stock(session));
			Assert.AreEqual(0, store.Writes.Count, "Conquest only reads Lords");
			Assert.IsTrue(JToken.DeepEquals(original, store.Data), "the save is untouched");
		}

		[Test]
		public void A_guest_or_a_missing_save_gets_the_standard_village_and_a_guest_reads_nothing()
		{
			var store = new InMemoryProgressStore(LordsDoc());
			var guest = new VillageSource(store, new Account { IsGuest = true }).LoadAsync().Result;
			Assert.IsFalse(guest.IsLords);
			Assert.AreEqual(0, store.Loads, "a guest never reads the save");

			var none = new VillageSource(new InMemoryProgressStore(null), new Account()).LoadAsync().Result;
			Assert.IsFalse(none.IsLords);
			StringAssert.Contains("Standard village", VillageText.Heading(none));
		}

		[Test]
		public void The_gate_stands_on_open_ground_south_of_the_farm()
		{
			var data = VillageSource.CreateDefault(null, "", SharedStock.SystemClock()).Data;
			var gate = VillageGame.GatePosition(data);
			Assert.Less(gate.z, -data.FarmRz);
			Assert.AreEqual(0f, gate.x);
		}
	}
}
