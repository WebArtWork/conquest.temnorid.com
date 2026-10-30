using ITKamianets.Engine.Feedback;
using System.Collections;
using System.Collections.Generic;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Input;
using UnityEngine;

namespace Temnorid.Conquest
{
	/// <summary>
	/// The player's Lord in a fight: a sword swing on the main action (left mouse) and the Charge on the second
	/// (right mouse): a short rush forward that hits everyone it runs into. The hits come from the shared combat
	/// package; this class chooses the numbers and draws the swing and the rush.
	/// </summary>
	[RequireComponent(typeof(Health), typeof(CharacterController))]
	public sealed class PlayerLord : MonoBehaviour
	{
		public ConquestGame Game;

		[Header("Sword (main action)")]
		public float AttackRange = 3.2f;
		public float AttackHalfAngle = 75f;
		public float AttackDamage = 20f;
		public float AttackCooldown = 0.55f;

		[Header("Charge (second action)")]
		public float ChargeDistance = 8f;
		public float ChargeSeconds = 0.28f;
		public float ChargeHitRadius = 2.2f;
		public float ChargeDamage = 35f;
		public float ChargeCooldown = 5f;

		public Health Health { get; private set; }

		/// <summary>0 while the Charge is recharging, 1 when it is ready.</summary>
		public float ChargeReady => Mathf.Clamp01(1f - (_nextCharge - Time.time) / ChargeCooldown);

		public bool IsCharging { get; private set; }

		CharacterController _controller;
		float _nextAttack;
		float _nextCharge;

		void Awake()
		{
			Health = GetComponent<Health>();
			_controller = GetComponent<CharacterController>();
		}

		void Update()
		{
			if (Game == null || Game.Phase != ConquestPhase.Playing || Game.Paused || !Health.IsAlive) return;

			var actions = EngineInput.Default.Player;
			if (actions.Primary.IsPressed()) TrySwing();
			if (actions.Secondary.IsPressed()) TryCharge();
		}

		/// <summary>Aim here instead of where the camera looks. Used by automated tests that play the game.</summary>
		public Vector3? AimOverride;

		/// <summary>Swings the sword if it is ready. Returns whether it swung.</summary>
		public bool TrySwing()
		{
			if (Time.time < _nextAttack || IsCharging) return false;
			Swing();
			return true;
		}

		/// <summary>Starts the Charge if it is ready. Returns whether it started.</summary>
		public bool TryCharge()
		{
			if (Time.time < _nextCharge || IsCharging) return false;
			StartCoroutine(Charge());
			return true;
		}

		Vector3 AimDirection()
		{
			if (AimOverride.HasValue) return AimOverride.Value;
			var camera = Camera.main;
			var forward = camera != null ? camera.transform.forward : transform.forward;
			forward.y = 0f;
			return forward.sqrMagnitude > 0.001f ? forward.normalized : transform.forward;
		}

		void Swing()
		{
			_nextAttack = Time.time + AttackCooldown;
			var direction = AimDirection();
			var origin = transform.position;
			CombatQueries.DamageInCone(origin, direction, AttackRange, AttackHalfAngle, Health.Team, new DamageInfo(AttackDamage, gameObject, origin, "sword"));

			Fx.Pulse(PrimitiveType.Cube, origin + Vector3.up * 1.1f + direction * 1.5f, Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f, 30f),
				new Vector3(0.3f, 0.06f, 0.5f), new Vector3(2.8f, 0.06f, 0.5f), new Color(1f, 0.95f, 0.7f), 0.14f);
		}

		IEnumerator Charge()
		{
			IsCharging = true;
			_nextCharge = Time.time + ChargeCooldown;

			var direction = AimDirection();
			var speed = ChargeDistance / ChargeSeconds;
			var hit = new HashSet<Health>();

			for (var t = 0f; t < ChargeSeconds; t += Time.deltaTime)
			{
				_controller.Move(direction * speed * Time.deltaTime);
				Fx.Pulse(PrimitiveType.Sphere, transform.position + Vector3.up * 0.9f, Quaternion.identity, Vector3.one * 0.7f, Vector3.one * 0.1f, new Color(0.4f, 0.8f, 1f), 0.2f);

				// Everyone the rush runs into is hit once.
				var found = CombatQueries.FindInRadius(transform.position, ChargeHitRadius, Health.Team);
				var targets = new List<Health>(found);
				foreach (var target in targets)
				{
					if (!hit.Add(target)) continue;
					target.ApplyDamage(new DamageInfo(ChargeDamage, gameObject, target.transform.position, "charge"));
				}
				yield return null;
			}
			IsCharging = false;
		}
	}
}
