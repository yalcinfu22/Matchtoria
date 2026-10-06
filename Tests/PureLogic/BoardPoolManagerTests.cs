using NUnit.Framework;

namespace DreamGamesCase.PureLogic.Tests
{
    [TestFixture]
    public class PoolTypeMapTests
    {
        // Pins TileType -> PoolType mapping. Regression guard for Iter 14 Purple fix
        // (Purple previously fell through default -> PoolType.None, causing runtime
        // KeyNotFoundException in BoardPoolManager.Get).
        [TestCase(TileType.Red,    PoolType.Matchable)]
        [TestCase(TileType.Green,  PoolType.Matchable)]
        [TestCase(TileType.Blue,   PoolType.Matchable)]
        [TestCase(TileType.Yellow, PoolType.Matchable)]
        [TestCase(TileType.Purple, PoolType.Matchable)]
        [TestCase(TileType.Rock,   PoolType.Rock)]
        [TestCase(TileType.Box,    PoolType.Box)]
        [TestCase(TileType.Vase,   PoolType.Vase)]
        [TestCase(TileType.Stone,  PoolType.Stone)]
        [TestCase(TileType.HorizontalRocket, PoolType.HorizontalRocket)]
        [TestCase(TileType.VerticalRocket,   PoolType.VerticalRocket)]
        [TestCase(TileType.TNT,       PoolType.TNT)]
        [TestCase(TileType.ColorBomb, PoolType.ColorBomb)]
        [TestCase(TileType.None,   PoolType.None)]
        public void FromTileType_MapsToExpectedPool(TileType input, PoolType expected)
        {
            PoolType actual = PoolTypeMap.FromTileType(input);
            Assert.AreEqual(expected, actual, $"TileType.{input} should map to PoolType.{expected}");
        }

        [Test]
        public void FromTileType_AllMatchableColors_ShareMatchablePool()
        {
            // Every color that GetRandomMatchableColor can emit (BoardModel.cs:375)
            // must resolve to the same pool so BoardPoolManager.Get never faults.
            TileType[] matchableColors = { TileType.Red, TileType.Green, TileType.Blue, TileType.Yellow, TileType.Purple };
            foreach (TileType color in matchableColors)
            {
                Assert.AreEqual(PoolType.Matchable, PoolTypeMap.FromTileType(color),
                    $"TileType.{color} is in GetRandomMatchableColor; must map to Matchable pool.");
            }
        }
    }
}
