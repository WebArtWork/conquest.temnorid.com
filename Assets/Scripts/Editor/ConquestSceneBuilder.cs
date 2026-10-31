using ITKamianets.Engine.Graybox.EditorTools;
using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Graybox;
using ITKamianets.Engine.AI;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.CharacterView;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Shell.Editor;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Temnorid.Village;

namespace Temnorid.Conquest.EditorTools
{
	/// <summary>
	/// Builds the whole assault from Unity primitives: open ground, a village behind a wall with a gate, four
	/// houses, the hall at the far end, guards and the enemy Lord, the player's Lord, camera, light and screens.
	/// Run it from the menu (Temnorid > Conquest > Build Scene) or the command line
	/// (<c>-executeMethod Temnorid.Conquest.EditorTools.ConquestSceneBuilder.Build</c>); it rebuilds
	/// <c>Assets/Scenes/Main.unity</c> from scratch each time, so do not edit that scene by hand.
	/// The NavMesh is baked when the game starts, from the ground and wall colliders.
	/// </summary>
	public static class ConquestSceneBuilder
	{
		const string ScenePath = "Assets/Scenes/Main.unity";
		const string VillageScenePath = "Assets/Scenes/Village.unity";
		public const string Version = "0.3.0";
		const string MaterialDir = "Assets/Materials";

		// The village is a rectangle behind a wall: x from -24 to 24, z from 0 (the gate) to 48.
		const float VillageHalfWidth = 24f;
		const float VillageDepth = 48f;
		const float GateWidth = 6f;

		[MenuItem("Temnorid/Conquest/Build Scene")]
		public static void Build()
		{
			ShellSceneKit.ApplyPlatformSettings("Temnorid", "Temnorid Conquest", "com.itkamianets.conquest", Version);
			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.itkamianets.conquest");
			PlayerSettings.runInBackground = true;
			PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
			RenderSettings.ambientLight = new Color(0.6f, 0.64f, 0.72f);

			var world = BuildWorld(out var surface);
			BuildHall(world);
			var soldiers = BuildSoldiers(out var lord);
			var player = BuildPlayer();
			BuildCameraAndLight(player.GetComponent<CharacterMotor>());

			var gameObject = new GameObject("Game");
			var game = gameObject.AddComponent<ConquestGame>();
			game.PlayerHealth = player.GetComponent<Health>();
			game.Surface = surface;
			game.StartingSoldiers = soldiers;
			game.EnemyLord = lord;
			player.GetComponent<PlayerLord>().Game = game;

			var hud = gameObject.AddComponent<ConquestHud>();
			hud.Game = game;
			hud.Lord = player.GetComponent<PlayerLord>();

			EditorSceneManager.SaveScene(scene, ScenePath);
			BuildVillageScene();
			ShellSceneKit.BuildBoot();
			ShellSceneKit.BuildMenu();
			ShellSceneKit.SetBuildOrder(VillageScenePath, ScenePath); // Boot, Menu, the home village, then the assault
			AssetDatabase.SaveAssets();
			Debug.Log("[Conquest] Scene built: " + ScenePath + " (" + soldiers.Length + " defenders)");
		}

		// ---- the home village hub ----

		/// <summary>
		/// <c>Assets/Scenes/Village.unity</c>: the village view (drawn at run time from the player's Lords save or the standard village),
		/// a wide safety ground, the hero, the camera and the game objects. The gate is made when the village is drawn.
		/// </summary>
		static void BuildVillageScene()
		{
			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
			RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.74f);

			var world = new GameObject("World").transform;
			Prim(PrimitiveType.Cube, "SafetyGround", world, new Vector3(0f, -0.8f, 0f), new Vector3(600f, 1f, 600f), Mat("Grass", new Color(0.33f, 0.55f, 0.28f)), true);

			var view = new GameObject("Village").AddComponent<VillageView>();
			view.LitTemplate = Mat("VillageLit", Color.white);
			view.TextMaterial = GrayboxBuilder.WorldTextMaterial(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), MaterialDir);

