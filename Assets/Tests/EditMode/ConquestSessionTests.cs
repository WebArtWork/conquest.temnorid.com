using System.IO;
using NUnit.Framework;

namespace Temnorid.Conquest.Tests
{
	public class ConquestSessionTests
	{
		[Test]
		public void Both_ways_of_winning_count_as_a_taken_village_and_are_remembered()
		{
			var session = new ConquestSession();
			session.Complete(100f, VictoryKind.DefeatedLord);
			session.Complete(80f, VictoryKind.TookHall);
			session.Complete(120f, VictoryKind.TookHall);

			Assert.AreEqual(3, session.Completions);
			Assert.AreEqual(1, session.LordVictories);
			Assert.AreEqual(2, session.HallVictories);
		}

		[Test]
		public void The_best_time_is_the_fastest_run_whichever_way_it_was_won()
		{
			var session = new ConquestSession();
			Assert.IsTrue(session.Complete(100f, VictoryKind.DefeatedLord), "the first run is a best");
			Assert.IsFalse(session.Complete(130f, VictoryKind.TookHall));
			Assert.IsTrue(session.Complete(61.5f, VictoryKind.TookHall));
			Assert.AreEqual(61.5f, session.BestSeconds, 0.001f);
		}

		[Test]
		public void Loading_a_damaged_save_cannot_give_negative_records()
		{
			var session = new ConquestSession();
			session.Load(new ConquestSave { completions = -5, lordVictories = -1, hallVictories = -2, bestSeconds = -9f });
			Assert.AreEqual(0, session.Completions);
			Assert.AreEqual(0f, session.BestSeconds);
			Assert.IsTrue(session.Complete(50f, VictoryKind.TookHall));
		}

		[Test]
		public void Records_survive_a_restart_and_a_damaged_file_starts_fresh()
		{
			var folder = Path.Combine(Path.GetTempPath(), "conquest-test-" + System.Guid.NewGuid());
			try
			{
				var store = new ConquestSaveStore(folder);
				Assert.IsNull(store.Load(), "nothing saved yet");

				var session = new ConquestSession();
				session.Complete(75f, VictoryKind.DefeatedLord);
				session.Complete(66f, VictoryKind.TookHall);
				store.Save(session.ToSave());

				var restored = new ConquestSession();
				restored.Load(new ConquestSaveStore(folder).Load());
				Assert.AreEqual(2, restored.Completions);
				Assert.AreEqual(1, restored.LordVictories);
				Assert.AreEqual(1, restored.HallVictories);
				Assert.AreEqual(66f, restored.BestSeconds, 0.001f);

				File.WriteAllText(Path.Combine(folder, "conquest", "default.json"), "nope");
				Assert.IsNull(new ConquestSaveStore(folder).Load(), "damaged file: start fresh");
				Assert.AreEqual(1, Directory.GetFiles(Path.Combine(folder, "conquest"), "default.corrupt-*.json").Length, "the damaged file is kept");
			}
			finally
			{
				if (Directory.Exists(folder)) Directory.Delete(folder, true);
			}
		}
	}
}
