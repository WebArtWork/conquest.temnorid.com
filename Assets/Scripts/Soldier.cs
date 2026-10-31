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
	/// <para>
	/// Online, the host runs the defenders as always and the other players only see them: on their machines a defender is
	/// <see cref="Remote"/> (no AI, no damage of its own) and shows what the host publishes.
	/// </para>
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

		/// <summary>The id this defender has online (the same on every machine), or -1 when playing alone.</summary>
		public int NetId { get; set; } = -1;

		/// <summary>True on a machine that only shows this defender: the host does its thinking.</summary>
		public bool Remote { get; private set; }

		/// <summary>True once the host said this picture is gone.</summary>
		public bool Gone => _gone;

		/// <summary>When this defender last struck at somebody, for showing it on other machines.</summary>
		public float LastAttackAt { get; private set; } = -100f;

		AiTarget _marker;
		float _nextStrike;
		bool _striking;
		bool _shownWindup;
		bool _shownAttack;
		bool _gone;
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

		/// <summary>
		/// Turns this defender into a picture of the host's: no thinking, no walking of its own. Call it while the object is still switched off,
		/// so its navigation agent never tries to stand on a NavMesh that this machine did not bake.
		/// </summary>
		public void MakeRemote(int netId)
		{
			Remote = true;
			NetId = netId;
			GetComponent<AiAgent>().enabled = false;
			GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
		}

		void Update()
		{
			if (Remote) return;
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
			LastAttackAt = Time.time;
			victim.ApplyDamage(new DamageInfo(MeleeDamage, gameObject, target.position, "melee"));
			Spark(target.position);
		}

		static void Spark(Vector3 at)
		{
			Fx.Pulse(PrimitiveType.Sphere, at + Vector3.up * 1f, Quaternion.identity, Vector3.one * 0.2f, Vector3.one * 0.9f, new Color(1f, 0.5f, 0.2f), 0.15f);
		}

		GameObject Warn(Vector3 center)
		{
			// The warning: a red disc on the ground grows to the size of the blow, so the player can see where it lands.
			return Fx.Pulse(PrimitiveType.Cylinder, new Vector3(center.x, 0.05f, center.z), Quaternion.identity,
				new Vector3(0.5f, 0.02f, 0.5f), new Vector3(StrikeRadius * 2f, 0.02f, StrikeRadius * 2f), new Color(0.95f, 0.1f, 0.05f), StrikeWarningSeconds);
		}

		IEnumerator Strike()
		{
			_striking = true;
			Agent.enabled = false;
			Agent.Nav.isStopped = true;

			var center = transform.position;
			StrikeCenter = center;
			_warning = Warn(center);

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
			if (Remote) return; // a picture only ends when the host says so (see Vanish)
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

		// ---- the picture of a defender on another machine ----

		/// <summary>Shows the host's numbers: health, the heavy strike's warning, a blow landing. Remote defenders only.</summary>
		public void ShowRemote(float health, bool windingUp, bool attacking)
		{
			if (!Remote || _gone) return;
			var wanted = Mathf.Clamp(health, 0.01f, Health.MaxHealth);
			if (wanted < Health.Current) Health.ApplyDamage(new DamageInfo(Health.Current - wanted, null, transform.position, "host"));
			else if (wanted > Health.Current) Health.Heal(wanted - Health.Current);

			if (windingUp && !_shownWindup && StrikeDamage > 0f)
			{
				StrikeCenter = transform.position;
				_warning = Warn(transform.position);
			}
			_shownWindup = windingUp;
			if (attacking && !_shownAttack) Spark(transform.position + transform.forward * 1.5f);
			_shownAttack = attacking;
		}

		/// <summary>The host says this defender is gone (it died): falls over and disappears like a defender that died here.</summary>
		public void Vanish()
		{
			if (!Remote || _gone) return;
			_gone = true;
			var body = GetComponent<ITKamianets.Engine.Net.RemoteBody>();
			if (body != null)
			{
				body.enabled = false;
				Destroy(body);
			}
			All.Remove(this);
			foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
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
