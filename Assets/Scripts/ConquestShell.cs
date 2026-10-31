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
				GameScene = "Main",
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
