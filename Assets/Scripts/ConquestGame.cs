using ITKamianets.Engine.AI;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Input;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Temnorid.Conquest
{
	public enum ConquestPhase
	{
		Playing,
		Victory,
		Defeat,
	}

	/// <summary>
	/// Runs one assault: bakes the NavMesh, wakes the defenders, and ends the run in victory (the Lord is dead,
	/// or the player is in the hall) or defeat (the player is dead).
	/// </summary>
	[DefaultExecutionOrder(-100)]
	public sealed class ConquestGame : MonoBehaviour
	{
		/// <summary>Where saves go instead of the persistent data path. Tests set it before loading the scene.</summary>
		public static string SaveRootOverride;

		public static ConquestGame Instance { get; private set; }

		[Header("Scene")]
		public Health PlayerHealth;
		public NavMeshSurface Surface;
		public Soldier[] StartingSoldiers;
		public Soldier EnemyLord;

		public bool SaveEnabled = true;

		const string Log = "Conquest";

		ConquestSaveStore _store;

		public ConquestSession Session { get; private set; }
		public ConquestPhase Phase { get; private set; } = ConquestPhase.Playing;
		public VictoryKind Won { get; private set; }
		public bool Paused { get; private set; }
		public float RunTime { get; private set; }
		public bool NewBestTime { get; private set; }

		public int GuardsLeft
		{
			get
			{
				var count = 0;
				foreach (var soldier in Soldier.All) if (!soldier.IsLord) count++;
				return count;
			}
		}

		void Awake()
		{
			Instance = this;
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;

			Session = new ConquestSession();
			_store = new ConquestSaveStore(SaveRootOverride);
			Session.Load(_store.Load());

			// The defenders wait, switched off, until the ground exists to walk on.
			Surface.BuildNavMesh();
			foreach (var soldier in StartingSoldiers) soldier.gameObject.SetActive(true);

			PlayerHealth.Died += OnPlayerDied;
		}

		void OnEnable() => Soldier.Killed += OnSoldierKilled;

		void OnDisable() => Soldier.Killed -= OnSoldierKilled;

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		void Update()
		{
			if (Phase != ConquestPhase.Playing) return;
			if (!Paused) RunTime += Time.deltaTime;
			if (EngineInput.Default.UI.Pause.WasPressedThisFrame()) SetPaused(!Paused);
		}

		void OnApplicationQuit() => Save();

		public void SetPaused(bool paused)
		{
			if (Phase != ConquestPhase.Playing) return;
			Paused = paused;
			Time.timeScale = paused ? 0f : 1f;
			EngineInput.Default.PlayerEnabled = !paused;
		}

		void OnSoldierKilled(Soldier soldier)
		{
			if (soldier.IsLord) Win(VictoryKind.DefeatedLord);
		}

		/// <summary>The village is taken: either way of winning ends here.</summary>
		public void Win(VictoryKind kind)
		{
			if (Phase != ConquestPhase.Playing) return;
			Phase = ConquestPhase.Victory;
			Won = kind;
			NewBestTime = Session.Complete(RunTime, kind);
			Save();

			// The fighting stops: nobody should kill a player who has already won.
			foreach (var soldier in Soldier.All) soldier.Agent.Stop();
			EngineInput.Default.PlayerEnabled = false; // unlocks the cursor for the buttons
			EngineLog.Info(Log, $"Village taken ({kind}) in {RunTime:0.0}s.");
		}

		void OnPlayerDied(Health health, DamageInfo info)
		{
			if (Phase != ConquestPhase.Playing) return;
			Phase = ConquestPhase.Defeat;
			var marker = health.GetComponent<AiTarget>();
			if (marker != null) marker.Alive = false;
			EngineInput.Default.PlayerEnabled = false;
		}

		/// <summary>Starts the assault again from outside the village.</summary>
		public void Retry()
		{
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}

		public void Save()
		{
			if (SaveEnabled && Session != null) _store.Save(Session.ToSave());
		}

		public void SaveAndQuit()
		{
			Save();
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}
	}
}
