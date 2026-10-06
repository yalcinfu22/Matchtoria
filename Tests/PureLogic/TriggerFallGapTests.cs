using NUnit.Framework;
using UnityEngine;
using System.Linq;

namespace DreamGamesCase.PureLogic.Tests
{
    // Pins GameConfig.TRIGGER_FALL_GAP behavior: when Phase-3 collected a
    // trigger (TNT/Rocket/ColorBomb), the first cascade fall must start at
    // least GAP seconds after the last trigger-cascade command. Match-only
    // swaps must NOT incur this pause.
    [TestFixture]
    public class TriggerFallGapTests
    {
        private BoardModel _model;
        private System.Random _rng;
        private const float GAP = GameConfig.TRIGGER_FALL_GAP;
        private const float Eps = 1e-3f;

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

        // Cascade-start = first tile movement after the gap. Includes Fall*
        // (real tile drops) and top-row Spawn (start.y == boardHeight). The
        // in-place Phase-8 special-tile Spawn has start.y < boardHeight and
        // is intentionally excluded.
        private static bool IsCascadeStart(Command c, int boardHeight) =>
            c.CommandType == Commands.Fall
            || c.CommandType == Commands.FallLeft
            || c.CommandType == Commands.FallRight
            || (c.CommandType == Commands.Spawn
                && Mathf.RoundToInt(c.StartPosition.y) == boardHeight);

        // Trigger-cascade = anything emitted by the explosion: the Trigger
        // command itself plus every DestroySelf/TakeDamage produced by its
        // damage spread.
        private static bool IsTriggerCascadeCmd(Commands t) =>
            t == Commands.Trigger
            || t == Commands.DestroySelf
            || t == Commands.TakeDamage;

        private static (float lastTriggerBeforeFall, float firstFall) Extract(SwapResult r, int boardHeight)
        {
            float firstFall = r.Commands
                .Where(c => IsCascadeStart(c, boardHeight))
                .Select(c => c.startTimeStamp)
                .DefaultIfEmpty(float.NaN)
                .Min();

            // Constrain to cmds BEFORE first fall — cascade match damage
            // emits more DestroySelf cmds AFTER falls, which would
            // otherwise skew the "last trigger" anchor.
            float lastTrigger = r.Commands
                .Where(c => IsTriggerCascadeCmd(c.CommandType)
                            && c.startTimeStamp < firstFall)
                .Select(c => c.startTimeStamp)
                .DefaultIfEmpty(float.NaN)
                .Max();

            return (lastTrigger, firstFall);
        }

        // =====================================================================
        // TNT swap — 5×5 explosion. Last damage ts ≈ 0.1 (insta-blast),
        // first cascade tile movement must be ≥ 0.1 + 0.7.
        // =====================================================================
        [Test]
        public void TNTSwap_FirstFallStartsAtLeastGapAfterLastTriggerCommand()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "ybgr",   // y=3
                "bgrb",   // y=2
                "gybT",   // y=1: T at (3,1)
                "rygy");  // y=0
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(2, 1), new Vector2Int(3, 1));
            var (lastTrigger, firstFall) = Extract(result, board.GetLength(1));

            Assert.IsFalse(float.IsNaN(lastTrigger), "expected at least one trigger-cascade command");
            Assert.IsFalse(float.IsNaN(firstFall),   "expected at least one cascade-start command");
            Assert.GreaterOrEqual(firstFall - lastTrigger, GAP - Eps,
                $"first cascade move (t={firstFall:F4}) must come ≥ {GAP}s after last trigger cmd (t={lastTrigger:F4})");
        }

        // =====================================================================
        // Rocket swap — HRocket spreads along its row at ROCKET_STEP=0.01s
        // intervals. Same gap requirement after the last spread cmd.
        // =====================================================================
        [Test]
        public void RocketSwap_FirstFallStartsAtLeastGapAfterLastTriggerCommand()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "ybgr",   // y=3
                "bgrb",   // y=2
                "gybH",   // y=1: H (HRocket) at (3,1)
                "rygy");  // y=0
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(2, 1), new Vector2Int(3, 1));
            var (lastTrigger, firstFall) = Extract(result, board.GetLength(1));

            Assert.IsFalse(float.IsNaN(lastTrigger), "expected at least one trigger-cascade command");
            Assert.IsFalse(float.IsNaN(firstFall),   "expected at least one cascade-start command");
            Assert.GreaterOrEqual(firstFall - lastTrigger, GAP - Eps,
                $"first cascade move (t={firstFall:F4}) must come ≥ {GAP}s after last trigger cmd (t={lastTrigger:F4})");
        }

        // =====================================================================
        // ColorBomb + Matchable swap — relies on the SetTargetColor fix.
        // CB blast emits Trigger + N DestroySelf at the same timestamp.
        // =====================================================================
        [Test]
        public void ColorBombMatchableSwap_FirstFallStartsAtLeastGapAfterLastTriggerCommand()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "ybgr",   // y=3
                "bgrb",   // y=2
                "grbg",   // y=1
                "rCgy");  // y=0  (0,0)=Red  (1,0)=ColorBomb
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(1, 0));
            var (lastTrigger, firstFall) = Extract(result, board.GetLength(1));

            Assert.IsFalse(float.IsNaN(lastTrigger), "expected at least one trigger-cascade command");
            Assert.IsFalse(float.IsNaN(firstFall),   "expected at least one cascade-start command");
            Assert.GreaterOrEqual(firstFall - lastTrigger, GAP - Eps,
                $"first cascade move (t={firstFall:F4}) must come ≥ {GAP}s after last trigger cmd (t={lastTrigger:F4})");
        }

        // =====================================================================
        // Negative — match-only swap (no triggerable involved) must NOT
        // pay the gap. First cascade move comes immediately (≪ GAP) after
        // the match destroys.
        // =====================================================================
        [Test]
        public void MatchOnlySwap_NoGapApplied_FirstFallStartsImmediatelyAfterDestroy()
        {
            // Pre-swap: row y=0 = "rrb". Swap (2,0)b ↔ (2,1)r → row y=0 = "rrr"
            // 3-match. Above tiles fall into the gap.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rby",   // y=2
                "gyr",   // y=1
                "rrb");  // y=0
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(2, 0), new Vector2Int(2, 1));
            var (lastTrigger, firstFall) = Extract(result, board.GetLength(1));

            Assert.IsFalse(float.IsNaN(lastTrigger), "expected at least one DestroySelf from the match");
            Assert.IsFalse(float.IsNaN(firstFall),   "expected at least one cascade-start command");
            Assert.Less(firstFall - lastTrigger, GAP - Eps,
                $"match-only swap must NOT pay the trigger-fall gap; got delta={firstFall - lastTrigger:F4}, GAP={GAP}");
        }
    }
}
