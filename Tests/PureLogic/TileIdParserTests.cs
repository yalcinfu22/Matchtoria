using NUnit.Framework;

namespace DreamGamesCase.PureLogic.Tests
{
    [TestFixture]
    public class TileIdParserTests
    {
        // Bare ids: type recognized, health = per-type default (Box=1, Vase=2, others=0).
        [TestCase("red",       TileType.Red,              0)]
        [TestCase("green",     TileType.Green,            0)]
        [TestCase("blue",      TileType.Blue,             0)]
        [TestCase("yellow",    TileType.Yellow,           0)]
        [TestCase("box",       TileType.Box,              1)]
        [TestCase("vase",      TileType.Vase,             2)]
        [TestCase("stone",     TileType.Stone,            0)]
        [TestCase("ro_h",      TileType.HorizontalRocket, 0)]
        [TestCase("ro_v",      TileType.VerticalRocket,   0)]
        [TestCase("TNT",       TileType.TNT,              0)]
        [TestCase("ColorBomb", TileType.ColorBomb,        0)]
        public void Parse_BareIds_ResolveTypeAndDefaultHealth(string id, TileType expectedType, int expectedHealth)
        {
            TileIdParser.Result result = TileIdParser.Parse(id);
            Assert.IsTrue(result.Valid, $"'{id}' must parse as valid");
            Assert.AreEqual(expectedType, result.Type);
            Assert.AreEqual(expectedHealth, result.Health);
        }

        // Suffix digits override the per-type default.
        [TestCase("box1",  TileType.Box,  1)]
        [TestCase("box3",  TileType.Box,  3)]
        [TestCase("box5",  TileType.Box,  5)]
        [TestCase("vase1", TileType.Vase, 1)]
        [TestCase("vase3", TileType.Vase, 3)]
        [TestCase("vase5", TileType.Vase, 5)]
        public void Parse_NumericSuffix_OverridesHealth(string id, TileType expectedType, int expectedHealth)
        {
            TileIdParser.Result result = TileIdParser.Parse(id);
            Assert.IsTrue(result.Valid);
            Assert.AreEqual(expectedType, result.Type);
            Assert.AreEqual(expectedHealth, result.Health);
        }

        [TestCase("")]
        [TestCase("null")]
        [TestCase("nonsense")]
        [TestCase("123")]
        [TestCase("box_3")]
        public void Parse_InvalidId_ReturnsInvalid(string id)
        {
            TileIdParser.Result result = TileIdParser.Parse(id);
            Assert.IsFalse(result.Valid, $"'{id}' should not parse as valid");
        }

        [Test]
        public void Parse_NullId_ReturnsInvalid()
        {
            TileIdParser.Result result = TileIdParser.Parse(null);
            Assert.IsFalse(result.Valid);
        }
    }
}
