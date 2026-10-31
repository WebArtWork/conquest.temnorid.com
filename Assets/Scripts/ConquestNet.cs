using System.Collections.Generic;
using System.Globalization;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Temnorid.Conquest
{
	/// <summary>
	/// Makes the assault shared: up to four players storm the same village. The host runs everything that decides an outcome (the guards
	/// and the Lord with their AI, who is hit, the clock, victory and defeat) and publishes it about ten times a second as entities:
	/// <list type="bullet">
	/// <item>id 0, <see cref="KindState"/>: the clock (Value), guards left (Yaw), phase, how it was won, whether the Lord stands (State);</item>
	/// <item>ids from <see cref="SoldierBase"/>, <see cref="KindGuard"/> or <see cref="KindLord"/>: position, yaw, health (Value), the heavy strike
	/// warning and a blow landing (State bits 1 and 2). The id is 1000 plus the defender's index in the scene.</item>
	/// </list>
	/// Each player simulates their own hero and publishes its pose about fifteen times a second; the others show it (<see cref="RemoteHero"/>).
	/// On the host, other players' heroes are bodies the guards can see and hit; the damage a guard does to one is sent to its owner
	/// (<c>cq.dmg</c>), who applies it to their own hero. A blow a guest lands on a defender is an event to the host (<c>cq.hit</c>, defender,
	/// damage, weapon) and the host checks it (the player is alive, in reach, the damage is what that weapon does, not faster than the weapon can)
	/// before it hurts the defender. The host also decides the hall is entered (from the player's pose) and that the assault is lost
	/// (every hero down at once), and announces both with <c>cq.end</c>. A host that leaves ends the game for everybody.
	/// </summary>
	public sealed class ConquestNet : MonoBehaviour
	{
		public const int KindState = 300;
		public const int KindGuard = 1;
		public const int KindLord = 2;
		public const int StateId = 0;
		public const int SoldierBase = 1000;

		public const string HitEvent = "cq.hit";
		public const string DamageEvent = "cq.dmg";
		public const string EndEvent = "cq.end";

		const float PublishEvery = 0.1f;
		const float PoseEvery = 1f / 15f;
		const float ReachSlack = 3.5f; // the pose is a few updates old and eased

		ConquestGame _game;
		NetSession _net;
		PlayerLord _lord;
		bool _ready;
		float _nextPublish;
		float _nextPose;

		readonly List<NetEntity> _list = new List<NetEntity>();
		readonly Dictionary<ulong, RemoteHero> _heroes = new Dictionary<ulong, RemoteHero>();
		readonly Dictionary<string, float> _lastHit = new Dictionary<string, float>();

		public bool IsClient => _net != null && _net.IsClient;
		public bool IsHost => _net != null && _net.IsHost;
		public string LocalName => _net != null ? _net.LocalName : "";

		/// <summary>Everyone else's hero, by player id.</summary>
		public IReadOnlyDictionary<ulong, RemoteHero> Heroes => _heroes;

		/// <summary>A short line about who came or left, for the screen, and when it was set.</summary>
		public string Note { get; private set; } = "";

		public float NoteAt { get; private set; } = -100f;

		/// <summary>Defenders hit on the host by a guest, counted for tests.</summary>
		public int HitsAccepted { get; private set; }

		public int HitsRefused { get; private set; }

		public void Init(ConquestGame game)
		{
			if (_ready) return;
			_ready = true;
			_game = game;
			_net = NetSession.Instance;
			_lord = game.PlayerHealth.GetComponent<PlayerLord>();

			_net.On(HitEvent, OnHit);
			_net.On(DamageEvent, OnDamage);
			_net.On(EndEvent, OnEnd);
			_net.Disconnected += OnDisconnected;
			_net.EntityUpdated += OnEntity;
			_net.EntityGone += OnGone;
			_net.PlayerLeft += OnPlayerLeft;
			_net.GameStarted += OnGameStarted;
			Soldier.Killed += OnKilled;

		}

		// Everybody starts abreast outside the gate, in roster order (the host first). Done in Start: the character's own Awake has run by then.
		void Start()
		{
			if (!_ready) return;
			var slot = 0;
			for (var i = 0; i < _net.Players.Count; i++)
				if (_net.Players[i].Id == _net.LocalId) slot = i;
			var spread = (slot - (_net.Players.Count - 1) * 0.5f) * 2.5f;
			var spawn = _game.SpawnPoint + new Vector3(spread, 0f, 0f);
			_game.SpawnPoint = spawn;
			var motor = _game.PlayerHealth.GetComponent<CharacterMotor>();
			if (motor != null) motor.Teleport(spawn, 0f);
		}

		void OnDestroy()
		{
			if (!_ready) return;
			_net.Off(HitEvent, OnHit);
			_net.Off(DamageEvent, OnDamage);
			_net.Off(EndEvent, OnEnd);
			_net.Disconnected -= OnDisconnected;
			_net.EntityUpdated -= OnEntity;
			_net.EntityGone -= OnGone;
			_net.PlayerLeft -= OnPlayerLeft;
			_net.GameStarted -= OnGameStarted;
			Soldier.Killed -= OnKilled;
		}

		// ---- what a guest asks ----

		/// <summary>A guest's blow landed on a defender (which is only a picture here): tells the host. True when the request was sent.</summary>
		public bool RequestHit(Health target, float damage, string type)
		{
			if (!IsClient || target == null) return false;
			var soldier = target.GetComponentInParent<Soldier>();
			if (soldier == null || !soldier.Remote || soldier.NetId < SoldierBase) return false;
			_net.Send(HitEvent, new ConquestHit(soldier.NetId - SoldierBase, damage, type).Write(), true);
			return true;
		}

		// ---- the host checks and applies ----

		void OnHit(ulong sender, string payload)
		{
			if (!_net.IsHost || _game.Phase != ConquestPhase.Playing) return;
			if (!ConquestHit.TryRead(payload, out var hit) || !Accept(sender, hit, out var soldier, out var source))
			{
				HitsRefused++;
				return;
			}
			HitsAccepted++;
			soldier.Health.ApplyDamage(new DamageInfo(hit.Damage, source, soldier.transform.position, hit.Type));
		}

		bool Accept(ulong sender, in ConquestHit hit, out Soldier soldier, out GameObject source)
		{
			soldier = null;
			source = null;
			if (_net.Find(sender) == null || _lord == null) return false;
			if (hit.Defender >= _game.StartingSoldiers.Length) return false;
			soldier = _game.StartingSoldiers[hit.Defender];
			if (soldier == null || soldier.Remote || !soldier.Health.IsAlive) return false;

			var sword = hit.Type == ConquestHit.Sword;
			var cap = sword ? _lord.AttackDamage : _lord.ChargeDamage;
			if (hit.Damage > cap + 0.01f) return false;

			// The player must be alive, and close enough to that defender for that weapon.
			if (!_net.TryGetPose(sender, out var pose) || (pose.State & RemoteHero.AliveBit) == 0) return false;
			var reach = (sword ? _lord.AttackRange : _lord.ChargeDistance + _lord.ChargeHitRadius) + ReachSlack;
			var apart = soldier.transform.position - pose.Position;
			apart.y = 0f;
			if (apart.magnitude > reach) return false;

			// Not faster than the weapon can be used.
			var key = sender + "|" + hit.Defender + "|" + hit.Type;
			var gap = sword ? _lord.AttackCooldown * 0.5f : _lord.ChargeCooldown * 0.5f;
			if (_lastHit.TryGetValue(key, out var last) && Time.unscaledTime - last < gap) return false;
			_lastHit[key] = Time.unscaledTime;

			if (_heroes.TryGetValue(sender, out var hero)) source = hero.gameObject;
			return true;
		}

		// ---- damage to a hero ----

		// Host: a guard hurt another player's hero here; its owner applies it to the hero they play.
		void OnHeroDamaged(RemoteHero hero, DamageInfo info)
		{
			if (!_net.IsHost) return;
			_net.SendTo(hero.Owner, DamageEvent, info.Amount.ToString("R", CultureInfo.InvariantCulture));
		}

		void OnDamage(ulong sender, string payload)
		{
			if (!_net.IsClient || sender != 0UL || _game.Phase != ConquestPhase.Playing || _game.Fallen) return;
			if (!float.TryParse(payload, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || amount <= 0f || amount > 100f) return;
			var health = _game.PlayerHealth;
			health.ApplyDamage(new DamageInfo(amount, null, health.transform.position, "guard"));
		}

		// ---- how it ends ----

		/// <summary>Host: tells everybody how the assault ended.</summary>
		public void AnnounceEnd()
		{
			if (!_net.IsHost) return;
			_net.Send(EndEvent, ((int)_game.Phase).ToString(CultureInfo.InvariantCulture) + "|" + (int)_game.Won + "|" + _game.RunTime.ToString("R", CultureInfo.InvariantCulture));
		}

		void OnEnd(ulong sender, string payload)
		{
			if (!_net.IsClient) return;
			var parts = payload.Split('|');
			if (parts.Length != 3 || !int.TryParse(parts[0], out var phase) || !int.TryParse(parts[1], out var kind)) return;
			if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var time)) return;
			_game.MirrorEnd((ConquestPhase)phase, (VictoryKind)kind, time);
		}

		/// <summary>Host: everybody loads the assault again together.</summary>
		public void RestartForAll()
		{
			if (_net.IsHost) _net.StartGame(SceneManager.GetActiveScene().name);
		}

		void OnGameStarted(string scene)
		{
			Time.timeScale = 1f;
			SceneManager.LoadScene(scene);
		}

		void OnDisconnected(string reason)
		{
			// The host closed the game or the connection dropped: nothing left to storm together.
			ShellFlow.QuitToMenu();
		}

		void OnPlayerLeft(NetPlayer player)
		{
			Note = player.Name + " left the assault.";
			NoteAt = Time.unscaledTime;
			if (_heroes.TryGetValue(player.Id, out var hero) && hero != null) Destroy(hero.gameObject);
			_heroes.Remove(player.Id);
		}

		void OnKilled(Soldier soldier)
		{
			if (_net.IsHost && !soldier.Remote && soldier.NetId >= SoldierBase) _net.RemoveEntity(soldier.NetId);
		}

		// ---- the host publishes, everybody shows ----

		void Update()
		{
			if (!_ready || _net == null || !_net.IsOnline) return;

			if (Time.unscaledTime >= _nextPose)
			{
				_nextPose = Time.unscaledTime + PoseEvery;
				_net.PublishPose(MyPose());
			}
			ShowHeroes();

			if (!_net.IsHost) return;
			if (Time.unscaledTime >= _nextPublish)
			{
				_nextPublish = Time.unscaledTime + PublishEvery;
				Publish();
			}
			if (_game.Phase == ConquestPhase.Playing)
			{
				CheckHall();
				CheckDefeat();
			}
		}

		NetPose MyPose()
		{
			var hero = _game.PlayerHealth;
			var motor = hero.GetComponent<CharacterMotor>();
			var velocity = motor != null ? motor.Velocity : Vector3.zero;
			velocity.y = 0f;
			var alive = hero.IsAlive && !_game.Fallen;
			var state = (alive ? RemoteHero.AliveBit : 0)
				| (_lord != null && _lord.IsCharging ? RemoteHero.ChargingBit : 0)
				| ((_lord != null ? _lord.SwingCount : 0) << RemoteHero.SwingShift);
			return new NetPose
			{
				Position = hero.transform.position,
				Yaw = hero.transform.eulerAngles.y,
				Speed = velocity.magnitude,
				State = state,
				A = hero.Current,
				B = hero.MaxHealth,
			};
		}

		void ShowHeroes()
		{
			foreach (var player in _net.Players)
			{
				if (player.Id == _net.LocalId) continue;
				if (!_net.TryGetPose(player.Id, out var pose)) continue;
				if (!_heroes.TryGetValue(player.Id, out var hero) || hero == null)
				{
					hero = RemoteHero.Create(player, ShellConfig.Current.ColourOf(player.Colour), _net.IsHost, OnHeroDamaged);
					_heroes[player.Id] = hero;
				}
				hero.Show(pose);
			}
		}

		void Publish()
		{
			_list.Clear();
			var lordAlive = _game.EnemyLord != null && _game.EnemyLord.Health.IsAlive;
			var heroes = 0;
			if (_game.PlayerHealth.IsAlive && !_game.Fallen) heroes++;
			foreach (var hero in _heroes.Values) if (hero != null && hero.Alive) heroes++;
			_list.Add(new NetEntity
			{
				Id = StateId,
				Kind = KindState,
				Value = _game.RunTime,
				Yaw = _game.GuardsLeft,
				State = (int)_game.Phase | ((int)_game.Won << 2) | (lordAlive ? 1 << 4 : 0) | (heroes << 5),
			});

			var soldiers = _game.StartingSoldiers;
			for (var i = 0; i < soldiers.Length; i++)
			{
				var soldier = soldiers[i];
				if (soldier == null || soldier.Remote || !soldier.Health.IsAlive) continue;
				_list.Add(new NetEntity
				{
					Id = SoldierBase + i,
					Kind = soldier.IsLord ? KindLord : KindGuard,
					Position = soldier.transform.position,
					Yaw = soldier.transform.eulerAngles.y,
					Value = soldier.Health.Current,
					State = (soldier.IsWindingUp ? 1 : 0) | (Time.time - soldier.LastAttackAt < 0.3f ? 2 : 0),
				});
			}
			_net.PublishEntities(_list);
		}

		// Host: another player standing inside the hall doorway takes the village, as the host's own hero does.
		void CheckHall()
		{
			var hall = HallTrigger.Instance;
			if (hall == null) return;
			var area = hall.Area;
			foreach (var hero in _heroes.Values)
			{
				if (hero == null || !hero.Alive) continue;
				if (area.Contains(hero.transform.position + Vector3.up)) { _game.Win(VictoryKind.TookHall); return; }
			}
		}

		// Host: the assault is lost when every hero is down at once. One that stands keeps it going, and a fallen one comes back.
		void CheckDefeat()
		{
			if (!_game.Fallen) return;
			foreach (var player in _net.Players)
			{
				if (player.Id == _net.LocalId) continue;
				if (!_net.TryGetPose(player.Id, out var pose) || (pose.State & RemoteHero.AliveBit) != 0) return;
			}
			_game.Defeat();
		}

		// ---- a guest shows ----

		void OnEntity(NetEntity entity)
		{
			if (_game == null || !_net.IsClient) return;
			if (entity.Id == StateId)
			{
				var phase = (ConquestPhase)(entity.State & 3);
				if (phase != ConquestPhase.Playing) _game.MirrorEnd(phase, (VictoryKind)((entity.State >> 2) & 3), entity.Value);
				else _game.MirrorProgress(entity.Value, Mathf.RoundToInt(entity.Yaw));
				return;
			}

			var index = entity.Id - SoldierBase;
			if (index < 0 || index >= _game.StartingSoldiers.Length) return;
			var soldier = _game.StartingSoldiers[index];
			if (soldier == null || !soldier.Remote || soldier.Gone) return;
			var body = soldier.GetComponent<RemoteBody>();
			if (body == null) body = soldier.gameObject.AddComponent<RemoteBody>();
			body.Apply(entity);
			soldier.ShowRemote(entity.Value, (entity.State & 1) != 0, (entity.State & 2) != 0);
		}

		void OnGone(int id)
		{
			var index = id - SoldierBase;
			if (index < 0 || _game == null || index >= _game.StartingSoldiers.Length) return;
			var soldier = _game.StartingSoldiers[index];
			if (soldier != null) soldier.Vanish();
		}
	}
}
