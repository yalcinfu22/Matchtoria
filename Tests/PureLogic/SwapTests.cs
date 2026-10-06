using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DreamGamesCase.PureLogic.Tests
{
    // Phase-1 scope: Swap command endpoint correctness, no-match revert integrity,
    // null-cell rejection, consecutive-call timestamp reset, vertical-swap column
    // match smoke.
    //
    // Out-of-scope (Phase 3 — rocket/TNT/ColorBomb triggers): tests that create
    // a 4+ match and assert on the spawned special tile.
    //
    // A9 (match-splash → Rocket self-trigger) is RESOLVED: NodeModel.DamageLayerWith
    // L64-69 source-filters non-Rocket/TNT sources for ITriggerable tiles, so a
    // matchable-color splash never triggers a Rocket. See docs/SUGGESTIONS.md.
    //
    // Open Phase-3 blockers for spawn tests: A2 (DoubleRocket null-Invoke NRE) and
    // A3 (HRocket/VRocket swap-trigger NRE on barren own-cell). Both are tracked
    // in docs/SUGGESTIONS.md. Once those land, spawn tests will be added under a
    // SwapSpecialSpawnTests fixture. Iter 18.
    [TestFixture]
    public class SwapTests
    {
        private BoardModel _model;
        private System.Random _rng;

        [SetUp]
        public void SetUp()
        {
            _model = new BoardModel();
            _rng = new System.Random(42);
            TileFactory.RandomRangeOverride = max => _rng.Next(0, max);
        }

        [TearDown]
        public void TearDown()
        {
            TileFactory.RandomRangeOverride = null;
        }

        // =====================================================================
        // Endpoint correctness — horizontal swap emits a single Swap command with
        // Start=pos1, Target=pos2. View resolves direction from the endpoints.
        // =====================================================================

        [Test]
        public void ProcessSwap_HorizontalSwap_EmitsSingleSwapWithPos1ToPos2Endpoints()
        {
            // y=0 = "rrgr" pre-swap. Swap (2,0)↔(3,0): g↔r → y=0 = "rrrg" (3-match).
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "bbby",
                "rrgr");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(2, 0), new Vector2Int(3, 0));

            int swapCount = result.Commands.Count(c => c.CommandType == Commands.Swap);
            Assert.AreEqual(1, swapCount, "valid horizontal swap must emit exactly 1 Swap command");

            bool hasSwapPos1ToPos2 = result.Commands.Any(c =>
                c.CommandType == Commands.Swap &&
                Mathf.RoundToInt(c.StartPosition.x)  == 2 && Mathf.RoundToInt(c.StartPosition.y)  == 0 &&
                Mathf.RoundToInt(c.TargetPosition.x) == 3 && Mathf.RoundToInt(c.TargetPosition.y) == 0);

            Assert.IsTrue(hasSwapPos1ToPos2, "horizontal swap must emit Swap Start=(2,0) Target=(3,0)");
        }

        // =====================================================================
        // Endpoint correctness — vertical swap emits a single Swap with
        // Start=pos1 (lower), Target=pos2 (upper).
        // =====================================================================

        [Test]
        public void ProcessSwap_VerticalSwap_EmitsSingleSwapWithLowerToUpperEndpoints()
        {
            // Pre-swap y=0="rbb" (bottom), y=1="grr". Swap (0,0)↔(0,1): r↔g.
            // Post: y=0="gbb", y=1="rrr" → 3-match on y=1.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "grr",   // y=1 top
                "rbb");  // y=0 bottom
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(0, 1));

            int swapCount = result.Commands.Count(c => c.CommandType == Commands.Swap);
            Assert.AreEqual(1, swapCount, "valid vertical swap must emit exactly 1 Swap command");

            bool hasSwapLowerToUpper = result.Commands.Any(c =>
                c.CommandType == Commands.Swap &&
                Mathf.RoundToInt(c.StartPosition.x)  == 0 && Mathf.RoundToInt(c.StartPosition.y)  == 0 &&
                Mathf.RoundToInt(c.TargetPosition.x) == 0 && Mathf.RoundToInt(c.TargetPosition.y) == 1);

            Assert.IsTrue(hasSwapLowerToUpper, "vertical swap must emit Swap Start=(0,0) Target=(0,1)");
        }

        // =====================================================================
        // Null middle cell — swap attempt on an empty cell must be rejected by
        // IsValidSwap (null is not IMovable). Zero commands emitted.
        // =====================================================================

        [Test]
        public void ProcessSwap_NullMiddleCell_ReturnsEmptyCommands()
        {
            // '.' at (1,0) → null Middle layer. Swap (0,0)↔(1,0) must be rejected.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rgb",
                "r.b");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(1, 0));

            Assert.AreEqual(0, result.Commands.Count,
                "swap into null middle cell must emit no commands");
        }

        // =====================================================================
        // ProcessSwap resets m_currentTimeStamp to 0 at the top, so the earliest
        // Swap command in a second call is at t≈0 — not carried over from the
        // first call's cascade timestamps.
        // =====================================================================

        [Test]
        public void ProcessSwap_ConsecutiveCalls_TimestampResetsToZero()
        {
            // First swap: valid 3-match → cascade runs → timestamps advance.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "grr",
                "rbb");
            _model.SetBoardForTests(board);

            SwapResult first = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(0, 1));
            Assert.Greater(first.Commands.Count, 0, "first swap should produce commands");

            // Second swap: fresh board, same model instance. The model must reset
            // m_currentTimeStamp=0 internally at the top of ProcessSwap.
            var board2 = TestBoardBuilder.MiddleLayerFromAscii(
                "grr",
                "rbb");
            _model.SetBoardForTests(board2);

            SwapResult second = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(0, 1));

            float earliestSwapTs = second.Commands
                .Where(c => c.CommandType == Commands.Swap)
                .Select(c => c.startTimeStamp)
                .DefaultIfEmpty(float.PositiveInfinity)
                .Min();

            Assert.Less(earliestSwapTs, 0.001f,
                "second ProcessSwap must start timestamp at 0 — earliest Swap is t≈0");
        }

        // =====================================================================
        // Post-swap board integrity for an invalid swap — every cell is
        // untouched. Stronger than "pos1/pos2 reverted" — checks the WHOLE board.
        // =====================================================================

        [Test]
        public void ProcessSwap_NoMatchProduced_EveryCellIsUnchanged()
        {
            // Board designed so no 3-match is possible anywhere. Every adjacent swap
            // must revert with the board perfectly unchanged.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rgby",
                "gryb",
                "bygr",
                "yrbg");
            _model.SetBoardForTests(board);

            int w = board.GetLength(0);
            int h = board.GetLength(1);
            TileType[,] before = new TileType[w, h];
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                before[x, y] = _model.GetTileTypeAt(new Vector2Int(x, y), NodeLayer.Middle);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(1, 0));

            // Must be rejected-with-revert path: 1 forward + 1 revert Swap.
            int swapCount = result.Commands.Count(c => c.CommandType == Commands.Swap);
            Assert.AreEqual(2, swapCount,
                "no-match swap emits exactly 1 forward + 1 revert Swap command");

            // Every cell must equal the pre-swap snapshot.
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                TileType now = _model.GetTileTypeAt(new Vector2Int(x, y), NodeLayer.Middle);
                Assert.AreEqual(before[x, y], now,
                    $"cell ({x},{y}) must be unchanged after invalid swap (before={before[x, y]}, after={now})");
            }
        }

        // =====================================================================
        // Valid 3-match swap emits exactly 1 Swap command for the swap itself —
        // no swap-back rewind. Cascade uses Fall* enums, not Swap, so the count
        // is pinned even with multi-step cascades.
        // =====================================================================

        [Test]
        public void ProcessSwap_ValidMatch_EmitsExactlyOneSwapCommand()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "grr",
                "rbb");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(0, 1));

            int swapCount = result.Commands.Count(c => c.CommandType == Commands.Swap);

            Assert.AreEqual(1, swapCount,
                "valid swap must emit exactly 1 Swap command (no revert on success)");
        }

        // =====================================================================
        // Vertical swap that produces a column 3-match is routed through
        // FindMatchesAfterSwap's vertical branch. End-to-end smoke that the
        // match is detected and destroy commands are emitted.
        // =====================================================================

        [Test]
        public void ProcessSwap_VerticalSwapProducesColumnMatch_DetectedAndDestroyed()
        {
            // Col x=1 pre-swap bottom→top: r,r,g,r. Swap (1,2)↔(1,3): g↔r.
            // Post col x=1: r,r,r,g → 3-match on y=0..2.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "brb",  // y=3 top : (1,3) = r
                "bgb",  // y=2     : (1,2) = g
                "brb",  // y=1     : (1,1) = r
                "yrb"); // y=0 bot : (1,0) = r
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(1, 2), new Vector2Int(1, 3));

            int destroyCount = result.Commands.Count(c => c.CommandType == Commands.DestroySelf);
            Assert.GreaterOrEqual(destroyCount, 3,
                "vertical swap producing a column 3-match must emit ≥3 DestroySelf commands");
        }

        // =====================================================================
        // Boundary: 3-match lands on the topmost row (y = h-1). Pins that
        // MatchManager's horizontal scan handles the top boundary correctly.
        // =====================================================================

        [Test]
        public void ProcessSwap_MatchAtTopRow_DetectedAndDestroyed()
        {
            // Swap (0,1)↔(0,2): r↔g. Post y=2 = "rrr" → 3-match at topmost row.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "grr",  // y=2 top : (0,2)=g, (1,2)=r, (2,2)=r
                "rby",  // y=1     : (0,1)=r
                "ybg"); // y=0 bot
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 1), new Vector2Int(0, 2));

            int destroyCount = result.Commands.Count(c => c.CommandType == Commands.DestroySelf);
            Assert.GreaterOrEqual(destroyCount, 3,
                "3-match at topmost row (y=h-1) must still be detected and destroyed");
        }

        // =====================================================================
        // Boundary: 3-match lands in the rightmost column (x = w-1). Pins that
        // MatchManager's vertical scan handles the right boundary correctly.
        // =====================================================================

        [Test]
        public void ProcessSwap_MatchAtRightmostColumn_DetectedAndDestroyed()
        {
            // Swap (1,2)↔(2,2): r↔g. Post col 2 = r(y=0), r(y=1), r(y=2) → 3-match.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "yrg",  // y=2 top : (1,2)=r, (2,2)=g
                "gbr",  // y=1     : (2,1)=r
                "bbr"); // y=0 bot : (2,0)=r
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(1, 2), new Vector2Int(2, 2));

            int destroyCount = result.Commands.Count(c => c.CommandType == Commands.DestroySelf);
            Assert.GreaterOrEqual(destroyCount, 3,
                "3-match in rightmost column (x=w-1) must still be detected and destroyed");
        }

        // =====================================================================
        // Cascade invariant: after ProcessSwap returns with a valid match, the
        // Middle layer must be fully filled — no null cells. ProcessCascade's
        // fall+spawn loop is responsible for this. Regression would mean a
        // visible gap on the grid that never refills.
        // =====================================================================

        [Test]
        public void ProcessSwap_AfterValidMatch_MiddleLayerHasNoNullCells()
        {
            // 4x4 board with no pre-existing match. Swap (0,0)↔(0,1) makes y=1 "rrrb" → no 3-match.
            // Redesign: swap brings r into place on a 3-row.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "bypg",
                "ypyg",
                "grrb",
                "rbby");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(0, 1));
            Assert.Greater(result.Commands.Count, 0, "valid swap should produce commands");

            int w = board.GetLength(0);
            int h = board.GetLength(1);
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                TileType here = _model.GetTileTypeAt(new Vector2Int(x, y), NodeLayer.Middle);
                Assert.AreNotEqual(TileType.None, here,
                    $"post-cascade cell ({x},{y}) must be non-empty (Middle layer fully refilled)");
            }
        }

        // =====================================================================
        // Cascade invariant: all command positions must be inside board bounds.
        // A stray OOB Position would indicate a fall/spawn emitter misreading
        // the board dimensions.
        // =====================================================================

        [Test]
        public void ProcessSwap_AfterValidMatch_AllCommandPositionsAreInBounds()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "bypg",
                "ypyg",
                "grrb",
                "rbby");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(0, 1));

            int w = board.GetLength(0);
            int h = board.GetLength(1);
            foreach (Command cmd in result.Commands)
            {
                int sx = Mathf.RoundToInt(cmd.StartPosition.x);
                int sy = Mathf.RoundToInt(cmd.StartPosition.y);
                int tx = Mathf.RoundToInt(cmd.TargetPosition.x);
                int ty = Mathf.RoundToInt(cmd.TargetPosition.y);

                // Target must always be inside the grid.
                Assert.IsTrue(tx >= 0 && tx < w, $"target X {tx} out of bounds (w={w}) for {cmd.CommandType}");
                Assert.IsTrue(ty >= 0 && ty < h, $"target Y {ty} out of bounds (h={h}) for {cmd.CommandType}");

                if (cmd.CommandType == Commands.Spawn)
                {
                    // Top-row refill: Start sits exactly one cell above the top row.
                    // In-place specials: Start == Target (still in bounds).
                    Assert.IsTrue(sx >= 0 && sx < w, $"spawn start X {sx} out of bounds (w={w})");
                    Assert.IsTrue(sy == h || (sy >= 0 && sy < h),
                        $"spawn start Y must be either == height ({h}) for refill or in-bounds for in-place spawn, got {sy}");
                }
                else
                {
                    Assert.IsTrue(sx >= 0 && sx < w, $"start X {sx} out of bounds (w={w}) for {cmd.CommandType}");
                    Assert.IsTrue(sy >= 0 && sy < h, $"start Y {sy} out of bounds (h={h}) for {cmd.CommandType}");
                }
            }
        }

        // =====================================================================
        // Revert path Start/Target precision — both forward and revert Swap
        // commands carry identical (Start=pos1, Target=pos2) endpoints. The View
        // executes a Swap as "exchange contents of these two cells", direction-
        // less, so revert is structurally the same command at a later timestamp.
        // =====================================================================

        [Test]
        public void ProcessSwap_NoMatchProduced_RevertEmitsExactStartTargetPairs()
        {
            // 2x2 board with no possible 3-match. Swap (0,0)↔(1,0) → forward Swap
            // Start=(0,0) Target=(1,0); no match → revert Swap with the same
            // endpoints at a later timestamp.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rg",
                "gr");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(1, 0));

            // Exactly 2 swaps: 1 forward + 1 revert.
            int swapCount = result.Commands.Count(c => c.CommandType == Commands.Swap);
            Assert.AreEqual(2, swapCount, "no-match swap emits exactly 1 forward + 1 revert Swap");

            // The (Start=(0,0), Target=(1,0)) pair must appear exactly twice — once
            // for forward, once for revert.
            int swapPos1ToPos2 = result.Commands.Count(c =>
                c.CommandType == Commands.Swap &&
                Mathf.RoundToInt(c.StartPosition.x)  == 0 && Mathf.RoundToInt(c.StartPosition.y)  == 0 &&
                Mathf.RoundToInt(c.TargetPosition.x) == 1 && Mathf.RoundToInt(c.TargetPosition.y) == 0);

            Assert.AreEqual(2, swapPos1ToPos2,
                "Swap Start=(0,0) Target=(1,0) must appear twice — once forward, once revert");

            // Forward and revert must be at distinct timestamps. ProcessSwap calls
            // AdvanceTime() between forward emit and revert emit when no match found.
            float[] swapTs = result.Commands
                .Where(c => c.CommandType == Commands.Swap)
                .Select(c => c.startTimeStamp)
                .OrderBy(t => t)
                .ToArray();
            Assert.AreEqual(2, swapTs.Length);
            Assert.Less(swapTs[0], swapTs[1],
                "forward Swap must precede revert Swap in time (AdvanceTime between phases)");
        }
    }
}
