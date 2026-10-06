using NUnit.Framework;

namespace DreamGamesCase.PureLogic.Tests
{
    // Pinning tests for a known inconsistency discovered during Iter 9 investigation.
    // DO NOT "fix" these by flipping the expected value — the inconsistency itself
    // is the bug. See docs/SUGGESTIONS.md §A7 for the unification path.
    [TestFixture]
    public class TileFactoryInconsistencyTests
    {
        [Test]
        public void CreateTile_Stone_ReturnsRockTileType_DivergesFromFactoryDict()
        {
            // Factory dict maps "stone" → TileType.Stone, but Stone class's ctor passes
            // TileType.Rock to its base. So CreateTile("stone").TileType is Rock, not Stone.
            // BoardPoolManager.TileTypeToPoolType sends Rock → PoolType.Rock, leaving
            // PoolType.Stone unreachable at runtime through this code path.
            TileModel model = TileFactory.CreateTile("stone");
            Assert.IsNotNull(model);
            Assert.AreEqual(TileType.Rock, model.TileType,
                "Stone obstacle's TileType is Rock (not Stone). See SUGGESTIONS §A7.");
        }
    }
}
