using System.Globalization;

namespace Temnorid.Conquest
{
	/// <summary>
	/// A hit a client reports to the host: which defender (its index in the scene), how much damage and with what ("sword" or "charge").
	/// The host decides whether it counts; this is only the wire format, with no Unity in it.
	/// </summary>
	public readonly struct ConquestHit
	{
		public const string Sword = "sword";
		public const string Charge = "charge";

		public readonly int Defender;
		public readonly float Damage;
		public readonly string Type;

		public ConquestHit(int defender, float damage, string type)
		{
			Defender = defender;
			Damage = damage;
			Type = type;
		}

		public string Write() => Defender.ToString(CultureInfo.InvariantCulture) + "|" + Damage.ToString("R", CultureInfo.InvariantCulture) + "|" + Type;

		public static bool TryRead(string text, out ConquestHit hit)
		{
			hit = default;
			if (string.IsNullOrEmpty(text) || text.Length > 64) return false;
			var parts = text.Split('|');
			if (parts.Length != 3) return false;
			if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var defender) || defender < 0) return false;
			if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var damage) || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f) return false;
			if (parts[2] != Sword && parts[2] != Charge) return false;
			hit = new ConquestHit(defender, damage, parts[2]);
			return true;
		}
	}
}
