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
				// One player: sign in, then straight into the home village. Leaving the village area leads to the neighbouring village.
				// No landing screen, no lobby, no Profile for now.
				Multiplayer = false,
				Profile = false,
				SkipLanding = true,
				GameScene = ConquestScenes.Village,
				Accent = new Color(0.86f, 0.3f, 0.25f),
			};
		}
	}
}
