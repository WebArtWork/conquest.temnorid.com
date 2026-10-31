using ITKamianets.Engine.Shell;
using UnityEngine;

namespace Temnorid.Conquest
{
	/// <summary>What the shared shell (splash, landing, lobby, top and bottom bars) needs to know about this game.</summary>
	public static class ConquestShell
	{
		public const string Repo = "WebArtWork/conquest.temnorid.com";
		public const string AssaultPlace = "assault";
		public const string ObjectivesPlace = "objectives";
		/// <summary>The place that goes home to the village hub (alone only); the hub's own places are Village and Assault.</summary>
		public const string VillagePlace = "village";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void Configure()
		{
			ShellConfig.Current = new ShellConfig
			{
				Title = "Temnorid Conquest",
				Tagline = "Storm the gate. Take the village.",
				GameId = "conquest",
				Repo = Repo,
				InstallerSuffix = "-Setup.exe",
				MaxPlayers = 4,
				Multiplayer = true,
				// Playing alone starts in the player's home village; the assault scene (Main) follows from there. The online lobby
				// also starts GameScene, so VillageGame sends anybody who arrives there online straight on to Main.
				GameScene = ConquestScenes.Village,
				Accent = new Color(0.86f, 0.3f, 0.25f),
				Places =
				{
					new ShellPlace(AssaultPlace, "Assault", "A"),
					new ShellPlace(ObjectivesPlace, "Objectives", "O"),
				},
			};
		}
	}
}
