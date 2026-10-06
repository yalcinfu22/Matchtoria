using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DreamGamesCase.PureLogic.Tests
{
    // Cross-cutting timing invariant: when a tile that JUST received a Fall command
    // gets matched in the cascade, its DestroySelf must not fire before the Fall
    // animation finishes. Otherwise the View destroys a tile mid-tween and the
    // player sees an early disappear / ghost. See chat 2026-04-29 analysis.
    //
    // Invariant (per Command):
    //   For any DestroySelf at cell C with timestamp T_d, and any Fall* / Spawn
    //   command targeting cell C with startTimeStamp T_f s.t. T_f <= T_d, we require:
    //       T_d  >=  T_f + FALL_TIME       (DestroySelf at OR after fall animation end)
    //
    // We treat T_d == T_f + FALL_TIME (within float epsilon) as PASS, because at that
    // exact moment the fall tween has just delivered the tile to its target cell.
    [TestFixture]
    public class CascadeTimingTests
    {
        private BoardModel _model;
        private System.Random _rng;

        private const float FALL_TIME = GameConfig.FALL_TIME;
        private const float EPS       = 1e-4f;

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
        // RED-then-GREEN regression: cascade match destroys must wait for fall
        // animation to end. Pre-fix this fails because:
        //   * FallManager emits Fall cmd with startTimeStamp = currentTime + FALL_TIME
        //     (a +fallTime SHIFT — semantically meaning "fall starts a fallTime later")
        //   * BoardModel.AdvanceTime() advances currentTime by TIME_STEP (0.1f)
        //   * Net: cascade DestroySelf gets emitted at the SAME timestamp the Fall
        //     animation is starting, not when it ends.
        //
        // Setup walk-through (4×4 board, y=3 top):
        //   y=3:  bbb        y=3:  ...           y=3:  ggg(refill)
        //   y=2:  yyp        y=2:  yyp           y=2:  bbb  ← cascade match!
        //   y=1:  rrr  ─→    y=1:  ...     ─→    y=1:  yyp
        //   y=0:  gbb        y=0:  gbb           y=0:  gbb
        //   (post-swap)      (post-match-y=1)    (post-fall)
        //
        // The bbb at y=2 in the post-fall snapshot becomes a 3-match → cascade
        // DestroySelf for those three cells. Each of those cells received a Fall
        // command (b dropped from y=3 to y=2). DestroySelf must come AT OR AFTER
        // (Fall startTime + FALL_TIME).
        // =====================================================================

        [Test]
        public void ProcessSwap_CascadeMatch_DestroyDoesNotFireBeforeFallEnds()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "bbb",
                "yyp",
                "grr",
                "rbb");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(0, 1));

            AssertNoEarlyDestroy(result.Commands);
        }

        // Reverse layout — vertical fall after horizontal-bottom-row match.
        // Provides a second independent scenario; same invariant must hold.
        [Test]
        public void ProcessSwap_StackedColumnCascade_DestroyDoesNotFireBeforeFallEnds()
        {
            // y=4 top. Swap (0,0)<->(1,0) makes y=0 = "rrr" (3-match).
            // After destroy, column 0 collapses; if pieces above happen to align
            // they cascade. We don't predict specific cascade — we just enforce
            // the invariant whatever cascade the RNG/board produces.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "byp",
                "byp",
                "rgg",
                "ryy",
                "rgr");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(1, 0));

            AssertNoEarlyDestroy(result.Commands);
        }

        // ---------------------------------------------------------------------
        private static void AssertNoEarlyDestroy(List<Command> commands)
        {
            // Collect Fall* / Spawn commands by TARGET cell with startTimeStamp.
            // (Fall variants and Spawn all carry an animation that ends at start + FALL_TIME.)
            var movesByTarget = new Dictionary<Vector2Int, List<float>>();
            foreach (Command c in commands)
            {
                bool isMove =
                    c.CommandType == Commands.Fall ||
                    c.CommandType == Commands.FallLeft ||
                    c.CommandType == Commands.FallRight ||
                    c.CommandType == Commands.Spawn;
                if (!isMove) continue;

                Vector2Int target = new Vector2Int(
                    Mathf.RoundToInt(c.TargetPosition.x),
                    Mathf.RoundToInt(c.TargetPosition.y));

                if (!movesByTarget.TryGetValue(target, out var list))
                {
                    list = new List<float>();
                    movesByTarget[target] = list;
                }
                list.Add(c.startTimeStamp);
            }

            // For each DestroySelf, verify no Fall into its cell finishes after it.
            foreach (Command c in commands)
            {
                if (c.CommandType != Commands.DestroySelf) continue;

                Vector2Int pos = new Vector2Int(
                    Mathf.RoundToInt(c.StartPosition.x),
                    Mathf.RoundToInt(c.StartPosition.y));

                if (!movesByTarget.TryGetValue(pos, out var fallStarts)) continue;

                foreach (float fStart in fallStarts)
                {
                    // Only Falls happening at-or-before this destroy matter:
                    // a future Fall (T_f > T_d) belongs to a later cascade slice
                    // and the tile destroyed at T_d is a different physical tile.
                    if (fStart > c.startTimeStamp + EPS) continue;

                    float fallEnd = fStart + FALL_TIME;
                    Assert.GreaterOrEqual(c.startTimeStamp, fallEnd - EPS,
                        $"DestroySelf at {pos} fires t={c.startTimeStamp:F4}, but a Fall→{pos} " +
                        $"started at t={fStart:F4} and only finishes at t={fallEnd:F4}. " +
                        $"This is the cascade early-destroy bug — tile gets killed mid-tween.");
                }
            }
        }
    }
}
