using System;
using System.Text;
using System.Threading.Tasks;
using ITKamianets.Engine.Account;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using Temnorid.Village;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Temnorid.Conquest
{
	/// <summary>Names of the scenes and the places that lead between the village and the assault.</summary>
	public static class ConquestScenes
	{
		public const string Village = "Village";
		public const string Assault = "Main";
	}

	/// <summary>Plain text for the hub, with no Unity in it so it can be tested.</summary>
	public static class VillageText
	{
		/// <summary>One line per resource: name, what the stores hold now, and the most they hold.</summary>
		public static string Stock(VillageSession session)
		{
			if (session == null) return "";
			var text = new StringBuilder();
			foreach (var id in Res.All)
			{
				if (text.Length > 0) text.Append('\n');
				text.Append(Res.Name(id)).Append("  ").Append(session.Stock.Count(id)).Append(" / ").Append((int)Math.Floor(session.Stock.Capacity(id) + 1e-9));
			}
			return text.ToString();
		}

		/// <summary>"Ann's village" or "Standard village", then why, for the player (never an id or a token).</summary>
		public static string Heading(VillageSession session)
		{
			if (session == null) return "";
			return string.IsNullOrEmpty(session.Status) ? session.Title : session.Title + "  -  " + session.Status;
		}
	}

	/// <summary>
	/// The player's home hub (scene <c>Village</c>): the village and farm drawn with <see cref="VillageView"/> from the player's Lords save
	/// (a Google player with a save) or the standard village (guest, signed out, offline, no save), a hero walking around it, and a gate
	/// that marches out to the assault. Conquest only <b>reads</b> Lords here: the stock is shown, never changed, and nothing is written
	/// (no <see cref="StockSyncer"/>, no <c>SaveStockAsync</c>). Solo only: online play goes straight to the assault.
	/// </summary>
	[DefaultExecutionOrder(-100)]
	public sealed class VillageGame : MonoBehaviour
	{
		/// <summary>Tests (and anything that wants another source) set this to load the village their own way; null uses the signed-in account.</summary>
		public static Func<Amounts, Task<VillageSession>> VillageLoader;

		public static VillageGame Instance { get; private set; }

		const float AccountWaitSeconds = 3f;
		const float LoadSeconds = 6f;

		public VillageView View;
		public CharacterMotor Hero;

		/// <summary>The village is still loading: the hub is usable meanwhile.</summary>
		public bool Loading { get; private set; }

		public bool HasVillage => Village != null;

		/// <summary>The village and its stock (read only here); null while it loads.</summary>
		public VillageSession Village { get; private set; }

		/// <summary>The gate that marches out; null until the village is drawn.</summary>
		public MarchGate Gate { get; private set; }

		public event Action VillageReady;

		void Awake()
		{
			Instance = this;
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;

			// The hub is solo only for now: the online lobby starts the assault itself, so anybody who lands here online goes on to it.
			if (NetSession.Instance.IsOnline)
			{
				SceneManager.LoadScene(ConquestScenes.Assault);
				return;
			}

			if (View == null) View = FindAnyObjectByType<VillageView>();
			if (NeedsNetwork())
			{
				Loading = true;
				StartCoroutine(LoadVillage());
			}
			else
			{
				// Nothing to wait for (guest, signed out, tests): the standard village is there in the first frame.
				var reason = GameAccount.IsSignedIn ? "Guest account: showing the standard village. Sign in with Google to see your own." : "Not signed in: showing the standard village.";
				Apply(VillageSource.CreateDefault(null, reason, SharedStock.SystemClock()));
			}
		}

		bool _started;

		// The hero wakes after this component (its motor is not ready in Awake), so it is put on the spawn spot from here.
		void Start()
		{
			_started = true;
			if (Village != null) PlaceHero();
		}

		void PlaceHero()
		{
			if (Hero != null && View != null) Hero.Teleport(View.SpawnPosition, 0f);
		}

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		static bool NeedsNetwork() => VillageLoader != null || (GameAccount.IsSignedIn && !GameAccount.IsGuest) || GameAccount.Busy;

		System.Collections.IEnumerator LoadVillage()
		{
			// A sign-in that is still running (the shell restores the account in the background) gets a few seconds; never longer.
			var end = Time.unscaledTime + AccountWaitSeconds;
			while (GameAccount.Busy && Time.unscaledTime < end) yield return null;

			Task<VillageSession> task;
			try
			{
				task = VillageLoader != null ? VillageLoader(null) : VillageSource.ForAccount().LoadAsync(null, TimeSpan.FromSeconds(LoadSeconds));
			}
			catch (Exception exception)
			{
				task = Task.FromResult(Fallback("Could not load the village (" + exception.GetType().Name + "): showing the standard village."));
			}
			var limit = Time.unscaledTime + LoadSeconds + 2f;
			while (!task.IsCompleted && Time.unscaledTime < limit) yield return null;
			var session = task.IsCompleted && !task.IsFaulted && !task.IsCanceled && task.Result != null
				? task.Result
				: Fallback("The village took too long to load: showing the standard village.");
			Apply(session);
		}

		static VillageSession Fallback(string why) => VillageSource.CreateDefault(null, why, SharedStock.SystemClock());

		void Apply(VillageSession session)
		{
			Village = session;
			Loading = false;
			if (View == null) View = new GameObject("Village").AddComponent<VillageView>();
			View.Build(session.Data);

			if (Gate == null) Gate = MarchGate.Create(View.transform, GatePosition(session.Data), View.LitTemplate);
			if (_started) PlaceHero();
			Debug.Log("[Conquest] " + (session.IsLords ? "Showing the player's Lords village." : "Showing the standard village."));
			VillageReady?.Invoke();
		}

		/// <summary>The gate stands just past the south edge of the farm, on open ground.</summary>
		public static Vector3 GatePosition(VillageData data) => new Vector3(0f, 0f, -(data.FarmRz + 12f));

		/// <summary>Marches out to the assault. Alone only; the stock is not touched.</summary>
		public void MarchOut()
		{
			Time.timeScale = 1f;
			SceneManager.LoadScene(ConquestScenes.Assault);
		}
	}
}
