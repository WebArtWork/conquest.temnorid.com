using System;
using ITKamianets.Engine.Save;

namespace Temnorid.Conquest
{
	/// <summary>How the village was taken.</summary>
	public enum VictoryKind
	{
		/// <summary>The enemy Lord was defeated.</summary>
		DefeatedLord,
		/// <summary>The player got into the hall.</summary>
		TookHall,
	}

	/// <summary>What is kept between runs.</summary>
	[Serializable]
	public sealed class ConquestSave
	{
		public int completions;
		public int lordVictories;
		public int hallVictories;
		public float bestSeconds;
	}

	/// <summary>
	/// The record of the player's assaults with no Unity in them: how many villages were taken, in which way,
	/// and the best time. Both ways of winning count the same; the way is only remembered.
	/// </summary>
	public sealed class ConquestSession
	{
		public int Completions { get; private set; }
		public int LordVictories { get; private set; }
		public int HallVictories { get; private set; }

		/// <summary>The best time in seconds, 0 when there is none yet.</summary>
		public float BestSeconds { get; private set; }

		/// <summary>The village was taken in <paramref name="seconds"/>. Returns true when that is a new best time.</summary>
		public bool Complete(float seconds, VictoryKind kind)
		{
			Completions++;
			if (kind == VictoryKind.DefeatedLord) LordVictories++;
			else HallVictories++;

			var best = BestSeconds <= 0f || seconds < BestSeconds;
			if (best) BestSeconds = seconds;
			return best;
		}

		public ConquestSave ToSave() => new ConquestSave
		{
			completions = Completions,
			lordVictories = LordVictories,
			hallVictories = HallVictories,
			bestSeconds = BestSeconds,
		};

		public void Load(ConquestSave save)
		{
			if (save == null) return;
			Completions = Math.Max(0, save.completions);
			LordVictories = Math.Max(0, save.lordVictories);
			HallVictories = Math.Max(0, save.hallVictories);
			BestSeconds = Math.Max(0f, save.bestSeconds);
		}
	}

	/// <summary>Reads and writes the <see cref="ConquestSave"/> in the game's save slot.</summary>
	public sealed class ConquestSaveStore
	{
		const string Slot = "default";
		const int PayloadVersion = 1;

		readonly SaveStore _store;

		public ConquestSaveStore(string rootDirectory = null)
		{
			_store = new SaveStore("conquest", rootDirectory);
		}

		/// <summary>The saved record, or null when there is none yet or it cannot be read.</summary>
		public ConquestSave Load()
		{
			var result = _store.Load<ConquestSave>(Slot, PayloadVersion);
			return result.Ok ? result.Value : null;
		}

		public void Save(ConquestSave save) => _store.Save(Slot, save, PayloadVersion);
	}
}
