using UnityEngine;

namespace Temnorid.Conquest
{
	/// <summary>
	/// The gate of the village hub: two posts and a red banner with a trigger between them. The hero walking through it marches out to the
	/// assault scene. Built in code when the village is drawn, since its place depends on the size of the farm.
	/// </summary>
	[RequireComponent(typeof(BoxCollider))]
	public sealed class MarchGate : MonoBehaviour
	{
		public static MarchGate Create(Transform parent, Vector3 position, Material template = null)
		{
			var root = new GameObject("MarchGate");
			root.transform.SetParent(parent, false);
			root.transform.localPosition = position;
			var wood = MakeMaterial(template, new Color(0.45f, 0.3f, 0.18f));
			var red = MakeMaterial(template, new Color(0.75f, 0.12f, 0.12f));
			Part(root.transform, "PostL", new Vector3(-3.5f, 3f, 0f), new Vector3(1f, 6f, 1f), wood);
			Part(root.transform, "PostR", new Vector3(3.5f, 3f, 0f), new Vector3(1f, 6f, 1f), wood);
			Part(root.transform, "Banner", new Vector3(0f, 5.2f, 0f), new Vector3(8f, 1.6f, 0.2f), red);

			var box = root.GetComponent<BoxCollider>();
			if (box == null) box = root.AddComponent<BoxCollider>();
			box.isTrigger = true;
			box.center = new Vector3(0f, 1.5f, 0f);
			box.size = new Vector3(6f, 3f, 2.5f);
			return root.AddComponent<MarchGate>();
		}

		static Material MakeMaterial(Material template, Color colour)
		{
			var shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
			var material = new Material(shader);
			if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
			material.color = colour;
			return material;
		}

		static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
		{
			var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
			part.name = name;
			part.transform.SetParent(parent, false);
			part.transform.localPosition = position;
			part.transform.localScale = scale;
			part.GetComponent<Renderer>().sharedMaterial = material;
			Destroy(part.GetComponent<Collider>());
		}

		void OnTriggerEnter(Collider other)
		{
			if (other.GetComponentInParent<HubHero>() == null) return;
			VillageGame.Instance?.MarchOut();
		}
	}

}