			var hero = BuildPlayer();
			hero.name = "Hero";
			Object.DestroyImmediate(hero.GetComponent<PlayerLord>());
			Object.DestroyImmediate(hero.GetComponent<AiTarget>());
			Object.DestroyImmediate(hero.GetComponent<Health>());
			hero.AddComponent<HubHero>();
			BuildCameraAndLight(hero.GetComponent<CharacterMotor>());

			var gameObject = new GameObject("Game");
			var game = gameObject.AddComponent<VillageGame>();
			game.View = view;
			game.Hero = hero.GetComponent<CharacterMotor>();
			gameObject.AddComponent<VillageHud>().Game = game;

			EditorSceneManager.SaveScene(scene, VillageScenePath);
			Debug.Log("[Conquest] Scene built: " + VillageScenePath);
		}

		// ---- the village ----

		static Transform BuildWorld(out NavMeshSurface surface)
		{
			var grass = Mat("Grass", new Color(0.33f, 0.55f, 0.28f));
			var dirt = Mat("Dirt", new Color(0.58f, 0.44f, 0.3f));
			var wood = Mat("Palisade", new Color(0.45f, 0.3f, 0.18f));
			var plaster = Mat("Plaster", new Color(0.86f, 0.8f, 0.66f));
			var roof = Mat("Roof", new Color(0.55f, 0.22f, 0.18f));
			var door = Mat("Door", new Color(0.25f, 0.16f, 0.1f));

			var root = new GameObject("World");
			surface = root.AddComponent<NavMeshSurface>();
			surface.collectObjects = CollectObjects.Children;
			surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
			var t = root.transform;

			Box(t, "Ground", new Vector3(0f, -0.5f, 14f), new Vector3(150f, 1f, 150f), grass);
			Prim(PrimitiveType.Cube, "Road", t, new Vector3(0f, 0.02f, 8f), new Vector3(4f, 0.05f, 100f), dirt);

			// The palisade: four walls, with the gate gap in the south one.
			var wall = new GameObject("Palisade").transform;
			wall.SetParent(t, false);
			wall.gameObject.AddComponent<AssetIdTag>().Id = "buildings/palisade";
			const float height = 3.5f;
			var sideLength = (VillageHalfWidth * 2f - GateWidth) * 0.5f;
			Box(wall, "SouthWest", new Vector3(-(GateWidth + sideLength) * 0.5f, height * 0.5f, 0f), new Vector3(sideLength, height, 1f), wood);
			Box(wall, "SouthEast", new Vector3((GateWidth + sideLength) * 0.5f, height * 0.5f, 0f), new Vector3(sideLength, height, 1f), wood);
			Box(wall, "North", new Vector3(0f, height * 0.5f, VillageDepth), new Vector3(VillageHalfWidth * 2f + 1f, height, 1f), wood);
			Box(wall, "West", new Vector3(-VillageHalfWidth, height * 0.5f, VillageDepth * 0.5f), new Vector3(1f, height, VillageDepth), wood);
			Box(wall, "East", new Vector3(VillageHalfWidth, height * 0.5f, VillageDepth * 0.5f), new Vector3(1f, height, VillageDepth), wood);

			// Gate towers and a banner so the way in is easy to find.
			var red = Mat("Banner", new Color(0.75f, 0.12f, 0.12f));
			Box(wall, "TowerL", new Vector3(-GateWidth * 0.5f - 1f, 3f, 0f), new Vector3(2f, 6f, 2f), wood);
			Box(wall, "TowerR", new Vector3(GateWidth * 0.5f + 1f, 3f, 0f), new Vector3(2f, 6f, 2f), wood);
			Prim(PrimitiveType.Cube, "GateBanner", wall, new Vector3(0f, 5.2f, 0f), new Vector3(GateWidth + 0.5f, 1.4f, 0.15f), red);

			House(t, "House_1", new Vector3(-14f, 0f, 10f), 8f, 7f, plaster, roof, door);
			House(t, "House_2", new Vector3(14f, 0f, 10f), 8f, 7f, plaster, roof, door);
			House(t, "House_3", new Vector3(-14f, 0f, 29f), 8f, 7f, plaster, roof, door);
			House(t, "House_4", new Vector3(14f, 0f, 29f), 8f, 7f, plaster, roof, door);
			return t;
		}

		static void House(Transform parent, string name, Vector3 center, float width, float depth, Material wall, Material roof, Material door)
		{
			var house = new GameObject(name).transform;
			house.SetParent(parent, false);
			house.position = center;
			house.gameObject.AddComponent<AssetIdTag>().Id = "buildings/house";
			Box(house, "Walls", new Vector3(0f, 2f, 0f), new Vector3(width, 4f, depth), wall);
			var left = Prim(PrimitiveType.Cube, "RoofLeft", house, new Vector3(-width * 0.25f, 4.7f, 0f), new Vector3(width * 0.62f, 0.3f, depth + 0.8f), roof);
			left.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
			var right = Prim(PrimitiveType.Cube, "RoofRight", house, new Vector3(width * 0.25f, 4.7f, 0f), new Vector3(width * 0.62f, 0.3f, depth + 0.8f), roof);
			right.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
			Prim(PrimitiveType.Cube, "Door", house, new Vector3(0f, 1.1f, -depth * 0.5f - 0.05f), new Vector3(1.4f, 2.2f, 0.1f), door);
		}

		/// <summary>The hall: stone walls with a doorway on the south side and a trigger just inside it.</summary>
		static void BuildHall(Transform world)
		{
			var stone = Mat("HallStone", new Color(0.5f, 0.48f, 0.52f));
			var red = Mat("Banner", new Color(0.75f, 0.12f, 0.12f));
			var gold = Mat("Gold", new Color(1f, 0.82f, 0.25f));
			var floor = Mat("HallFloor", new Color(0.36f, 0.3f, 0.34f));

			var hall = new GameObject("Hall").transform;
			hall.SetParent(world, false);
			hall.gameObject.AddComponent<AssetIdTag>().Id = "buildings/hall";

			const float z0 = 36f, z1 = 46f, halfWidth = 8f, height = 6f, gap = 4f;
			Prim(PrimitiveType.Cube, "Floor", hall, new Vector3(0f, 0.03f, (z0 + z1) * 0.5f), new Vector3(halfWidth * 2f, 0.05f, z1 - z0), floor);
			Box(hall, "WallWest", new Vector3(-halfWidth, height * 0.5f, (z0 + z1) * 0.5f), new Vector3(1f, height, z1 - z0 + 1f), stone);
			Box(hall, "WallEast", new Vector3(halfWidth, height * 0.5f, (z0 + z1) * 0.5f), new Vector3(1f, height, z1 - z0 + 1f), stone);
			Box(hall, "WallNorth", new Vector3(0f, height * 0.5f, z1), new Vector3(halfWidth * 2f + 1f, height, 1f), stone);
			var side = (halfWidth * 2f - gap) * 0.5f;
			Box(hall, "WallSouthA", new Vector3(-(gap + side) * 0.5f, height * 0.5f, z0), new Vector3(side + 0.5f, height, 1f), stone);
			Box(hall, "WallSouthB", new Vector3((gap + side) * 0.5f, height * 0.5f, z0), new Vector3(side + 0.5f, height, 1f), stone);
			Prim(PrimitiveType.Cube, "Lintel", hall, new Vector3(0f, height - 0.8f, z0), new Vector3(gap + 1f, 1.6f, 1f), stone);

			// Inside: a throne, banners, braziers.
			Prim(PrimitiveType.Cube, "ThroneBase", hall, new Vector3(0f, 0.4f, z1 - 1.5f), new Vector3(2.4f, 0.8f, 1.6f), stone);
			Prim(PrimitiveType.Cube, "ThroneBack", hall, new Vector3(0f, 1.6f, z1 - 1.9f), new Vector3(1.8f, 2.4f, 0.4f), gold);
			Prim(PrimitiveType.Cube, "BannerL", hall, new Vector3(-halfWidth + 0.6f, 3.2f, 41f), new Vector3(0.1f, 3f, 1.6f), red);
			Prim(PrimitiveType.Cube, "BannerR", hall, new Vector3(halfWidth - 0.6f, 3.2f, 41f), new Vector3(0.1f, 3f, 1.6f), red);
			Prim(PrimitiveType.Cube, "DoorBanner", hall, new Vector3(0f, height + 0.7f, z0 - 0.7f), new Vector3(gap + 2f, 1.2f, 0.12f), red);

			// The trigger: the first three metres inside the doorway.
			var trigger = new GameObject("HallTrigger");
			trigger.transform.SetParent(hall, false);
			trigger.transform.position = new Vector3(0f, 1.5f, z0 + 2.5f);
			var box = trigger.AddComponent<BoxCollider>();
			box.isTrigger = true;
			box.size = new Vector3(gap + 1f, 3f, 3f);
			trigger.AddComponent<HallTrigger>();

			var light = new GameObject("HallLight");
			light.transform.SetParent(hall, false);
			light.transform.position = new Vector3(0f, 4f, 41f);
			var lamp = light.AddComponent<Light>();
			lamp.type = LightType.Point;
			lamp.range = 14f;
			lamp.intensity = 2.5f;
			lamp.color = new Color(1f, 0.75f, 0.5f);
			lamp.shadows = LightShadows.None;
		}

		// ---- the defenders ----

		static Soldier[] BuildSoldiers(out Soldier lord)
		{
			var root = new GameObject("Defenders").transform;
			var armour = Mat("GuardRed", new Color(0.6f, 0.15f, 0.15f));
			var steel = Mat("Steel", new Color(0.75f, 0.78f, 0.85f));
			var wood = Mat("SpearWood", new Color(0.45f, 0.3f, 0.18f));

			var list = new System.Collections.Generic.List<Soldier>
			{
				Guard(root, "Guard_1", new Vector3(-2.5f, 0f, 13f), armour, steel, wood, null),
				Guard(root, "Guard_2", new Vector3(2.5f, 0f, 19f), armour, steel, wood, null),
				Guard(root, "Guard_3", new Vector3(-17f, 0f, 20f), armour, steel, wood, Patrol(root, "Patrol_3", new Vector3(-17f, 0f, 17f), new Vector3(-17f, 0f, 26f))),
				Guard(root, "Guard_4", new Vector3(17f, 0f, 22f), armour, steel, wood, Patrol(root, "Patrol_4", new Vector3(17f, 0f, 19f), new Vector3(17f, 0f, 25f))),
				Guard(root, "Guard_5", new Vector3(-3.5f, 0f, 34.5f), armour, steel, wood, null),
				Guard(root, "Guard_6", new Vector3(3.5f, 0f, 34.5f), armour, steel, wood, null),
			};
			lord = EnemyLord(root, new Vector3(0f, 0f, 31f));
			list.Add(lord);
			return list.ToArray();
		}

		static Transform[] Patrol(Transform parent, string name, Vector3 a, Vector3 b)
		{
			var group = new GameObject(name).transform;
			group.SetParent(parent, false);
			var first = new GameObject("A").transform;
			first.SetParent(group, false);
			first.position = a;
			var second = new GameObject("B").transform;
			second.SetParent(group, false);
			second.position = b;
			return new[] { first, second };
		}

		static Soldier Guard(Transform parent, string name, Vector3 position, Material armour, Material steel, Material wood, Transform[] patrol)
		{
			var root = NewRoot(parent, name, position, "units/guard");
			var soldier = Configure(root, "Guard", false, 50f, 8f, 4.0f, 0.45f, 1.8f, 2.2f, 1.3f);
			if (patrol != null)
			{
				var agent = root.GetComponent<AiAgent>();
				agent.PatrolPoints = patrol;
			}

			var visual = new GameObject("Visual").transform;
			visual.SetParent(root.transform, false);
			Prim(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.9f, 0f), new Vector3(0.85f, 0.85f, 0.85f), armour);
			Prim(PrimitiveType.Sphere, "Helmet", visual, new Vector3(0f, 1.78f, 0f), new Vector3(0.5f, 0.5f, 0.5f), steel);
			Prim(PrimitiveType.Cube, "Spear", visual, new Vector3(0.55f, 1.2f, 0.7f), new Vector3(0.06f, 0.06f, 2.2f), wood);
			Prim(PrimitiveType.Cube, "SpearHead", visual, new Vector3(0.55f, 1.2f, 1.85f), new Vector3(0.14f, 0.14f, 0.3f), steel);
			root.SetActive(false); // woken by the game once the NavMesh exists
			return soldier;
		}

		static Soldier EnemyLord(Transform parent, Vector3 position)
		{
			var crimson = Mat("LordCrimson", new Color(0.45f, 0.06f, 0.1f));
			var gold = Mat("Gold", new Color(1f, 0.82f, 0.25f));
			var steel = Mat("Steel", new Color(0.75f, 0.78f, 0.85f));
			var dark = Mat("Dark", new Color(0.12f, 0.1f, 0.12f));

			var root = NewRoot(parent, "EnemyLord", position, "units/enemy-lord");
			var soldier = Configure(root, "Enemy Lord", true, 250f, 14f, 3.4f, 0.6f, 2.3f, 2.8f, 1.5f);
			soldier.StrikeDamage = 24f;
			soldier.StrikeRadius = 4.5f;
			soldier.StrikeInterval = 7f;
			soldier.StrikeWarningSeconds = 1.2f;
			soldier.StrikeTriggerDistance = 5.5f;
			root.GetComponent<HealthBar3D>().Height = 3.2f;
			root.GetComponent<HealthBar3D>().Width = 1.8f;

			var visual = new GameObject("Visual").transform;
			visual.SetParent(root.transform, false);
			Prim(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 1.1f, 0f), new Vector3(1.15f, 1.1f, 1.15f), crimson);
			Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 2.25f, 0f), new Vector3(0.6f, 0.6f, 0.6f), steel);
			Prim(PrimitiveType.Cube, "Crown", visual, new Vector3(0f, 2.65f, 0f), new Vector3(0.5f, 0.22f, 0.5f), gold);
			Prim(PrimitiveType.Cube, "Cape", visual, new Vector3(0f, 1.3f, -0.55f), new Vector3(1.0f, 1.8f, 0.1f), crimson);
			Prim(PrimitiveType.Cube, "Blade", visual, new Vector3(0.75f, 1.3f, 0.7f), new Vector3(0.12f, 0.12f, 1.7f), steel);
			Prim(PrimitiveType.Cube, "Hilt", visual, new Vector3(0.75f, 1.3f, -0.1f), new Vector3(0.45f, 0.1f, 0.14f), dark);
			root.SetActive(false);
			return soldier;
		}

		static GameObject NewRoot(Transform parent, string name, Vector3 position, string assetId)
		{
			var root = new GameObject(name);
			root.transform.SetParent(parent, false);
			root.transform.position = position;
			root.AddComponent<AssetIdTag>().Id = assetId;
			return root;
		}

		static Soldier Configure(GameObject root, string displayName, bool isLord, float health, float meleeDamage, float speed, float radius, float height, float attackRange, float attackCooldown)
		{
			var agent = root.AddComponent<NavMeshAgent>();
			agent.speed = speed;
			agent.acceleration = 14f;
			agent.angularSpeed = 360f;
			agent.radius = radius;
			agent.height = height;
			agent.stoppingDistance = attackRange * 0.6f;
			agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

			var capsule = root.AddComponent<CapsuleCollider>();
			capsule.radius = radius;
			capsule.height = height;
			capsule.center = new Vector3(0f, height * 0.5f, 0f);

			var life = root.AddComponent<Health>();
			life.MaxHealth = health;
			life.Team = 2;
			life.Restore();

			root.AddComponent<AiTarget>().Team = 2;

			var ai = root.AddComponent<AiAgent>();
			ai.Team = 2;
			ai.SenseRadius = isLord ? 18f : 15f;
			ai.LoseRadius = isLord ? 36f : 28f;
			ai.AttackRange = attackRange;
			ai.AttackCooldown = attackCooldown;

			var soldier = root.AddComponent<Soldier>();
			soldier.DisplayName = displayName;
			soldier.IsLord = isLord;
			soldier.MeleeDamage = meleeDamage;

			root.AddComponent<HealthBar3D>().Height = height + 0.5f;
			return soldier;
		}

		// ---- the player ----

		static GameObject BuildPlayer()
		{
			var blue = Mat("LordBlue", new Color(0.2f, 0.45f, 0.9f));
			var skin = Mat("Skin", new Color(0.88f, 0.7f, 0.55f));
			var steel = Mat("Steel", new Color(0.75f, 0.78f, 0.85f));
			var gold = Mat("Gold", new Color(1f, 0.82f, 0.25f));
			var cape = Mat("Cape", new Color(0.12f, 0.25f, 0.6f));

			var player = new GameObject("Player");
			player.transform.position = new Vector3(0f, 0.1f, -26f);
			player.AddComponent<CharacterController>();
			player.AddComponent<CharacterMotor>();
			player.AddComponent<CharacterViewControls>();

			var life = player.AddComponent<Health>();
			life.MaxHealth = 200f;
			life.Team = 1;
			life.Restore();
			player.AddComponent<AiTarget>().Team = 1;
			player.AddComponent<PlayerLord>();
			player.AddComponent<AssetIdTag>().Id = "characters/lord";

			var body = new GameObject("Body").transform;
			body.SetParent(player.transform, false);
			Prim(PrimitiveType.Capsule, "Torso", body, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), blue);
			Prim(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 1.62f, 0.02f), new Vector3(0.38f, 0.38f, 0.38f), skin);
			Prim(PrimitiveType.Cube, "Crown", body, new Vector3(0f, 1.9f, 0.02f), new Vector3(0.32f, 0.14f, 0.32f), gold);
			Prim(PrimitiveType.Cube, "Cape", body, new Vector3(0f, 1.0f, -0.4f), new Vector3(0.6f, 1.2f, 0.07f), cape);
			Prim(PrimitiveType.Cube, "Blade", body, new Vector3(0.5f, 1.0f, 0.45f), new Vector3(0.08f, 0.08f, 1.1f), steel);
			Prim(PrimitiveType.Cube, "Guard", body, new Vector3(0.5f, 1.0f, -0.05f), new Vector3(0.25f, 0.08f, 0.1f), gold);
			return player;
		}

		static void BuildCameraAndLight(CharacterMotor target)
		{
			var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
			var camera = cameraObject.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = new Color(0.56f, 0.72f, 0.9f);
			camera.fieldOfView = 68f;
			camera.nearClipPlane = 0.1f;
			camera.farClipPlane = 400f;
			cameraObject.AddComponent<AudioListener>();
			var view = cameraObject.AddComponent<CharacterViewCamera>();

			var serialized = new SerializedObject(view);
			serialized.FindProperty("target").objectReferenceValue = target;
			serialized.FindProperty("settings.StartView").enumValueIndex = (int)CameraView.ThirdPerson;
			serialized.FindProperty("settings.Distance").floatValue = 4.8f; // far enough back to see the fight, not so far that walls get in the way
			serialized.ApplyModifiedPropertiesWithoutUndo();

			var sun = new GameObject("Sun");
			sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
			var light = sun.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 1.15f;
			light.shadows = LightShadows.Soft;
		}

		// ---- helpers (shared: 3d-unity-graybox) ----

		static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material) => GrayboxBuilder.Box(parent, name, position, scale, material);

		static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider = false) => GrayboxBuilder.Prim(type, name, parent, position, scale, material, keepCollider);

		static Material Mat(string name, Color colour) => GrayboxBuilder.Mat(name, colour, MaterialDir);
	}
}
