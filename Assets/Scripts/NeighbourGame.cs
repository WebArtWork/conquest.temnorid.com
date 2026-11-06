using ITKamianets.Engine.Character;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Shell;
using ITKamianets.Engine.UI;
using Temnorid.Village;
using UnityEngine;
using UnityEngine.UI;

namespace Temnorid.Conquest
{
	/// <summary>
	/// The neighbouring village, outside the area of the player's own: a village like ours (the standard one, always with its stone wall) whose four
	/// gates are shut. The hero stands outside it and can walk round, but cannot get in yet; a road behind leads back home. Sieges will come here later.
	/// </summary>
	public sealed class NeighbourGame : MonoBehaviour
	{
		public VillageView View;
		public CharacterMotor Hero;

		const float GateReach = 14f;

		VillageData _data;
		readonly System.Collections.Generic.List<Vector3> _gates = new System.Collections.Generic.List<Vector3>();
		MarchGate _home;
		UiBuilder _ui;
		ShellChrome _chrome;
		Text _prompt;
		bool _started;

		void Awake()
		{
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;

			var session = VillageSource.CreateDefault(null, "The neighbouring village.", SharedStock.SystemClock());
			_data = session.Data;
			if (_data.WallLevel < 3) _data.WallLevel = 3;
			if (View == null) View = FindAnyObjectByType<VillageView>();
			View.Build(_data);

			// Only the village and its wall: the farm's fields are not shown.
			foreach (var plot in View.Plots) if (plot.Root != null) plot.Root.gameObject.SetActive(false);
			CloseGates();

			// The way home lies behind the hero, on open ground.
			_home = MarchGate.Create(View.transform, new Vector3(0f, 0f, -(_data.VillageRz + 70f)), View.LitTemplate, ConquestScenes.Village, new Color(0.2f, 0.35f, 0.8f));
		}

		// The hero wakes after this component (its motor is not ready in Awake), so it is put on its spot from here.
		void Start()
		{
			_started = true;
			if (Hero != null) Hero.Teleport(new Vector3(0f, 0.1f, -(_data.VillageRz + 30f)), 0f);

			_ui = ShellUi.Builder();
			UiBuilder.EnsureEventSystem(transform);
			_chrome = ShellChrome.Create(transform);
			_chrome.SetTitle("Neighbouring village");

			var canvas = UiBuilder.CreateCanvas(transform, "HudCanvas");
			canvas.sortingOrder = 0;
			var safe = new GameObject("Safe", typeof(RectTransform), typeof(SafeAreaFitter));
			safe.transform.SetParent(canvas.transform, false);
			var hud = new GameObject("Hud", typeof(RectTransform));
			hud.transform.SetParent(safe.transform, false);
			var rect = (RectTransform)hud.transform;
			rect.anchorMin = new Vector2(0f, _chrome.BottomFraction);
			rect.anchorMax = new Vector2(1f, 1f - _chrome.TopFraction);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			_prompt = _ui.Label("Prompt", hud.transform, "", 44, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.22f), Color.white);
		}

		/// <summary>A heavy door in each of the four gaps of the wall, with a red banner over it: the village is shut.</summary>
		void CloseGates()
		{
			var lit = View.LitTemplate;
			var wood = Mat(lit, new Color(0.30f, 0.20f, 0.12f));
			var iron = Mat(lit, new Color(0.18f, 0.18f, 0.2f));
			var red = Mat(lit, new Color(0.7f, 0.1f, 0.1f));
			var height = Mathf.Min(6f, 3f + 0.4f * Mathf.Max(1, _data.WallLevel));
			var root = new GameObject("ClosedGates").transform;
			root.SetParent(View.Root != null ? View.Root : View.transform, false);

			for (var k = 0; k < 4; k++)
			{
				var theta = k * 90f * Mathf.Deg2Rad;
				var half = 11f * Mathf.Deg2Rad;
				var a = new Vector3(_data.VillageRx * Mathf.Cos(theta - half), 0f, _data.VillageRz * Mathf.Sin(theta - half));
				var b = new Vector3(_data.VillageRx * Mathf.Cos(theta + half), 0f, _data.VillageRz * Mathf.Sin(theta + half));
				var centre = (a + b) * 0.5f;
				var length = (b - a).magnitude;
				var rotation = Quaternion.LookRotation((b - a).normalized);

				var door = Piece(PrimitiveType.Cube, "Door" + k, root, centre + Vector3.up * (height * 0.5f), new Vector3(1.6f, height, length), wood, true);
				door.rotation = rotation;
				for (var i = 0; i < 3; i++)
				{
					var band = Piece(PrimitiveType.Cube, "Band" + k + i, root, centre + Vector3.up * (height * (0.2f + 0.3f * i)), new Vector3(1.8f, 0.25f, length + 0.2f), iron, false);
					band.rotation = rotation;
				}
				var banner = Piece(PrimitiveType.Cube, "Banner" + k, root, centre + Vector3.up * (height + 1.2f), new Vector3(0.3f, 2.4f, 3f), red, false);
				banner.rotation = rotation;
				_gates.Add(root.TransformPoint(centre));
			}
		}

		static Material Mat(Material template, Color colour)
		{
			var material = new Material(template) { color = colour };
			return material;
		}

		static Transform Piece(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, bool collider)
		{
			var piece = GameObject.CreatePrimitive(type);
			piece.name = name;
			if (!collider)
			{
				var c = piece.GetComponent<Collider>();
				if (c != null) Destroy(c);
			}
			piece.transform.SetParent(parent, false);
			piece.transform.localPosition = localPosition;
			piece.transform.localScale = scale;
			piece.GetComponent<Renderer>().sharedMaterial = material;
			return piece.transform;
		}

		void Update()
		{
			if (_prompt == null || Hero == null) return;
			var position = Hero.transform.position;
			var near = false;
			foreach (var gate in _gates)
			{
				var flat = gate - position;
				flat.y = 0f;
				if (flat.magnitude < GateReach) near = true;
			}
			_prompt.text = near ? "The gate is closed." : "";
		}
	}
}
