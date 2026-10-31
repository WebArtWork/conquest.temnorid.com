using System.Text;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Temnorid.Conquest
{
	/// <summary>
	/// The screen: health, the Charge, the objective, the enemy Lord's health, and the pause, victory and defeat
	/// menus, kept inside the strips the shell's bars leave free. The shell bar has two places, the assault itself and an Objectives panel.
	/// Plain uGUI built in code; it only shows what <see cref="ConquestGame"/> says. On a phone it also draws the touch controls
	/// (a move stick, a look area, attack, Charge, run and jump) that feed the same actions the keyboard and mouse do.
	/// </summary>
	public sealed class ConquestHud : MonoBehaviour
	{
		public ConquestGame Game;
		public PlayerLord Lord;

		static readonly Color Panel = new Color(0.06f, 0.05f, 0.09f, 0.82f);
		static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

		UiBuilder _ui;
		ShellChrome _chrome;
		Text _health, _charge, _objective, _status, _victoryTitle, _victoryBody, _fallen, _note, _objectivesBody, _hint;
		Image _healthFill, _chargeFill, _lordFill;
		GameObject _lordBar, _pause, _victory, _defeat, _objectives, _hudRoot, _touchRoot;
		Button _victoryAgain, _defeatAgain, _victoryVillage, _defeatVillage;
		Text _defeatNote;
		ConquestPhase _shownPhase;
		float _nextObjectivesRefresh;

		public ShellChrome Chrome => _chrome;

		void Start()
		{
			_ui = ShellUi.Builder();
			_ui.ButtonFontSize = 38;
			UiBuilder.EnsureEventSystem(transform);

			var canvas = UiBuilder.CreateCanvas(transform, "HudCanvas");
			canvas.sortingOrder = 0;
			var safe = new GameObject("Safe", typeof(RectTransform), typeof(SafeAreaFitter));
			safe.transform.SetParent(canvas.transform, false);

			_chrome = ShellChrome.Create(transform);
			// Alone, the village hub is one tap away; with others the hub is not used.
			if (!Game.Online) _chrome.SetPlaces(new[] { new ShellPlace(ConquestShell.AssaultPlace, "Assault", "A"), new ShellPlace(ConquestShell.ObjectivesPlace, "Objectives", "O"), new ShellPlace(ConquestShell.VillagePlace, "Village", "V") });
			_chrome.PlaceSelected += OnPlace;

			// Everything of the game stays between the top bar and the bottom tabs.
			_hudRoot = new GameObject("Hud", typeof(RectTransform));
			_hudRoot.transform.SetParent(safe.transform, false);
			Fit((RectTransform)_hudRoot.transform);
			var root = _hudRoot.transform;

			if (TouchButton.IsNeeded) BuildTouchControls();

			var left = Box("LordPanel", root, new Vector2(0.015f, 0.82f), new Vector2(0.27f, 0.985f), Panel);
			_health = Label(left.transform, "Health", "", 32, TextAnchor.UpperLeft, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.97f), Color.white);
			_healthFill = Bar(left.transform, "HealthBar", new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.56f), new Color(0.85f, 0.15f, 0.12f));
			_charge = Label(left.transform, "Charge", "", 28, TextAnchor.UpperLeft, new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.4f), Color.white);
			_chargeFill = Bar(left.transform, "ChargeBar", new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.14f), new Color(0.3f, 0.8f, 1f));

			var objective = Box("ObjectivePanel", root, new Vector2(0.73f, 0.8f), new Vector2(0.985f, 0.985f), Panel);
			_objective = Label(objective.transform, "Text", "", 28, TextAnchor.UpperLeft, new Vector2(0.05f, 0.05f), new Vector2(0.97f, 0.95f), Color.white);

			_status = Label(root, "Status", "", 36, TextAnchor.UpperCenter, new Vector2(0.3f, 0.93f), new Vector2(0.7f, 0.99f), Color.white);
			_lordBar = new GameObject("LordBar", typeof(RectTransform));
			_lordBar.transform.SetParent(root, false);
			var barRect = (RectTransform)_lordBar.transform;
			barRect.anchorMin = new Vector2(0.3f, 0.85f);
			barRect.anchorMax = new Vector2(0.7f, 0.93f);
			barRect.offsetMin = Vector2.zero;
			barRect.offsetMax = Vector2.zero;
			Label(_lordBar.transform, "Name", "THE ENEMY LORD", 30, TextAnchor.MiddleCenter, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Color(1f, 0.6f, 0.5f));
			_lordFill = Bar(_lordBar.transform, "LordFill", new Vector2(0f, 0.05f), new Vector2(1f, 0.45f), new Color(0.75f, 0.1f, 0.1f));

			_hint = Label(root, "Hint", HintText(), 26, TextAnchor.LowerLeft, new Vector2(0.015f, 0.01f), new Vector2(0.95f, 0.07f), new Color(1f, 1f, 1f, 0.7f));
			_hint.gameObject.SetActive(!TouchButton.IsNeeded); // a phone has its buttons on the screen instead
			_note = Label(root, "Note", "", 30, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.74f), new Vector2(0.75f, 0.82f), Gold);
			_fallen = Label(root, "Fallen", "", 64, TextAnchor.MiddleCenter, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.62f), new Color(1f, 0.4f, 0.35f));

			BuildPause(root);
			BuildVictory(root);
			BuildDefeat(root);
			BuildObjectives(root);
			_shownPhase = ConquestPhase.Playing;
		}

		string HintText()
		{
			if (TouchButton.IsNeeded) return "";
			return "WASD move  ·  Shift run  ·  Space jump  ·  left mouse sword  ·  right mouse charge  ·  V camera  ·  Esc pause and objectives";
		}

		// ---- phone controls ----

		/// <summary>
		/// Draws the touch controls: a stick (left thumb), a look area (anywhere on the right), Attack, Charge, Jump and Run. They write to the
		/// virtual touch device of <see cref="EngineInput"/>, so the character, the camera and <see cref="PlayerLord"/> read them like keys.
		/// Built on a phone by default; tests build them on purpose.
		/// </summary>
		public void BuildTouchControls()
		{
			if (_touchRoot != null) return;
			var parent = _hudRoot.transform;
			_touchRoot = new GameObject("Touch", typeof(RectTransform));
			_touchRoot.transform.SetParent(parent, false);
			Fit((RectTransform)_touchRoot.transform);
			_touchRoot.transform.SetAsFirstSibling(); // under the panels
			var root = _touchRoot.transform;

			var look = UiBuilder.Box("LookArea", root, new Vector2(0.35f, 0f), Vector2.one, new Color(1f, 1f, 1f, 0f));
			look.AddComponent<TouchLookArea>();

			var ring = UiBuilder.Box("MoveRing", root, new Vector2(0.02f, 0.05f), new Vector2(0.2f, 0.45f), new Color(1f, 1f, 1f, 0.1f));
			ring.GetComponent<Image>().raycastTarget = false;
			var handle = UiBuilder.Box("MoveHandle", ring.transform, new Vector2(0.28f, 0.28f), new Vector2(0.72f, 0.72f), new Color(1f, 1f, 1f, 0.35f));
			var stick = handle.AddComponent<OnScreenStick>();
			stick.controlPath = EngineTouchControls.Path(EngineTouchControls.Move);
			stick.movementRange = 110f;

			TouchKey(root, "Attack", EngineTouchControls.Primary, new Vector2(0.8f, 0.06f), new Vector2(0.97f, 0.3f), 40);
			TouchKey(root, "Charge", EngineTouchControls.Secondary, new Vector2(0.62f, 0.06f), new Vector2(0.78f, 0.26f), 34);
			TouchKey(root, "Jump", EngineTouchControls.Jump, new Vector2(0.84f, 0.34f), new Vector2(0.97f, 0.52f), 34);
			TouchKey(root, "Run", EngineTouchControls.Sprint, new Vector2(0.22f, 0.06f), new Vector2(0.34f, 0.22f), 34);
		}

		void TouchKey(Transform parent, string label, string control, Vector2 min, Vector2 max, int size)
		{
			var box = UiBuilder.Box("Touch " + label, parent, min, max, new Color(1f, 1f, 1f, 0.2f));
			_ui.Label("Text", box.transform, label, size, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.92f));
			var key = box.AddComponent<OnScreenButton>();
			key.controlPath = EngineTouchControls.Path(control);
		}

		// ---- places ----

		void OnPlace(string id)
		{
			if (id == ConquestShell.VillagePlace) { if (Game != null) Game.SaveAndVillage(); return; }
			if (_objectives == null) return;
			var objectives = id == ConquestShell.ObjectivesPlace;
			_objectives.SetActive(objectives);
			if (objectives) RefreshObjectives();
			// Away from the assault (Objectives, Profile) the player is in a menu: alone the world waits, with others it goes on.
			if (Game != null) Game.SetPaused(id != ConquestShell.AssaultPlace);
		}

		void BuildObjectives(Transform parent)
		{
			_objectives = Box("Objectives", parent, new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.94f), Panel);
			Label(_objectives.transform, "Title", "Objectives", 54, TextAnchor.MiddleCenter, new Vector2(0f, 0.86f), new Vector2(1f, 0.98f), Gold);
			_objectivesBody = Label(_objectives.transform, "Body", "", 32, TextAnchor.UpperLeft, new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.85f), Color.white, true);
			Button(_objectives.transform, "Back to the assault", new Vector2(0.2f, 0.05f), new Vector2(0.8f, 0.18f), () => _chrome.Select(ConquestShell.AssaultPlace));
			_objectives.SetActive(false);
		}

		void RefreshObjectives()
		{
			var text = new StringBuilder();
			var lord = Game.EnemyLord;
			var lordAlive = lord != null && lord.Health.IsAlive;
			text.Append("Take the village, either way:\n");
			text.Append("  defeat the enemy Lord  -  ").Append(Game.Phase == ConquestPhase.Victory && Game.Won == VictoryKind.DefeatedLord ? "done" : lordAlive ? "he still stands" : "he has fallen").Append('\n');
			text.Append("  or get into the hall  -  ").Append(Game.Phase == ConquestPhase.Victory && Game.Won == VictoryKind.TookHall ? "done" : "not yet").Append('\n');
			text.Append("Guards left  ").Append(Game.GuardsLeft).Append("\n\n");

			var net = Game.Net;
			if (Game.Online && net != null)
			{
				text.Append("Heroes in the assault:\n");
				text.Append("  ").Append(net.LocalName).Append("  (you)  ").Append(Mathf.CeilToInt(Game.PlayerHealth.Current)).Append(" hp\n");
				foreach (var hero in net.Heroes.Values)
					if (hero != null) text.Append("  ").Append(hero.PlayerName).Append("  ").Append(hero.Alive ? Mathf.CeilToInt(hero.HealthNow) + " hp" : "down").Append('\n');
				text.Append('\n');
			}

			var session = Game.Session;
			text.Append("Villages taken  ").Append(session.Completions);
			if (session.BestSeconds > 0f) text.Append("     Best time  ").Append(Format(session.BestSeconds));
			UiBuilder.SetText(_objectivesBody, text.ToString());
		}

		// ---- screens ----

		void BuildPause(Transform parent)
		{
			_pause = Box("PauseMenu", parent, new Vector2(0.33f, 0.2f), new Vector2(0.67f, 0.8f), Panel);
			Label(_pause.transform, "Title", "PAUSED", 56, TextAnchor.MiddleCenter, new Vector2(0f, 0.78f), new Vector2(1f, 0.96f), Gold);
			Button(_pause.transform, "Resume", new Vector2(0.15f, 0.55f), new Vector2(0.85f, 0.74f), () => Game.SetPaused(false));
			Button(_pause.transform, "Objectives", new Vector2(0.15f, 0.34f), new Vector2(0.85f, 0.52f), () => _chrome.Select(ConquestShell.ObjectivesPlace));
			Button(_pause.transform, "Main menu", new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.28f), () => Game.SaveAndMenu());
			Label(_pause.transform, "Note", "", 24, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 0.09f), new Color(1f, 1f, 1f, 0.7f));
			_pause.SetActive(false);
		}

		void BuildVictory(Transform parent)
		{
			_victory = Box("Victory", parent, new Vector2(0.26f, 0.08f), new Vector2(0.74f, 0.92f), Panel);
			_victoryTitle = Label(_victory.transform, "Title", "VILLAGE TAKEN", 60, TextAnchor.MiddleCenter, new Vector2(0f, 0.82f), new Vector2(1f, 0.97f), Gold);
			_victoryBody = Label(_victory.transform, "Body", "", 32, TextAnchor.UpperCenter, new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.8f), Color.white);
			_victoryAgain = _ui.Button("Play again", _victory.transform, new Vector2(0.15f, 0.235f), new Vector2(0.85f, 0.335f), () => Game.Retry());
			_victoryVillage = _ui.Button("Return to the village", _victory.transform, new Vector2(0.15f, 0.125f), new Vector2(0.85f, 0.225f), () => Game.SaveAndVillage());
			Button(_victory.transform, "Main menu", new Vector2(0.15f, 0.015f), new Vector2(0.85f, 0.115f), () => Game.SaveAndMenu());
			_victory.SetActive(false);
		}

		void BuildDefeat(Transform parent)
		{
			_defeat = Box("Defeat", parent, new Vector2(0.3f, 0.22f), new Vector2(0.7f, 0.78f), new Color(0.1f, 0.03f, 0.03f, 0.94f));
			Label(_defeat.transform, "Title", "YOU FELL", 64, TextAnchor.MiddleCenter, new Vector2(0f, 0.7f), new Vector2(1f, 0.95f), new Color(1f, 0.4f, 0.35f));
			_defeatNote = Label(_defeat.transform, "Note", "The village holds. Try another way in.", 30, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.52f), new Vector2(0.97f, 0.7f), Color.white, true);
			_defeatAgain = _ui.Button("Retry", _defeat.transform, new Vector2(0.15f, 0.37f), new Vector2(0.85f, 0.5f), () => Game.Retry());
			_defeatVillage = _ui.Button("Return to the village", _defeat.transform, new Vector2(0.15f, 0.215f), new Vector2(0.85f, 0.345f), () => Game.SaveAndVillage());
			Button(_defeat.transform, "Main menu", new Vector2(0.15f, 0.06f), new Vector2(0.85f, 0.19f), () => Game.SaveAndMenu());
			_defeat.SetActive(false);
		}

		void Update()
		{
			if (Game == null || _health == null) return;

			var health = Game.PlayerHealth;
			SetText(_health, $"HEALTH  {Mathf.CeilToInt(health.Current)} / {Mathf.CeilToInt(health.MaxHealth)}");
			SetFill(_healthFill, health.Fraction);

			var ready = Lord != null ? Lord.ChargeReady : 1f;
			SetText(_charge, ready >= 1f ? (TouchButton.IsNeeded ? "CHARGE  ready" : "CHARGE  ready (right mouse)") : "CHARGE  recharging");
			SetFill(_chargeFill, ready);

			SetText(_objective, ObjectiveText());
			var minutes = (int)(Game.RunTime / 60f);
			SetText(_status, $"{minutes}:{Game.RunTime - minutes * 60f:00.0}");
			if (_chrome != null) _chrome.SetStatus(Game.Online ? "Guards " + Game.GuardsLeft : "");

			var lord = Game.EnemyLord;
			var near = lord != null && lord.Health.IsAlive && lord.gameObject.activeInHierarchy
				&& Vector3.Distance(lord.transform.position, health.transform.position) < 32f;
			_lordBar.SetActive(near);
			if (near) SetFill(_lordFill, lord.Health.Fraction);

			SetText(_fallen, Game.Fallen && Game.Phase == ConquestPhase.Playing ? $"YOU FELL  -  back in {Mathf.CeilToInt(Game.RespawnIn)}" : "");
			var net = Game.Net;
			var showNote = net != null && Time.unscaledTime - net.NoteAt < 5f;
			SetText(_note, showNote ? net.Note : "");

			var onAssault = _chrome == null || _chrome.CurrentPlace == ConquestShell.AssaultPlace;
			_pause.SetActive(Game.Paused && onAssault && Game.Phase == ConquestPhase.Playing);

			// Esc while a panel is open resumes: go back to the assault.
			if (_chrome != null && !onAssault && !_chrome.ProfileOpen && !Game.Paused && Game.Phase == ConquestPhase.Playing) _chrome.Select(ConquestShell.AssaultPlace);

			var phase = Game.Phase;
			var won = phase == ConquestPhase.Victory;
			var lost = phase == ConquestPhase.Defeat;
			if (phase != _shownPhase)
			{
				_shownPhase = phase;
				if (_chrome != null && _chrome.CurrentPlace != ConquestShell.AssaultPlace) _chrome.Select(ConquestShell.AssaultPlace, false);
				_objectives.SetActive(false);
				onAssault = true;
			}
			if (won != _victory.activeSelf)
			{
				_victory.SetActive(won);
				if (won)
				{
					SetText(_victoryBody, VictoryText());
					Select(_victory);
				}
			}
			if (lost != _defeat.activeSelf)
			{
				_defeat.SetActive(lost);
				if (lost)
				{
					SetText(_defeatNote, Game.Online ? "Every hero fell. The village holds." : "The village holds. Try another way in.");
					Select(_defeat);
				}
			}

			// With others only the host can start the assault again.
			var waiting = Game.Online && !(Game.Net != null && Game.Net.IsHost);
			SetAgain(_victoryAgain, waiting, "Play again");
			SetAgain(_defeatAgain, waiting, "Retry");
			// The home village is for playing alone; with others the hub is not used.
			if (_victoryVillage.gameObject.activeSelf == Game.Online) _victoryVillage.gameObject.SetActive(!Game.Online);
			if (_defeatVillage.gameObject.activeSelf == Game.Online) _defeatVillage.gameObject.SetActive(!Game.Online);

			if (_objectives.activeSelf && Time.unscaledTime >= _nextObjectivesRefresh)
			{
				_nextObjectivesRefresh = Time.unscaledTime + 0.5f;
				RefreshObjectives();
			}
			if (Game.Paused && onAssault && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && !TouchButton.IsNeeded) Select(_pause);
		}

		string ObjectiveText()
		{
			var text = "Take the village:\ndefeat the enemy Lord\nor get into the hall\nGuards left  " + Game.GuardsLeft;
			if (!Game.Online || Game.Net == null) return text;
			var standing = Game.PlayerHealth.IsAlive && !Game.Fallen ? 1 : 0;
			foreach (var hero in Game.Net.Heroes.Values) if (hero != null && hero.Alive) standing++;
			return text + "\nHeroes standing  " + standing + " / " + (Game.Net.Heroes.Count + 1);
		}

		static void SetAgain(Button button, bool waiting, string label)
		{
			button.interactable = !waiting;
			var text = button.GetComponentInChildren<Text>();
			if (text != null) SetText(text, waiting ? "Waiting for the host" : label);
		}

		string VictoryText()
		{
			var session = Game.Session;
			var how = Game.Won == VictoryKind.DefeatedLord ? "The enemy Lord has fallen." : "The hall is stormed.";
			var best = Game.NewBestTime ? "(new best!)" : "best  " + Format(session.BestSeconds);
			return $"{how}\n\nTime  {Format(Game.RunTime)}   {best}\n\nVillages taken  {session.Completions}\nBy the Lord  {session.LordVictories}    By the hall  {session.HallVictories}";
		}

		static string Format(float seconds)
		{
			var minutes = (int)(seconds / 60f);
			return $"{minutes}:{seconds - minutes * 60f:00.0}";
		}

		static void SetText(Text label, string value)
		{
			if (label.text != value) label.text = value;
		}

		static void SetFill(Image fill, float fraction)
		{
			fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
		}

		static void Select(GameObject screen)
		{
			if (EventSystem.current == null) return;
			var button = screen.GetComponentInChildren<Button>();
			EventSystem.current.SetSelectedGameObject(button != null ? button.gameObject : null);
		}

		void Fit(RectTransform rect)
		{
			rect.anchorMin = new Vector2(0f, _chrome != null ? _chrome.BottomFraction : 0f);
			rect.anchorMax = new Vector2(1f, 1f - (_chrome != null ? _chrome.TopFraction : 0f));
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}

		// ---- building blocks (shared: 3d-unity-ui) ----

		static GameObject Box(string name, Transform parent, Vector2 min, Vector2 max, Color colour) => UiBuilder.Box(name, parent, min, max, colour);

		static Image Bar(Transform parent, string name, Vector2 min, Vector2 max, Color colour) => UiBuilder.Bar(name, parent, min, max, colour);

		Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Vector2 min, Vector2 max, Color colour, bool wrap = false) => _ui.Label(name, parent, text, size, anchor, min, max, colour, wrap);

		void Button(Transform parent, string text, Vector2 min, Vector2 max, System.Action onClick) => _ui.Button(text, parent, min, max, onClick);
	}
}
