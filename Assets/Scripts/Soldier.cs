using ITKamianets.Engine.Feedback;
using System;
using System.Collections;
using System.Collections.Generic;
using ITKamianets.Engine.AI;
using ITKamianets.Engine.Combat;
using UnityEngine;

namespace Temnorid.Conquest
{
	/// <summary>
	/// A defender of the village: a guard, or the enemy Lord. It is built on the shared bot and combat
	/// packages: <see cref="AiAgent"/> finds and chases the player and asks for attacks, <see cref="Health"/> keeps
	/// its hit points. This class says what an attack does, and, for the Lord, the heavy strike with its warning on the floor.
	/// </summary>
	[RequireComponent(typeof(AiAgent), typeof(Health))]
	public sealed class Soldier : MonoBehaviour
	{
		public static readonly List<Soldier> All = new List<Soldier>();

		/// <summary>Raised when a defender dies.</summary>
		public static event Action<Soldier> Killed;

		public string DisplayName = "Guard";
		public bool IsLord;
		public float MeleeDamage = 9f;

		[Header("Heavy strike (zero damage means none)")]
		public float StrikeDamage;
		public float StrikeRadius = 4.5f;
		public float StrikeInterval = 7f;
		public float StrikeWarningSeconds = 1.2f;
		public float StrikeTriggerDistance = 6f;

		public Health Health { get; private set; }
		public AiAgent Agent { get; private set; }

		/// <summary>True while the heavy strike's warning is showing on the ground.</summary>
		public bool IsWindingUp => _striking;

		/// <summary>Where the strike will land, while <see cref="IsWindingUp"/>.</summary>
		public Vector3 StrikeCenter { get; private set; }

		AiTarget _marker;
		float _nextStrike;
		bool _striking;
		GameObject _warning;

		void Awake()
		{
			Health = GetComponent<Health>();
			Agent = GetComponent<AiAgent>();
			_marker = GetComponent<AiTarget>();
			Agent.AttackRequested += OnAttackRequested;
			Health.Died += OnDied;
			_nextStrike = Time.time + StrikeInterval * 0.5f;
		}

		void OnEnable() => All.Add(this);
		void OnDisable() => All.Remove(this);

		void Update()
		{
			if (StrikeDamage <= 0f || _striking || !Health.IsAlive) return;
			if (Time.time < _nextStrike) return;
			if (Agent.State != AiState.Chase && Agent.State != AiState.Attack) return;
			if (Agent.Target == null) return;
			if (Vector3.Distance(Agent.Target.position, transform.position) <= StrikeTriggerDistance) StartCoroutine(Strike());
		}

		void OnAttackRequested(AiAgent agent, Transform target)
		{
			if (_striking) return;
			var victim = target.GetComponentInParent<Health>();
			if (victim == null) return;
			victim.ApplyDamage(new DamageInfo(MeleeDamage, gameObject, target.position, "melee"));
			Fx.Pulse(PrimitiveType.Sphere, target.position + Vector3.up * 1f, Quaternion.identity, Vector3.one * 0.2f, Vector3.one * 0.9f, new Color(1f, 0.5f, 0.2f), 0.15f);
		}

		IEnumerator Strike()
		{
			_striking = true;
			Agent.enabled = false;
			Agent.Nav.isStopped = true;

			// The warning: a red disc on the ground grows to the size of the blow, so the player can see where it lands.
			var center = transform.position;
			StrikeCenter = center;
			_warning = Fx.Pulse(PrimitiveType.Cylinder, new Vector3(center.x, 0.05f, center.z), Quaternion.identity,
				new Vector3(0.5f, 0.02f, 0.5f), new Vector3(StrikeRadius * 2f, 0.02f, StrikeRadius * 2f), new Color(0.95f, 0.1f, 0.05f), StrikeWarningSeconds);

			yield return new WaitForSeconds(StrikeWarningSeconds);

			CombatQueries.DamageInRadius(center, StrikeRadius, Health.Team, new DamageInfo(StrikeDamage, gameObject, center, "strike"));
			Fx.Pulse(PrimitiveType.Cylinder, new Vector3(center.x, 0.1f, center.z), Quaternion.identity,
				new Vector3(StrikeRadius * 2f, 0.1f, StrikeRadius * 2f), new Vector3(StrikeRadius * 2.4f, 0.02f, StrikeRadius * 2.4f), new Color(1f, 0.85f, 0.4f), 0.25f);

			_nextStrike = Time.time + StrikeInterval;
			Agent.enabled = true;
			Agent.Nav.isStopped = false;
			_striking = false;
		}

		void OnDied(Health health, DamageInfo info)
		{
			StopAllCoroutines();
			if (_warning != null) Destroy(_warning);
			All.Remove(this);

			Agent.Stop();
			Agent.Nav.enabled = false; // or the agent keeps steering the body while it falls
			if (_marker != null) _marker.Alive = false;
			foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;

			Killed?.Invoke(this);
			StartCoroutine(Fall());
		}

		IEnumerator Fall()
		{
			var start = transform.rotation;
			var end = start * Quaternion.Euler(90f, 0f, 0f);
			for (var t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
			{
				transform.rotation = Quaternion.Slerp(start, end, t);
				yield return null;
			}
			yield return new WaitForSeconds(2.5f);
			Destroy(gameObject);
		}
	}
}
