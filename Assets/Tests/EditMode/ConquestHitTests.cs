using NUnit.Framework;

namespace Temnorid.Conquest.Tests
{
	/// <summary>The message a guest sends the host when a blow lands: what the host reads must be what was written, and rubbish must be refused.</summary>
	public class ConquestHitTests
	{
		[Test]
		public void A_hit_survives_the_round_trip()
		{
			var text = new ConquestHit(4, 35f, ConquestHit.Charge).Write();
			Assert.IsTrue(ConquestHit.TryRead(text, out var hit));
			Assert.AreEqual(4, hit.Defender);
			Assert.AreEqual(35f, hit.Damage, 0.0001f);
			Assert.AreEqual(ConquestHit.Charge, hit.Type);
		}

		[TestCase("")]
		[TestCase(null)]
		[TestCase("1|20")]
		[TestCase("a|20|sword")]
		[TestCase("-1|20|sword")]
		[TestCase("1|0|sword")]
		[TestCase("1|-5|sword")]
		[TestCase("1|NaN|sword")]
		[TestCase("1|20|fireball")]
		[TestCase("1|20|sword|extra")]
		public void Nonsense_is_refused(string text)
		{
			Assert.IsFalse(ConquestHit.TryRead(text, out _));
		}

		[Test]
		public void The_number_is_read_the_same_in_every_culture()
		{
			var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
			try
			{
				System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("uk-UA");
				var text = new ConquestHit(1, 12.5f, ConquestHit.Sword).Write();
				Assert.AreEqual("1|12.5|sword", text);
				Assert.IsTrue(ConquestHit.TryRead(text, out var hit));
				Assert.AreEqual(12.5f, hit.Damage, 0.0001f);
			}
			finally
			{
				System.Threading.Thread.CurrentThread.CurrentCulture = previous;
			}
		}
	}
}
