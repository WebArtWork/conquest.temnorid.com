using ITKamianets.Engine.AI;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
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
	/// <para>
	/// Played with others, the host runs all of this (<see cref="ConquestNet"/> publishes it) and everybody else shows it.
	/// A fallen hero comes back after <see cref="RespawnSeconds"/>; the assault is lost only when every hero is down at once.
	/// </para>
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

		[Header("Playing with others")]
		public float RespawnSeconds = 6f;

		const string Log = "Conquest";

		ConquestSaveStore _store;
		int _mirroredGuards = -1;
		float _respawnAt;

		public ConquestSession Session { get; private set; }
		public ConquestPhase Phase { get; private set; } = ConquestPhase.Playing;
		public VictoryKind Won { get; private set; }
		public bool Paused { get; private set; }
		public float RunTime { get; private set; }
		public bool NewBestTime { get; private set; }

		/// <summary>The link to the other players, or null when playing alone.</summary>
		public ConquestNet Net { get; private set; }

		/// <summary>True when this game is shared with others (as host or guest).</summary>
		public bool Online { get; private set; }

		/// <summary>True on a guest: the defenders and the outcome are the host's, this machine only shows them.</summary>
		public bool IsClient => Net != null && Net.IsClient;

		/// <summary>The player's hero is down and will come back (online only; alone a fallen hero ends the assault).</summary>
		public bool Fallen { get; private set; }

		public float RespawnIn => Fallen ? Mathf.Max(0f, _respawnAt - Time.time) : 0f;

		/// <summary>Where this player's hero starts and comes back to.</summary>
		public Vector3 SpawnPoint { get; set; }

		public int GuardsLeft
		{
			get
			{
				if (IsClient && _mirroredGuards >= 0) return _mirroredGuards;
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
			SpawnPoint = PlayerHealth.transform.position;

			var session = NetSession.Instance;
			Online = session.IsOnline;
			var client = Online && session.IsClient;

			// The defenders wait, switched off, until the ground exists to walk on. A guest does not simulate them, so it needs no NavMesh.
			if (!client) Surface.BuildNavMesh();
			for (var i = 0; i < StartingSoldiers.Length; i++)
			{
				var soldier = StartingSoldiers[i];
				if (Online) soldier.NetId = ConquestNet.SoldierBase + i;
				if (client) soldier.MakeRemote(ConquestNet.SoldierBase + i);
				soldier.gameObject.SetActive(true);
			}

			PlayerHealth.Died += OnPlayerDied;

			if (Online)
			{
				Net = gameObject.AddComponent<ConquestNet>();
				Net.Init(this);
			}
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
			if (!Paused || Online) RunTime += Time.deltaTime; // the others play on while this player is in a menu
			if (EngineInput.Default.UI.Pause.WasPressedThisFrame()) SetPaused(!Paused);
			if (Fallen && Time.time >= _respawnAt) Respawn();
		}

		void OnApplicationQuit() => Save();

		public void SetPaused(bool paused)
		{
			if (Phase != ConquestPhase.Playing) return;
			Paused = paused;
			if (!Online) Time.timeScale = paused ? 0f : 1f; // with others the world does not stop
			EngineInput.Default.PlayerEnabled = !paused && !Fallen;
		}

		void OnSoldierKilled(Soldier soldier)
		{
			if (soldier.IsLord && !IsClient) Win(VictoryKind.DefeatedLord);
		}

		/// <summary>The village is taken: either way of winning ends here. Only the host (or a player alone) decides.</summary>
		public void Win(VictoryKind kind)
		{
			if (Phase != ConquestPhase.Playing || IsClient) return;
			Phase = ConquestPhase.Victory;
			Won = kind;
			NewBestTime = Session.Complete(RunTime, kind);
			Save();

			// The fighting stops: nobody should kill a player who has already won.
			foreach (var soldier in Soldier.All) soldier.Agent.Stop();
			EngineInput.Default.PlayerEnabled = false; // unlocks the cursor for the buttons
			EngineLog.Info(Log, $"Village taken ({kind}) in {RunTime:0.0}s.");
			Net?.AnnounceEnd();
		}

		/// <summary>Host: every hero is down at once, the assault is lost.</summary>
		public void Defeat()
		{
			if (Phase != ConquestPhase.Playing || IsClient) return;
			Phase = ConquestPhase.Defeat;
			EngineInput.Default.PlayerEnabled = false;
			Net?.AnnounceEnd();
		}

		/// <summary>Guest: the host says how the assault ended.</summary>
		public void MirrorEnd(ConquestPhase phase, VictoryKind kind, float runTime)
		{
			if (Phase != ConquestPhase.Playing || phase == ConquestPhase.Playing) return;
			Phase = phase;
			Won = kind;
			RunTime = runTime;
			Fallen = false;
			if (phase == ConquestPhase.Victory)
			{
				NewBestTime = Session.Complete(RunTime, kind);
				Save();
			}
			EngineInput.Default.PlayerEnabled = false;
		}

		/// <summary>Guest: the host's clock and the number of guards still standing.</summary>
		public void MirrorProgress(float runTime, int guardsLeft)
		{
			if (Phase != ConquestPhase.Playing) return;
			RunTime = runTime;
			_mirroredGuards = guardsLeft;
		}

		void OnPlayerDied(Health health, DamageInfo info)
		{
			if (Phase != ConquestPhase.Playing) return;
			var marker = health.GetComponent<AiTarget>();
			if (marker != null) marker.Alive = false;
			if (Online)
			{
				// With others a fallen hero is not the end: it comes back, unless everybody is down (the host decides that).
				Fallen = true;
				_respawnAt = Time.time + RespawnSeconds;
				EngineInput.Default.PlayerEnabled = false;
				return;
			}
			Phase = ConquestPhase.Defeat;
			EngineInput.Default.PlayerEnabled = false;
		}

		void Respawn()
		{
			Fallen = false;
			PlayerHealth.Restore();
			var marker = PlayerHealth.GetComponent<AiTarget>();
			if (marker != null) marker.Alive = true;
			var motor = PlayerHealth.GetComponent<CharacterMotor>();
			if (motor != null) motor.Teleport(SpawnPoint, 0f);
			else PlayerHealth.transform.position = SpawnPoint;
			EngineInput.Default.PlayerEnabled = !Paused;
		}

		/// <summary>Starts the assault again from outside the village. With others only the host can, and everybody starts again together.</summary>
		public void Retry()
		{
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
			if (Online)
			{
				if (Net != null && Net.IsHost) Net.RestartForAll();
				return;
			}
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}

		public void Save()
		{
			if (SaveEnabled && Session != null) _store.Save(Session.ToSave());
		}

		/// <summary>Keeps the record and goes back to the shell landing screen.</summary>
		public void SaveAndMenu()
		{
			Save();
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
			ShellFlow.QuitToMenu();
		}

		/// <summary>Keeps the record and goes back to the home village (alone only: with others the hub is not used).</summary>
		public void SaveAndVillage()
		{
			if (Online) return;
			Save();
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
			SceneManager.LoadScene(ConquestScenes.Village);
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
