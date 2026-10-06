using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

namespace DreamGamesCase.PureLogic.Tests
{
    [TestFixture]
    public class FallManagerTests
    {
        private FallManager _fm;

        private System.Random _rng;

        [SetUp]
        public void SetUp()
        {
            _fm = new FallManager();

            // Deterministic seed so assertions on spawned color are stable.
            _rng = new System.Random(42);
            TileFactory.RandomRangeOverride = max => _rng.Next(0, max);
        }

        [TearDown]
        public void TearDown()
        {
            TileFactory.RandomRangeOverride = null;
        }

        // =============================== FallIteration ==========================

        [Test]
        public void FallIteration_SingleColumnOneEmpty_StraightFall()
        {
            // Column x=0: bottom empty, middle tile → tile should drop one cell
            // Layout (top → bottom):
            //   r         (y=2)
            //   r         (y=1)
            //   .         (y=0)
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "r",
                "r",
                ".");

            List<Command> cmds = _fm.FallIteration(board, 0f, 0.1f);

            // One tile fell from y=1 to y=0 (the tile at y=2 stays put this iteration
            // because y=1 became empty after the drop).
            int fallCount = 0;
            foreach (Command c in cmds)
                if (c.CommandType == Commands.Fall) fallCount++;
            Assert.GreaterOrEqual(fallCount, 1, "Expected at least one straight Fall command");

            // Bottom now has a Red tile
            Assert.NotNull(board[0, 0].GetLayer(NodeLayer.Middle));
            Assert.AreEqual(TileType.Red, board[0, 0].GetLayer(NodeLayer.Middle).TileType);
        }

        [Test]
        public void FallIteration_DiagonalPullFromUpRight_EmitsFallRight()
        {
            // (0,0) empty, (0,1) Stone (blocks straight-up fall — Stone is not IMovable),
            // (1,1) Red (movable), (1,0) Box (HasMiddle=true, required by FallRight branch).
            // Expected: Red pulled from (1,1) into (0,0), Commands.FallRight emitted at source pos.
            //
            // Naming note: "FallRight" is source-relative (tile fell TO the down-left of its origin) —
            // see SUGGESTIONS.md §B3 for this confusion.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "Sr",
                ".B");

            List<Command> cmds = _fm.FallIteration(board, 0f, 0.1f);

            bool hasFallRight = false;
            foreach (Command c in cmds)
                if (c.CommandType == Commands.FallRight) hasFallRight = true;

