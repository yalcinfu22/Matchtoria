using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

namespace DreamGamesCase.PureLogic.Tests
{
    [TestFixture]
    public class MatchManagerTests
    {
        private MatchManager _mm;

        [SetUp]
        public void SetUp()
        {
            _mm = new MatchManager();
        }

        // ============================== Horizontal ==============================

        [Test]
        public void Horizontal_ThreeInARow_DetectsOneMatch()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii("rrr");
            List<Match> matches = _mm.HorizontalMatchFinder(NodeLayer.Middle, board, 0);

            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual(3, matches[0].tilePositions.Count);
            Assert.AreEqual(TileType.Red, matches[0].matchableType);
        }

        [Test]
        public void Horizontal_TwoInARow_NoMatch()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii("rr.");
            List<Match> matches = _mm.HorizontalMatchFinder(NodeLayer.Middle, board, 0);
            Assert.AreEqual(0, matches.Count);
        }

        [Test]
        public void Horizontal_FiveInARow_OneMatchOfFive()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii("rrrrr");
            List<Match> matches = _mm.HorizontalMatchFinder(NodeLayer.Middle, board, 0);
            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual(5, matches[0].tilePositions.Count);
        }

        [Test]
        public void Horizontal_BrokenByOther_NoMatch()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii("rrg");
            List<Match> matches = _mm.HorizontalMatchFinder(NodeLayer.Middle, board, 0);
            Assert.AreEqual(0, matches.Count);
        }

        [Test]
        public void Horizontal_TwoSeparateMatches_Detected()
        {
            // "rrrg ggg" split across 7 cols
            var board = TestBoardBuilder.MiddleLayerFromAscii("rrrgggg");
            List<Match> matches = _mm.HorizontalMatchFinder(NodeLayer.Middle, board, 0);
            Assert.AreEqual(2, matches.Count);
            Assert.AreEqual(TileType.Red, matches[0].matchableType);
            Assert.AreEqual(3, matches[0].tilePositions.Count);
            Assert.AreEqual(TileType.Green, matches[1].matchableType);
            Assert.AreEqual(4, matches[1].tilePositions.Count);
        }

        [Test]
        public void Horizontal_GapBreaksRun_EmitsOnly3Match()
        {
            // B7 FIX (Iter 5): gap resets the run. "rr.rrr" → one 3-match on the
            // right side; the "rr" fragment is below the length-3 threshold so it
            // is ignored by the finder.
            var board = TestBoardBuilder.MiddleLayerFromAscii("rr.rrr");
            List<Match> matches = _mm.HorizontalMatchFinder(NodeLayer.Middle, board, 0);
            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual(3, matches[0].tilePositions.Count);
            // Verify it is the RIGHT-side 3-match, not the left fragment.
            CollectionAssert.Contains(matches[0].tilePositions, new Vector2Int(3, 0));
            CollectionAssert.Contains(matches[0].tilePositions, new Vector2Int(5, 0));
        }

        [Test]
        public void Horizontal_GapBreaksRun_PreservesBothSideMatches()
        {
            // B7 FIX: both sides >=3 → two separate matches.
            var board = TestBoardBuilder.MiddleLayerFromAscii("rrr.rrr");
            List<Match> matches = _mm.HorizontalMatchFinder(NodeLayer.Middle, board, 0);
            Assert.AreEqual(2, matches.Count);
            Assert.AreEqual(3, matches[0].tilePositions.Count);
            Assert.AreEqual(3, matches[1].tilePositions.Count);
        }

        [Test]
        public void Horizontal_NullMiddleLayer_DoesNotThrow()
        {
            // Validates A1 fix — null-check inside HorizontalMatchFinder.
            var board = new NodeModel[3, 1];
            for (int x = 0; x < 3; x++)
                board[x, 0] = new NodeModel(null, null, null); // all three layers null
            Assert.DoesNotThrow(() => _mm.HorizontalMatchFinder(NodeLayer.Middle, board, 0));
        }

        // =============================== Vertical ===============================

        [Test]
        public void Vertical_ThreeInAColumn_DetectsOneMatch()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "r",
                "r",
                "r");
            List<Match> matches = _mm.VerticalMatchFinder(NodeLayer.Middle, board, 0);
            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual(3, matches[0].tilePositions.Count);
            Assert.AreEqual(TileType.Red, matches[0].matchableType);
        }

        [Test]
        public void Vertical_BrokenByOther_NoMatch()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "r",
                "r",
                "g");
            List<Match> matches = _mm.VerticalMatchFinder(NodeLayer.Middle, board, 0);
            Assert.AreEqual(0, matches.Count);
        }

        [Test]
        public void Vertical_NullMiddleLayer_DoesNotThrow()
        {
            // Validates A1 fix — null-check inside VerticalMatchFinder.
            var board = new NodeModel[1, 3];
            for (int y = 0; y < 3; y++)
                board[0, y] = new NodeModel(null, null, null);
            Assert.DoesNotThrow(() => _mm.VerticalMatchFinder(NodeLayer.Middle, board, 0));
        }

        // ============================ FindMatches (full) =========================

        [Test]
        public void FindMatches_HorizontalThree_OneMatchInResult()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rrr",
                "gby",
                "pgb");
            MatchResult result = _mm.FindMatches(NodeLayer.Middle, board);
            Assert.AreEqual(1, result.matches.Count);
            Assert.AreEqual(TileType.Red, result.matches[0].matchableType);
        }

        [Test]
        public void FindMatches_NoMatchesAtAll_EmptyResult()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rgb",
                "gbr",
                "brg");
            MatchResult result = _mm.FindMatches(NodeLayer.Middle, board);
            Assert.AreEqual(0, result.matches.Count);
        }

        // ======================== FindMatchesAfterSwap (fast) ====================

        [Test]
        public void FindMatchesAfterSwap_HorizontalSwapProducesHorizontalMatch()
        {
            // Starting:        After swap (0,1)<->(1,1):
            //   b b b             b b b
            //   g r r             r g r     <- pos1=(0,1) has r now, pos2=(1,1) has g
            //   p p y             p p y
            //
            // We set up the board as post-swap (caller swaps tiles before calling
            // FindMatchesAfterSwap, per BoardModel.ProcessSwap phase 1).
            // Scenario: swap caused "rrr" on row y=1? Here we invent a fresh case:
            // Pre-swap row y=1 has "grr"; user drags (0,1)→(1,1), post-swap row = "rgr" — no match.
            // Instead use:
            // Pre-swap  "r.r"  with '.' at (1,1); actually simpler — use a concrete
            // layout where post-swap gives "rrr" in row y=1.
            //
            // Concrete: post-swap board where (0,1)↔(1,1) was swapped and now row y=1 = "rrr"
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "bby",
                "rrr",
                "pgb");

            MatchResult r = _mm.FindMatchesAfterSwap(
                NodeLayer.Middle, board,
                new Vector2Int(0, 1), new Vector2Int(1, 1));

            Assert.AreEqual(1, r.matches.Count);
            Assert.AreEqual(TileType.Red, r.matches[0].matchableType);
        }

        [Test]
        public void FindMatchesAfterSwap_VerticalSwapProducesVerticalMatch()
        {
            // Post-swap column x=0 = r/r/r
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rby",
                "rgb",
                "rpb");

            MatchResult r = _mm.FindMatchesAfterSwap(
                NodeLayer.Middle, board,
                new Vector2Int(0, 1), new Vector2Int(0, 2));

            Assert.AreEqual(1, r.matches.Count);
            Assert.AreEqual(TileType.Red, r.matches[0].matchableType);
            Assert.AreEqual(3, r.matches[0].tilePositions.Count);
        }

        [Test]
        public void FindMatchesAfterSwap_NoMatchAfterSwap_EmptyMatches()
        {
            // Post-swap row y=1 = "rgb" — no 3-run anywhere
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "pgy",
                "rgb",
                "pbg");

            MatchResult r = _mm.FindMatchesAfterSwap(
                NodeLayer.Middle, board,
                new Vector2Int(0, 1), new Vector2Int(1, 1));

            Assert.AreEqual(0, r.matches.Count);
        }

        [Test]
        public void FindMatchesAfterSwap_FourHorizontal_ProducesHorizontalRocket()
        {
            // Post-swap row y=1 = "rrrr" → one 4-match → horizontal rocket
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "bbby",
                "rrrr",
                "pgbp");

            MatchResult r = _mm.FindMatchesAfterSwap(
                NodeLayer.Middle, board,
                new Vector2Int(0, 1), new Vector2Int(1, 1));

            Assert.AreEqual(1, r.matches.Count);
            Assert.AreEqual(4, r.matches[0].tilePositions.Count);
            Assert.AreEqual(1, r.specialTileTypes.Count);
            Assert.AreEqual(TileType.HorizontalRocket, r.specialTileTypes[0]);
        }

        [Test]
        public void FindMatchesAfterSwap_LShape_ProducesTNT()
        {
            // L-shape sharing a corner cell between horizontal and vertical matches
            // Post-swap layout: row y=1 "rrr", column x=0 "rrr" (shared corner at (0,1))
            //   . . . .
            //   r r r .
            //   r . . .
            //   r . . .
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "....",
                "rrr.",
                "r...",
                "r...");

            // The swap must land on the L's corner so intersection set includes it.
            // Row y=2 = "rrr.", column x=0 = "rrr" (y=0,1,2). Corner at (0, 2).
            MatchResult r = _mm.FindMatchesAfterSwap(
                NodeLayer.Middle, board,
                new Vector2Int(0, 2), new Vector2Int(1, 2));

            Assert.GreaterOrEqual(r.matches.Count, 1);
            bool hasTNT = false;
            for (int i = 0; i < r.specialTileTypes.Count; i++)
                if (r.specialTileTypes[i] == TileType.TNT) hasTNT = true;
            Assert.IsTrue(hasTNT, "Expected at least one TNT special from L-shape");
        }
    }
}
