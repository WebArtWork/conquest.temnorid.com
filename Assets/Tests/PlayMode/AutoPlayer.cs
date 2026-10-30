using UnityEngine;
using UnityEngine.AI;

namespace Temnorid.Conquest.Tests
{
	/// <summary>How the bot tries to take the village.</summary>
	public enum BotPlan
	{
		/// <summary>Fight everyone, nearest first, until the enemy Lord falls.</summary>
		Fighter,
		/// <summary>Ignore the guards and run for the hall.</summary>
		Runner,
	}

	/// <summary>
	/// A test-only player that plays the assault the way a decent person would, so the difficulty can be measured
	/// instead of guessed. The fighter swings, charges to close a gap and steps out of the Lord's heavy strike after a
	/// human-sized reaction time; the runner heads straight for the hall door, charging when it is free.
	/// </summary>
	public sealed class AutoPlayer : MonoBehaviour
	{
		public BotPlan Plan;
		public bool Dodges = true;
		public float Speed = 5f;
		public float ReactionSeconds = 0.4f;

		ConquestGame _game;
		PlayerLord _lord;
		CharacterController _controller;
		float _windupSeenAt = -1f;
		NavMeshPath _path;
		float _repathAt;
		Vector3 _corner;

		void Start()
		{
			_game = ConquestGame.Instance;
			_lord = GetComponent<PlayerLord>();
			_controller = GetComponent<CharacterController>();
			_path = new NavMeshPath(); // not allowed in a field initializer
		}

		void Update()
		{
			if (_game == null || _game.Phase != ConquestPhase.Playing || !_lord.Health.IsAlive) return;
			var position = transform.position;

			Soldier windup = null;
			foreach (var soldier in Soldier.All) if (soldier.IsWindingUp) windup = soldier;
			if (windup == null) _windupSeenAt = -1f;
			else if (Dodges)
			{
				if (_windupSeenAt < 0f) _windupSeenAt = Time.time;
				var away = Flat(position - windup.StrikeCenter);
				if (Time.time - _windupSeenAt >= ReactionSeconds && away.magnitude < windup.StrikeRadius + 0.8f)
				{
					Walk(away.sqrMagnitude < 0.01f ? Vector3.back : away.normalized, Speed);
					return;
				}
			}

			if (Plan == BotPlan.Runner)
			{
				// Straight up the road to the hall door and in.
				var goal = new Vector3(0f, 0f, 40f);
				var heading = Flat(goal - position).normalized;
				_lord.AimOverride = heading;
				_lord.TryCharge();

				// Someone in the way: cut them down, then carry on.
				Soldier blocker = null;
				var nearest = 2.6f;
				foreach (var soldier in Soldier.All)
				{
					var offset = Flat(soldier.transform.position - position);
					if (offset.magnitude < nearest && Vector3.Dot(offset.normalized, heading) > 0.3f)
					{
						nearest = offset.magnitude;
						blocker = soldier;
					}
				}
				if (blocker != null && nearest < 1.7f)
				{
					_lord.AimOverride = Flat(blocker.transform.position - position).normalized;
					_lord.TrySwing();
					return;
				}
				Walk(heading, Speed * 1.3f);
				return;
			}

			Soldier target = null;
			var best = float.MaxValue;
			foreach (var soldier in Soldier.All)
			{
				var distance = Flat(soldier.transform.position - position).magnitude;
				if (distance < best)
				{
					best = distance;
					target = soldier;
				}
			}
			if (target == null) return;

			var direction = Flat(target.transform.position - position);
			_lord.AimOverride = direction.normalized;
			if (best > 4.5f && best < 9f) _lord.TryCharge(); // close a gap with the rush
			if (best > 2.3f)
			{
				WalkTo(target.transform.position, Speed);
				return;
			}
			_lord.TrySwing();
		}

		/// <summary>Walks along the NavMesh path to a point, as a person finds their way round corners.</summary>
		void WalkTo(Vector3 destination, float speed)
		{
			if (Time.time >= _repathAt)
			{
				_repathAt = Time.time + 0.25f;
				_corner = destination;
				if (NavMesh.SamplePosition(transform.position, out var from, 3f, NavMesh.AllAreas)
					&& NavMesh.SamplePosition(destination, out var to, 3f, NavMesh.AllAreas)
					&& NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _path)
					&& _path.corners.Length > 1)
				{
					_corner = _path.corners[1];
					if (Flat(_corner - transform.position).magnitude < 0.6f && _path.corners.Length > 2) _corner = _path.corners[2];
				}
			}
			var heading = Flat(_corner - transform.position);
			if (heading.sqrMagnitude > 0.01f) Walk(heading.normalized, speed);
		}

		void Walk(Vector3 direction, float speed)
		{
			_controller.Move(direction * (speed * Time.deltaTime) + Vector3.down * (2f * Time.deltaTime));
		}

		static Vector3 Flat(Vector3 v)
		{
			v.y = 0f;
			return v;
		}
	}
}
