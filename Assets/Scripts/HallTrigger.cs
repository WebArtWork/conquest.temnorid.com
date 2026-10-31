using UnityEngine;

namespace Temnorid.Conquest
{
	/// <summary>
	/// The inside of the enemy hall, just past its doorway. Getting in takes the village, as defeating
	/// the Lord does: standing near the wall outside does not count.
	/// </summary>
	[RequireComponent(typeof(BoxCollider))]
	public sealed class HallTrigger : MonoBehaviour
	{
		public static HallTrigger Instance { get; private set; }

		/// <summary>The doorway area in world space. Online, the host checks other players' positions against it.</summary>
		public Bounds Area => GetComponent<BoxCollider>().bounds;

		void Awake() => Instance = this;

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		void OnTriggerEnter(Collider other)
		{
			if (other.GetComponentInParent<PlayerLord>() == null) return;
			ConquestGame.Instance?.Win(VictoryKind.TookHall);
		}
	}
}
