using ITKamianets.Engine.AI;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Net;
using UnityEngine;

namespace Temnorid.Conquest
{
	/// <summary>
	/// Another player's hero, shown on this machine from the poses that player publishes. On the host it also stands in the world
	/// the way a hero does (a body the guards can see and hit, <see cref="AiTarget"/>, a <see cref="Health"/> that only passes the damage on to the
	/// owner, who is the one that decides what it means for that hero). On a guest it is only a picture.
	/// </summary>
	public sealed class RemoteHero : MonoBehaviour
	{
		public const int AliveBit = 1;
		public const int ChargingBit = 2;
		public const int SwingShift = 8;

		public ulong Owner { get; private set; }
		public string PlayerName { get; private set; } = "";
		public Color Colour { get; private set; }
		public bool Alive { get; private set; } = true;
		public float HealthNow { get; private set; }
		public float HealthMax { get; private set; }

		/// <summary>On the host, the stand-in that guards hurt; its damage is forwarded to the owner.</summary>
		public Health Sink { get; private set; }

		AiTarget _marker;
		Collider _collider;
		RemoteBody _body;
		int _swingSeen = -1;
		bool _charging;

		/// <summary>Builds a hero for a player. <paramref name="simulated"/> is true on the host.</summary>
		public static RemoteHero Create(NetPlayer player, Color colour, bool simulated, System.Action<RemoteHero, DamageInfo> onDamaged)
		{
			var root = new GameObject("Hero " + player.Name);
			var hero = root.AddComponent<RemoteHero>();
			hero.Owner = player.Id;
			hero.PlayerName = player.Name;
			hero.Colour = colour;
			hero.HealthNow = hero.HealthMax = 200f;
			hero._body = root.AddComponent<RemoteBody>();
			hero.BuildVisual(colour, player.Name);

			if (simulated)
			{
				var capsule = root.AddComponent<CapsuleCollider>();
				capsule.radius = 0.4f;
				capsule.height = 1.9f;
				capsule.center = new Vector3(0f, 0.95f, 0f);
				capsule.isTrigger = false;
				hero._collider = capsule;

				var health = root.AddComponent<Health>();
				health.MaxHealth = 1e9f; // never dies here: the owner's own health decides
				health.Team = 1;
				health.Restore();
				hero.Sink = health;
				health.Damaged += (h, info) => onDamaged?.Invoke(hero, info);

				hero._marker = root.AddComponent<AiTarget>();
				hero._marker.Team = 1;
			}
			return hero;
		}

		void BuildVisual(Color colour, string playerName)
		{
			var body = Fx.Material(colour);
			var skin = Fx.Material(new Color(0.88f, 0.7f, 0.55f));
			var steel = Fx.Material(new Color(0.75f, 0.78f, 0.85f));
			var gold = Fx.Material(new Color(1f, 0.82f, 0.25f));

			Part(PrimitiveType.Capsule, "Torso", new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), body);
			Part(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.62f, 0.02f), new Vector3(0.38f, 0.38f, 0.38f), skin);
			Part(PrimitiveType.Cube, "Crown", new Vector3(0f, 1.9f, 0.02f), new Vector3(0.32f, 0.14f, 0.32f), gold);
			Part(PrimitiveType.Cube, "Cape", new Vector3(0f, 1.0f, -0.4f), new Vector3(0.6f, 1.2f, 0.07f), body);
			Part(PrimitiveType.Cube, "Blade", new Vector3(0.5f, 1.0f, 0.45f), new Vector3(0.08f, 0.08f, 1.1f), steel);

			var label = new GameObject("Name");
			label.transform.SetParent(transform, false);
			label.transform.localPosition = new Vector3(0f, 2.5f, 0f);
			var text = label.AddComponent<TextMesh>();
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
			text.text = playerName;
			text.fontSize = 64;
			text.characterSize = 0.07f;
			text.anchor = TextAnchor.MiddleCenter;
			text.color = colour;
			label.AddComponent<FaceCamera>();
		}

		void Part(PrimitiveType type, string partName, Vector3 position, Vector3 scale, Material material)
		{
			var part = GameObject.CreatePrimitive(type);
			part.name = partName;
			Destroy(part.GetComponent<Collider>()); // the hero has one collider of its own (host) or none (guest)
			part.transform.SetParent(transform, false);
			part.transform.localPosition = position;
			part.transform.localScale = scale;
			part.GetComponent<Renderer>().sharedMaterial = material;
		}

		/// <summary>Moves to the owner's newest pose and shows what they are doing.</summary>
		public void Show(NetPose pose)
		{
			_body.Apply(pose);
			var alive = (pose.State & AliveBit) != 0;
			if (alive != Alive)
			{
				Alive = alive;
				SetAlive(alive);
			}
			HealthNow = pose.A;
			HealthMax = Mathf.Max(1f, pose.B);

			var swings = pose.State >> SwingShift;
			if (_swingSeen >= 0 && swings != _swingSeen && alive) ShowSwing();
			_swingSeen = swings;
			_charging = alive && (pose.State & ChargingBit) != 0;
		}

		void SetAlive(bool alive)
		{
			if (_marker != null) _marker.Alive = alive;
			if (_collider != null) _collider.enabled = alive;
			foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = alive;
		}

		void ShowSwing()
		{
			var direction = transform.forward;
			Fx.Pulse(PrimitiveType.Cube, transform.position + Vector3.up * 1.1f + direction * 1.5f, Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f, 30f),
				new Vector3(0.3f, 0.06f, 0.5f), new Vector3(2.8f, 0.06f, 0.5f), new Color(1f, 0.95f, 0.7f), 0.14f);
		}

		void Update()
		{
			if (_charging)
				Fx.Pulse(PrimitiveType.Sphere, transform.position + Vector3.up * 0.9f, Quaternion.identity, Vector3.one * 0.7f, Vector3.one * 0.1f, new Color(0.4f, 0.8f, 1f), 0.2f);
		}

		sealed class FaceCamera : MonoBehaviour
		{
			void LateUpdate()
			{
				var camera = Camera.main;
				if (camera != null) transform.rotation = camera.transform.rotation;
			}
		}
	}
}
