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
		void OnTriggerEnter(Collider other)
		{
			if (other.GetComponentInParent<PlayerLord>() == null) return;
			ConquestGame.Instance?.Win(VictoryKind.TookHall);
		}
	}
}
