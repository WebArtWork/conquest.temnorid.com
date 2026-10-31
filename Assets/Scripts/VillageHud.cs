using ITKamianets.Engine.Input;
using ITKamianets.Engine.Shell;
using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Temnorid.Conquest
{
	/// <summary>
	/// The screen of the village hub: the shell bars (places Village and Assault), the village's name and why it is shown, the six stocks
	/// (read only), "Loading your village..." while a Lords save is read, a March out button, and on a phone a stick, a look area, Jump and
	/// Run. On a computer Esc frees the mouse for the buttons and the tabs. Plain uGUI built in code.
	/// </summary>
	public sealed class VillageHud : MonoBehaviour
	{
		public VillageGame Game;

		public const string VillagePlace = "village";
		public const string AssaultPlace = ConquestShell.AssaultPlace;

		static readonly Color Panel = new Color(0.06f, 0.05f, 0.09f, 0.82f);
		static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

		UiBuilder _ui;
		ShellChrome _chrome;
		Text _heading, _stock, _loading, _hint;
		GameObject _hudRoot, _touchRoot;
		bool _mouseFree;
		string _shownStock = "";
		float _nextStock;

		public ShellChrome Chrome => _chrome;
		public string StockText => _stock != null ? _stock.text : "";
		public string LoadingText => _loading != null ? _loading.text : "";

		void Start()
		{
			_ui = ShellUi.Builder();
			_ui.ButtonFontSize = 38;
			UiBuilder.EnsureEventSystem(transform);

			_chrome = ShellChrome.Create(transform);
			_chrome.SetPlaces(new[]
			{
				new ShellPlace(VillagePlace, "Village", "V"),
				new ShellPlace(AssaultPlace, "Assault", "A"),
			});
			_chrome.PlaceSelected += OnPlace;

			var canvas = UiBuilder.CreateCanvas(transform, "HudCanvas");
			canvas.sortingOrder = 0;
			var safe = new GameObject("Safe", typeof(RectTransform), typeof(SafeAreaFitter));
			safe.transform.SetParent(canvas.transform, false);
			_hudRoot = new GameObject("Hud", typeof(RectTransform));
			_hudRoot.transform.SetParent(safe.transform, false);
			var rect = (RectTransform)_hudRoot.transform;
			rect.anchorMin = new Vector2(0f, _chrome.BottomFraction);
			rect.anchorMax = new Vector2(1f, 1f - _chrome.TopFraction);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			var root = _hudRoot.transform;

			if (TouchButton.IsNeeded) BuildTouchControls();

			var panel = UiBuilder.Box("StockPanel", root, new Vector2(0.015f, 0.5f), new Vector2(0.3f, 0.985f), Panel);
			_ui.Label("Title", panel.transform, "STOCK", 30, TextAnchor.UpperLeft, new Vector2(0.06f, 0.88f), new Vector2(0.95f, 0.99f), Gold);
			_stock = _ui.Label("Stock", panel.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.06f, 0.02f), new Vector2(0.95f, 0.88f), Color.white);

			_heading = _ui.Label("Heading", root, "", 28, TextAnchor.UpperCenter, new Vector2(0.32f, 0.9f), new Vector2(0.98f, 0.99f), new Color(1f, 0.95f, 0.7f), true);
			_loading = _ui.Label("Loading", root, "", 48, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.42f), new Vector2(0.75f, 0.58f), Color.white);
			_hint = _ui.Label("Hint", root, TouchButton.IsNeeded ? "" : "WASD move  ·  Shift run  ·  Space jump  ·  V camera  ·  Esc frees the mouse  ·  walk through the red gate to march out", 26, TextAnchor.LowerLeft, new Vector2(0.015f, 0.01f), new Vector2(0.7f, 0.07f), new Color(1f, 1f, 1f, 0.7f));
			_ui.Button("March out", root, new Vector2(0.72f, 0.02f), new Vector2(0.985f, 0.14f), () => Game.MarchOut());
		}

		void BuildTouchControls()
		{
			if (_touchRoot != null) return;
			_touchRoot = new GameObject("Touch", typeof(RectTransform));
			_touchRoot.transform.SetParent(_hudRoot.transform, false);
			var rect = (RectTransform)_touchRoot.transform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			_touchRoot.transform.SetAsFirstSibling();
			var root = _touchRoot.transform;

			var look = UiBuilder.Box("LookArea", root, new Vector2(0.35f, 0f), Vector2.one, new Color(1f, 1f, 1f, 0f));
			look.AddComponent<TouchLookArea>();

			var ring = UiBuilder.Box("MoveRing", root, new Vector2(0.02f, 0.05f), new Vector2(0.2f, 0.45f), new Color(1f, 1f, 1f, 0.1f));
			ring.GetComponent<Image>().raycastTarget = false;
			var handle = UiBuilder.Box("MoveHandle", ring.transform, new Vector2(0.28f, 0.28f), new Vector2(0.72f, 0.72f), new Color(1f, 1f, 1f, 0.35f));
			var stick = handle.AddComponent<OnScreenStick>();
			stick.controlPath = EngineTouchControls.Path(EngineTouchControls.Move);
			stick.movementRange = 110f;

			TouchKey(root, "Jump", EngineTouchControls.Jump, new Vector2(0.84f, 0.34f), new Vector2(0.97f, 0.52f));
			TouchKey(root, "Run", EngineTouchControls.Sprint, new Vector2(0.22f, 0.06f), new Vector2(0.34f, 0.22f));
		}

		void TouchKey(Transform parent, string label, string control, Vector2 min, Vector2 max)
		{
			var box = UiBuilder.Box("Touch " + label, parent, min, max, new Color(1f, 1f, 1f, 0.2f));
			_ui.Label("Text", box.transform, label, 34, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.92f));
			box.AddComponent<OnScreenButton>().controlPath = EngineTouchControls.Path(control);
		}

		void OnPlace(string id)
		{
			if (id == AssaultPlace && Game != null) Game.MarchOut();
		}

		void Update()
		{
			if (Game == null || _stock == null) return;

			SetText(_loading, Game.Loading ? "Loading your village…" : "");
			if (_chrome != null) _chrome.SetStatus(Game.Village != null ? Game.Village.Title : "Loading");
			SetText(_heading, VillageText.Heading(Game.Village));
			var stock = _shownStock;
			if (Time.unscaledTime >= _nextStock || _shownStock.Length == 0)
			{
				_nextStock = Time.unscaledTime + 0.5f;
				stock = Game.Village != null ? VillageText.Stock(Game.Village) : "";
			}
			if (stock != _shownStock)
			{
				_shownStock = stock;
				SetText(_stock, stock);
			}

			// Esc frees the mouse for the buttons and tabs (the same toggle the assault's pause uses); a phone needs no mouse.
			if (EngineInput.Default.UI.Pause.WasPressedThisFrame())
			{
				_mouseFree = !_mouseFree;
				EngineInput.Default.PlayerEnabled = !_mouseFree;
			}
		}

		static void SetText(Text label, string value)
		{
			if (label.text != value) label.text = value;
		}
	}
}
