using System.Collections;
using System.IO;
using ITKamianets.Engine.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Conquest.Tests
{
	/// <summary>On a phone the touch controls feed the same actions the keyboard does. Here they are built on purpose and the virtual touch device is driven.</summary>
	public class TouchControlsTests
	{
		string _saveFolder;

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "conquest-touch-" + System.Guid.NewGuid());
			ConquestGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			ConquestGame.SaveRootOverride = null;
			Time.timeScale = 1f;
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		[UnityTest]
		public IEnumerator The_touch_controls_exist_and_the_attack_button_swings_the_sword()
		{
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			var game = ConquestGame.Instance;
			game.SaveEnabled = false;
			var hud = Object.FindAnyObjectByType<ConquestHud>();
			hud.BuildTouchControls();
			yield return null;

			Assert.AreEqual(1, Object.FindObjectsByType<TouchLookArea>(FindObjectsInactive.Include).Length, "one look area");
			Assert.AreEqual(1, Object.FindObjectsByType<OnScreenStick>(FindObjectsInactive.Include).Length, "one move stick");
			Assert.AreEqual(4, Object.FindObjectsByType<OnScreenButton>(FindObjectsInactive.Include).Length, "attack, charge, jump and run");
			var device = InputSystem.GetDevice<EngineTouchControls>();
			Assert.IsNotNull(device, "the virtual touch device exists while the controls are on");

			// Pressing the attack button on that device is the main action, which swings the sword.
			var lord = game.PlayerHealth.GetComponent<PlayerLord>();
			var swings = lord.SwingCount;
			InputSystem.QueueStateEvent(device, new EngineTouchControlsState { buttons = 1u << 3 }); // bit 3 is the Primary button
			var end = Time.realtimeSinceStartup + 3f;
			while (lord.SwingCount == swings && Time.realtimeSinceStartup < end) yield return null;
			Assert.Greater(lord.SwingCount, swings, "the attack button swung the sword");
		}
	}
}
