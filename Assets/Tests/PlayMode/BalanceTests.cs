using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Conquest.Tests
{
	/// <summary>
	/// Measures how hard the assault is: bots play whole runs at high speed and the outcome of each is written to
	/// <c>Builds/balance.txt</c>. Slow, so it only runs when asked for by name:
	/// <c>-testFilter Temnorid.Conquest.Tests.BalanceTests.Bots_play_whole_runs</c>.
	/// </summary>
	public class BalanceTests
	{
		const int RunsPerMode = 8;

		[UnityTest]
		[Explicit("Balance measurement: run by name")]
		[Timeout(1800000)]
		public IEnumerator Bots_play_whole_runs()
		{
			ConquestGame.SaveRootOverride = Path.Combine(Path.GetTempPath(), "conquest-balance-" + System.Guid.NewGuid());
			var report = new StringBuilder();

			var modes = new[]
			{
				("fighter, no dodge", BotPlan.Fighter, false),
				("fighter, dodges ", BotPlan.Fighter, true),
				("runner to hall  ", BotPlan.Runner, true),
			};

			foreach (var (name, plan, dodges) in modes)
			{
				var wins = 0;
				var totalTime = 0f;
				var totalHealth = 0f;
				for (var run = 0; run < RunsPerMode; run++)
				{
					yield return SceneManager.LoadSceneAsync("Main");
					yield return null;
					var game = ConquestGame.Instance;
					game.SaveEnabled = false;
					var bot = game.PlayerHealth.gameObject.AddComponent<AutoPlayer>();
					bot.Plan = plan;
					bot.Dodges = dodges;
					Time.timeScale = 5f;

					var deadline = Time.realtimeSinceStartup + 90f;
					while (game.Phase == ConquestPhase.Playing && game.RunTime < 240f && Time.realtimeSinceStartup < deadline) yield return null;
					Time.timeScale = 1f;

					var won = game.Phase == ConquestPhase.Victory;
					if (won)
					{
						wins++;
						totalTime += game.RunTime;
						totalHealth += game.PlayerHealth.Current;
					}
					report.AppendLine($"{name} run {run}: {game.Phase,-8} {(won ? game.Won.ToString() : ""),-13} time {game.RunTime,6:0.0}s  hp {game.PlayerHealth.Current,5:0}  guards left {game.GuardsLeft}  lord hp {(game.EnemyLord.Health.IsAlive ? game.EnemyLord.Health.Current : 0):0}");
				}
				report.AppendLine($"== {name}: {wins}/{RunsPerMode} won" + (wins > 0 ? $", average {totalTime / wins:0}s, {totalHealth / wins:0} hp left" : ""));
			}

			Directory.CreateDirectory("Builds");
			File.WriteAllText("Builds/balance.txt", report.ToString());
			ConquestGame.SaveRootOverride = null;
			Assert.Pass(report.ToString());
		}
	}
}
