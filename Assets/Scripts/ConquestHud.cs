using ITKamianets.Engine.UI;
using ITKamianets.Engine.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Temnorid.Conquest
{
	/// <summary>
	/// The screen: health, the Charge, the objective, the enemy Lord's health, and the pause, victory and defeat
	/// menus. Plain uGUI built in code; it only shows what <see cref="ConquestGame"/> says.
	/// </summary>
	public sealed class ConquestHud : MonoBehaviour
	{
		public ConquestGame Game;
		public PlayerLord Lord;

		static readonly Color Panel = new Color(0.06f, 0.05f, 0.09f, 0.72f);
		static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

		UiBuilder _ui;
		Text _health, _charge, _objective, _status, _victoryTitle, _victoryBody;
		Image _healthFill, _chargeFill, _lordFill;
		GameObject _lordBar, _pause, _victory, _defeat;

		void Start()
		{
			_ui = new UiBuilder();
			EnsureEventSystem();

			var canvasObject = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			canvasObject.transform.SetParent(transform, false);
			canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
			var scaler = canvasObject.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 0.5f;
			var root = canvasObject.transform;

			var left = Box("LordPanel", root, new Vector2(0.015f, 0.84f), new Vector2(0.27f, 0.985f), Panel);
			_health = Label(left.transform, "Health", "", 30, TextAnchor.UpperLeft, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.97f), Color.white);
			_healthFill = Bar(left.transform, "HealthBar", new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.56f), new Color(0.85f, 0.15f, 0.12f));
			_charge = Label(left.transform, "Charge", "", 26, TextAnchor.UpperLeft, new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.4f), Color.white);
			_chargeFill = Bar(left.transform, "ChargeBar", new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.14f), new Color(0.3f, 0.8f, 1f));

			var objective = Box("ObjectivePanel", root, new Vector2(0.75f, 0.84f), new Vector2(0.985f, 0.985f), Panel);
			_objective = Label(objective.transform, "Text", "", 26, TextAnchor.UpperLeft, new Vector2(0.05f, 0.05f), new Vector2(0.97f, 0.95f), Color.white);

			_status = Label(root, "Status", "", 34, TextAnchor.UpperCenter, new Vector2(0.3f, 0.93f), new Vector2(0.7f, 0.99f), Color.white);
			_lordBar = new GameObject("LordBar", typeof(RectTransform));
			_lordBar.transform.SetParent(root, false);
			var barRect = (RectTransform)_lordBar.transform;
			barRect.anchorMin = new Vector2(0.3f, 0.85f);
			barRect.anchorMax = new Vector2(0.7f, 0.93f);
			barRect.offsetMin = Vector2.zero;
			barRect.offsetMax = Vector2.zero;
			Label(_lordBar.transform, "Name", "THE ENEMY LORD", 28, TextAnchor.MiddleCenter, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Color(1f, 0.6f, 0.5f));
			_lordFill = Bar(_lordBar.transform, "LordFill", new Vector2(0f, 0.05f), new Vector2(1f, 0.45f), new Color(0.75f, 0.1f, 0.1f));

			Label(root, "Hint", "WASD move  ·  Shift run  ·  Space jump  ·  left mouse sword  ·  right mouse charge  ·  V camera  ·  Esc pause", 22, TextAnchor.LowerLeft, new Vector2(0.015f, 0.01f), new Vector2(0.95f, 0.06f), new Color(1f, 1f, 1f, 0.7f));

			_pause = Box("PauseMenu", root, new Vector2(0.35f, 0.32f), new Vector2(0.65f, 0.68f), new Color(0.06f, 0.05f, 0.09f, 0.92f));
			Label(_pause.transform, "Title", "PAUSED", 56, TextAnchor.MiddleCenter, new Vector2(0f, 0.68f), new Vector2(1f, 0.96f), Gold);
			Button(_pause.transform, "Resume", new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.62f), () => Game.SetPaused(false));
			Button(_pause.transform, "Save and quit", new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.34f), () => Game.SaveAndQuit());
			_pause.SetActive(false);

			_victory = Box("Victory", root, new Vector2(0.28f, 0.2f), new Vector2(0.72f, 0.8f), new Color(0.06f, 0.05f, 0.09f, 0.94f));
			_victoryTitle = Label(_victory.transform, "Title", "VILLAGE TAKEN", 60, TextAnchor.MiddleCenter, new Vector2(0f, 0.8f), new Vector2(1f, 0.97f), Gold);
			_victoryBody = Label(_victory.transform, "Body", "", 32, TextAnchor.UpperCenter, new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.78f), Color.white);
			Button(_victory.transform, "Play again", new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.32f), () => Game.Retry());
			Button(_victory.transform, "Quit", new Vector2(0.2f, 0.04f), new Vector2(0.8f, 0.16f), () => Game.SaveAndQuit());
			_victory.SetActive(false);

			_defeat = Box("Defeat", root, new Vector2(0.32f, 0.3f), new Vector2(0.68f, 0.7f), new Color(0.1f, 0.03f, 0.03f, 0.94f));
			Label(_defeat.transform, "Title", "YOU FELL", 64, TextAnchor.MiddleCenter, new Vector2(0f, 0.62f), new Vector2(1f, 0.95f), new Color(1f, 0.4f, 0.35f));
			Label(_defeat.transform, "Note", "The village holds. Try another way in.", 28, TextAnchor.MiddleCenter, new Vector2(0f, 0.5f), new Vector2(1f, 0.62f), Color.white);
			Button(_defeat.transform, "Retry", new Vector2(0.2f, 0.26f), new Vector2(0.8f, 0.46f), () => Game.Retry());
			Button(_defeat.transform, "Quit", new Vector2(0.2f, 0.04f), new Vector2(0.8f, 0.22f), () => Game.SaveAndQuit());
			_defeat.SetActive(false);
		}

		void Update()
		{
			if (Game == null || _health == null) return;

			var health = Game.PlayerHealth;
			SetText(_health, $"HEALTH  {Mathf.CeilToInt(health.Current)} / {Mathf.CeilToInt(health.MaxHealth)}");
			SetFill(_healthFill, health.Fraction);

			var ready = Lord != null ? Lord.ChargeReady : 1f;
			SetText(_charge, ready >= 1f ? "CHARGE  ready (right mouse)" : "CHARGE  recharging");
			SetFill(_chargeFill, ready);

			SetText(_objective, $"Take the village:\ndefeat the enemy Lord\nor get into the hall\nGuards left  {Game.GuardsLeft}");
			var minutes = (int)(Game.RunTime / 60f);
			SetText(_status, $"{minutes}:{Game.RunTime - minutes * 60f:00.0}");

			var lord = Game.EnemyLord;
			var near = lord != null && lord.Health.IsAlive && lord.gameObject.activeInHierarchy
				&& Vector3.Distance(lord.transform.position, health.transform.position) < 32f;
			_lordBar.SetActive(near);
			if (near) SetFill(_lordFill, lord.Health.Fraction);

			_pause.SetActive(Game.Paused);
			var won = Game.Phase == ConquestPhase.Victory;
			var lost = Game.Phase == ConquestPhase.Defeat;
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
				if (lost) Select(_defeat);
			}
			if (Game.Paused && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null) Select(_pause);
		}

		string VictoryText()
		{
			var session = Game.Session;
			var how = Game.Won == VictoryKind.DefeatedLord ? "You defeated the enemy Lord." : "You stormed the hall.";
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
			var button = screen.GetComponentInChildren<UnityEngine.UI.Button>();
			EventSystem.current.SetSelectedGameObject(button != null ? button.gameObject : null);
		}

		// ---- building blocks (shared: 3d-unity-ui) ----

		void EnsureEventSystem() => UiBuilder.EnsureEventSystem(transform);

		static GameObject Box(string name, Transform parent, Vector2 min, Vector2 max, Color colour) => UiBuilder.Box(name, parent, min, max, colour);

		static Image Bar(Transform parent, string name, Vector2 min, Vector2 max, Color colour) => UiBuilder.Bar(name, parent, min, max, colour);

		Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Vector2 min, Vector2 max, Color colour) => _ui.Label(name, parent, text, size, anchor, min, max, colour);

		void Button(Transform parent, string text, Vector2 min, Vector2 max, System.Action onClick) => _ui.Button(text, parent, min, max, onClick);
	}
}