            Assert.IsTrue(hasFallRight,
                "Expected Commands.FallRight when pulling from up-right neighbour");
            Assert.AreEqual(TileType.Red, board[0, 0].GetLayer(NodeLayer.Middle).TileType);
        }

        [Test]
        public void FallIteration_DiagonalPullFromUpLeft_EmitsFallLeft()
        {
            // (1,0) empty, (1,1) Stone (blocks straight-up), (0,1) Red (movable).
            // Expected: Red pulled from (0,1) into (1,0), Commands.FallLeft at source pos (0,1).
            //
            // FallRight branch is tried BEFORE FallLeft, so we must make right-diag fail
            // (x+1=2 oob here) to force the left-diag path.
            // Board layout (2 wide, 2 tall):
            //   r S    (y=1 top)   — (0,1)=Red movable, (1,1)=Stone blocks straight-up at x=1
            //   B .    (y=0 bot)   — (0,0)=Box (not empty, not movable), (1,0)=empty target
            // Simulation (right-to-left scan):
            //   y=0, x=1: (1,0) empty, (1,1)=Stone (not movable, HasMiddle → side-fall allowed),
            //             FallRight impossible (x+1=2 oob), FallLeft: (0,1)=Red movable → fires.
            //   y=0, x=0: (0,0)=Box → not empty → skip.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rS",
                "B.");

            List<Command> cmds = _fm.FallIteration(board, 0f, 0.1f);

            bool hasFallLeft = false;
            foreach (Command c in cmds)
                if (c.CommandType == Commands.FallLeft) hasFallLeft = true;

            Assert.IsTrue(hasFallLeft,
                "Expected Commands.FallLeft when pulling from up-left (up blocked, up-right OOB)");
            // (1,0) should now contain Red (pulled from (0,1))
            Assert.AreEqual(TileType.Red, board[1, 0].GetLayer(NodeLayer.Middle).TileType);
        }

        [Test]
        public void FallIteration_FullColumn_NoFallCommands()
        {
            // Column full top to bottom → nothing moves, nothing spawns.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "r",
                "g",
                "b");

            List<Command> cmds = _fm.FallIteration(board, 0f, 0.1f);

            Assert.AreEqual(0, cmds.Count, "Full column should produce no commands");
        }

        [Test]
        public void FallIteration_EmptyTopCell_EmitsSpawnCommand()
        {
            // Top row cell empty → TileFactory.CreateTile("random") → Spawn command
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                ".",
                "r",
                "g");

            List<Command> cmds = _fm.FallIteration(board, 0f, 0.1f);

            int spawnCount = 0;
            foreach (Command c in cmds)
                if (c.CommandType == Commands.Spawn) spawnCount++;

            Assert.AreEqual(1, spawnCount, "Empty top cell should trigger one spawn");
            // Top cell now filled with a Matchable (random color 4 options)
            TileModel spawned = board[0, 2].GetLayer(NodeLayer.Middle);
            Assert.NotNull(spawned);
            Assert.IsTrue(
                spawned.TileType == TileType.Red ||
                spawned.TileType == TileType.Green ||
                spawned.TileType == TileType.Blue ||
                spawned.TileType == TileType.Yellow,
                "Spawned tile should be one of the 4 matchable colors");
        }

        [Test]
        public void FallIteration_MiddleEmpty_FillsFromAbove_LeavesTopEmptyForNextIteration()
        {
            // Middle empty with tile above — after ONE iteration, middle filled, top empty.
            // (Top row empty fill only happens if top stays empty after cascading — but in
            // a single iteration, we expect the top-fill spawn branch to run too.)
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "r",
                ".",
                "b");

            _fm.FallIteration(board, 0f, 0.1f);

            // y=1 now has the Red tile that was at y=2
            TileModel m1 = board[0, 1].GetLayer(NodeLayer.Middle);
            Assert.NotNull(m1);
            Assert.AreEqual(TileType.Red, m1.TileType);

            // y=2 now filled by spawn (top-row fill branch always runs same iteration)
            TileModel m2 = board[0, 2].GetLayer(NodeLayer.Middle);
            Assert.NotNull(m2, "Top row should be refilled by spawn in same iteration");
        }

        [Test]
        public void FallIteration_DiagonalPullFromTileSettledInPriorIteration_StillFires()
        {
            // Regression: a tile that "settled" (sat in place) during iteration N must
            // remain pull-able as a diagonal source in iteration N+1. Side-fall is gated
            // on (x, y+1) being a non-movable obstacle, so we use a Stone to enable it.
            //
            // Layout (4 wide, 4 tall):
            //   y=3:  . . . .
            //   y=2:  . . . .
            //   y=1:  . . r S   ← (2,1) Red sits unmoved through iter 1, (3,1) Stone
            //   y=0:  r r r r
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "....",
                "....",
                "..rS",
                "rrrr");

            _fm.FallIteration(board, 0f, 0.1f);

            // Simulate a cascade-match opening a new hole at (3,0). Above it is Stone,
            // so straight fall is blocked — only diagonal pull from (2,1) can fill it.
            board[3, 0].SetLayer(NodeLayer.Middle, null);

            List<Command> iter2 = _fm.FallIteration(board, 0.1f, 0.1f);

            Assert.IsNotNull(board[3, 0].GetLayer(NodeLayer.Middle),
                "(3,0) must be filled in iter 2 — settled tiles must remain pull-able under Stone");
            bool hasFallLeft = false;
            foreach (Command c in iter2)
                if (c.CommandType == Commands.FallLeft) hasFallLeft = true;
            Assert.IsTrue(hasFallLeft,
                "Expected FallLeft pulling settled (2,1) into (3,0)");
        }

        // ================================ Fall (full loop) =======================

        [Test]
        public void Fall_MultipleFalls_KeepsLoopingUntilStable()
        {
            // Stack with two holes in a column — Fall() should run until all cells full.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                ".",
                ".",
                "r");

            List<Command> cmds = new List<Command>();
            float t = 0f;
            const float fallTime = 0.1f;
            while (true)
            {
                List<Command> iter = _fm.FallIteration(board, t, fallTime);
                if (iter.Count == 0) break;
                cmds.AddRange(iter);
                t += fallTime;
            }

            // After Fall completes, no cell in column 0 should be null (spawns fill top).
            for (int y = 0; y < 3; y++)
            {
                Assert.NotNull(board[0, y].GetLayer(NodeLayer.Middle),
                    $"Cell (0,{y}) should be filled after Fall loop");
            }
            Assert.Greater(cmds.Count, 0, "Fall loop should emit at least one command");
        }

        // =====================================================================
        // Refactor invariants (post Position→Start/Target split):
        // every Fall* command must encode exactly one source-cell → destination
        // relationship as (StartPosition, TargetPosition). The View tweens from
        // Start to Target — if these flip or drift, the visual breaks silently.
        // =====================================================================

        [Test]
        public void FallIteration_StraightFall_StartIsOneCellAboveTarget()
        {
            // Single column, single hole at the bottom: tile at y=1 must drop to y=0.
            // Expected Fall command: Start=(0,1), Target=(0,0).
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "r",
                "r",
                ".");

            List<Command> cmds = _fm.FallIteration(board, 0f, 0.1f);

            bool found = false;
            foreach (Command c in cmds)
            {
                if (c.CommandType != Commands.Fall) continue;

                int sx = Mathf.RoundToInt(c.StartPosition.x);
                int sy = Mathf.RoundToInt(c.StartPosition.y);
                int tx = Mathf.RoundToInt(c.TargetPosition.x);
                int ty = Mathf.RoundToInt(c.TargetPosition.y);

                Assert.AreEqual(tx, sx, "straight Fall must keep the same column");
                Assert.AreEqual(1, sy - ty, "straight Fall must drop exactly one row");

                if (sx == 0 && sy == 1 && tx == 0 && ty == 0) found = true;
            }
            Assert.IsTrue(found, "expected straight Fall with Start=(0,1) Target=(0,0)");
        }

        [Test]
        public void FallIteration_FallRight_StartIsUpRightOfTarget()
        {
            // (0,0) empty, (1,1) Red pulled diagonally down-left into (0,0).
            // Expected FallRight command: Start=(1,1), Target=(0,0).
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "Sr",
                ".B");

            List<Command> cmds = _fm.FallIteration(board, 0f, 0.1f);

            bool found = false;
            foreach (Command c in cmds)
            {
                if (c.CommandType != Commands.FallRight) continue;

                int sx = Mathf.RoundToInt(c.StartPosition.x);
                int sy = Mathf.RoundToInt(c.StartPosition.y);
                int tx = Mathf.RoundToInt(c.TargetPosition.x);
                int ty = Mathf.RoundToInt(c.TargetPosition.y);

                Assert.AreEqual(1, sx - tx, "FallRight: Start must be one column to the right of Target");
                Assert.AreEqual(1, sy - ty, "FallRight: Start must be one row above Target");

                if (sx == 1 && sy == 1 && tx == 0 && ty == 0) found = true;
            }
            Assert.IsTrue(found, "expected FallRight with Start=(1,1) Target=(0,0)");
        }

        [Test]
        public void FallIteration_FallLeft_StartIsUpLeftOfTarget()
        {
            // (1,0) empty, (0,1) Red pulled diagonally down-right into (1,0).
            // Expected FallLeft command: Start=(0,1), Target=(1,0).
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rS",
                "B.");

            List<Command> cmds = _fm.FallIteration(board, 0f, 0.1f);

            bool found = false;
            foreach (Command c in cmds)
            {
                if (c.CommandType != Commands.FallLeft) continue;

                int sx = Mathf.RoundToInt(c.StartPosition.x);
                int sy = Mathf.RoundToInt(c.StartPosition.y);
                int tx = Mathf.RoundToInt(c.TargetPosition.x);
                int ty = Mathf.RoundToInt(c.TargetPosition.y);

                Assert.AreEqual(-1, sx - tx, "FallLeft: Start must be one column to the left of Target");
                Assert.AreEqual(1, sy - ty, "FallLeft: Start must be one row above Target");

                if (sx == 0 && sy == 1 && tx == 1 && ty == 0) found = true;
            }
            Assert.IsTrue(found, "expected FallLeft with Start=(0,1) Target=(1,0)");
        }

        [Test]
        public void FallIteration_TopRowRefillSpawn_StartIsAboveBoardTop()
        {
            // 3-tall column with empty top: refill spawn fires at y=h-1=2.
            // Per FallManager L85, refill Start.y must equal m_board.GetLength(1) (=3),
            // not h-1, so the View tween starts off-screen above the visible top.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                ".",
                "r",
                "g");

            int h = board.GetLength(1); // 3
            List<Command> cmds = _fm.FallIteration(board, 0f, 0.1f);

            bool found = false;
            foreach (Command c in cmds)
            {
                if (c.CommandType != Commands.Spawn) continue;

                int sx = Mathf.RoundToInt(c.StartPosition.x);
                int sy = Mathf.RoundToInt(c.StartPosition.y);
                int tx = Mathf.RoundToInt(c.TargetPosition.x);
                int ty = Mathf.RoundToInt(c.TargetPosition.y);

                Assert.AreEqual(sx, tx, "refill Spawn must spawn into the same column");
                Assert.AreEqual(h, sy,
                    $"refill Spawn Start.y must equal board height ({h}) — off-screen anchor for the drop tween");
                Assert.AreEqual(h - 1, ty,
                    $"refill Spawn Target.y must equal top row index ({h - 1})");

                if (sx == 0) found = true;
            }
            Assert.IsTrue(found, "expected one refill Spawn at column 0");
        }
    }
}
