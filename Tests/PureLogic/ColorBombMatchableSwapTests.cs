using NUnit.Framework;
using UnityEngine;
using System.Linq;

namespace DreamGamesCase.PureLogic.Tests
{
    // Scope: ColorBomb + Matchable swap only. Asserts that swapping a CB with
    // an adjacent Matchable activates the bomb against that matchable's color
    // and emits DestroySelf for every tile of that color, with timestamps
    // ordered Swap < Trigger ≤ DestroySelf.
    //
    // CB+Triggerable combinations are out of scope (separate fixture).
    [TestFixture]
    public class ColorBombMatchableSwapTests
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

        // Diagonal red layout — 4 reds at (0,0), (1,1), (2,2), (3,3). No
        // pre-existing 3-match. Swap (0,0)↔(1,0) brings r to (1,0); col 1
        // post-swap is b,g,r,r — only 2 r's, so no incidental match damage
        // contaminates the count of CB-triggered destroys.
        private static NodeModel[,] BuildBoard()
        {
            return TestBoardBuilder.MiddleLayerFromAscii(
                "ybgr",   // y=3
                "bgrb",   // y=2
                "grbg",   // y=1
                "rCgy");  // y=0  (0,0)=Red  (1,0)=ColorBomb
        }

        private const int InitialRedCount = 4;
        private static readonly Vector2Int RedPos = new Vector2Int(0, 0);
        private static readonly Vector2Int ColorBombPos = new Vector2Int(1, 0);
        // SwapMiddleLayers moves CB from pos2=(1,0) into pos1=(0,0), where
        // CollectTriggerIfExists then dispatches the trigger.
        private static readonly Vector2Int CBTriggerPos = new Vector2Int(0, 0);

        // Swap fires AdvanceTime once (TIME_STEP=0.1) before Phase-6 trigger
        // invocation, so the CB trigger and its destroys land at t≈0.1.
        private const float TriggerTimestamp = 0.1f;
        private const float Eps = 1e-3f;

        [Test]
        public void Swap_DestroysEveryRedOnBoard_AtTriggerTimestamp()
        {
            _model.SetBoardForTests(BuildBoard());

            SwapResult result = _model.ProcessSwap(RedPos, ColorBombPos);

            int redDestroysAtTrigger = result.Commands.Count(c =>
                c.CommandType == Commands.DestroySelf
                && c.TileType == TileType.Red
                && Mathf.Abs(c.startTimeStamp - TriggerTimestamp) < Eps);

            Assert.AreEqual(InitialRedCount, redDestroysAtTrigger,
                $"CB-Matchable swap must emit {InitialRedCount} DestroySelf(Red) commands at t≈{TriggerTimestamp:F2} (one per red on the board at trigger time)");
        }

        [Test]
        public void Swap_DoesNotDestroyOtherColors_AtTriggerTimestamp()
        {
            _model.SetBoardForTests(BuildBoard());

            SwapResult result = _model.ProcessSwap(RedPos, ColorBombPos);

            int nonRedDestroysAtTrigger = result.Commands.Count(c =>
                c.CommandType == Commands.DestroySelf
                && c.TileType != TileType.Red
                && c.TileType != TileType.None
                && Mathf.Abs(c.startTimeStamp - TriggerTimestamp) < Eps);

            Assert.AreEqual(0, nonRedDestroysAtTrigger,
                "ColorBomb targeted at Red must not destroy other matchable colors at the trigger instant");
        }

        [Test]
        public void Swap_EmitsSingleTriggerCommand_AtColorBombDestination()
        {
            _model.SetBoardForTests(BuildBoard());

            SwapResult result = _model.ProcessSwap(RedPos, ColorBombPos);

            var triggers = result.Commands.Where(c => c.CommandType == Commands.Trigger).ToList();

            Assert.AreEqual(1, triggers.Count,
                "swap must emit exactly 1 Trigger command for the ColorBomb");

            Command trigger = triggers[0];
            Assert.AreEqual(CBTriggerPos.x, Mathf.RoundToInt(trigger.StartPosition.x),
                "Trigger X must be the CB destination cell");
            Assert.AreEqual(CBTriggerPos.y, Mathf.RoundToInt(trigger.StartPosition.y),
                "Trigger Y must be the CB destination cell");
            Assert.Less(Mathf.Abs(trigger.startTimeStamp - TriggerTimestamp), Eps,
                $"Trigger must fire at t≈{TriggerTimestamp:F2}");
        }

        [Test]
        public void Swap_ColorBombSelfRemovedFromBoard()
        {
            _model.SetBoardForTests(BuildBoard());

            _model.ProcessSwap(RedPos, ColorBombPos);

            TileType atDest = _model.GetTileTypeAt(CBTriggerPos, NodeLayer.Middle);
            Assert.AreNotEqual(TileType.ColorBomb, atDest,
                "ColorBomb must not remain at its destination cell after swap");
        }

        [Test]
        public void Swap_TimestampOrdering_SwapBeforeTrigger_TriggerBeforeOrEqualRedDestroys()
        {
            _model.SetBoardForTests(BuildBoard());

            SwapResult result = _model.ProcessSwap(RedPos, ColorBombPos);

            float swapTs = result.Commands
                .Where(c => c.CommandType == Commands.Swap)
                .Select(c => c.startTimeStamp)
                .DefaultIfEmpty(float.NaN)
                .First();

            float triggerTs = result.Commands
                .Where(c => c.CommandType == Commands.Trigger)
                .Select(c => c.startTimeStamp)
                .DefaultIfEmpty(float.NaN)
                .First();

            Assert.IsFalse(float.IsNaN(swapTs), "expected a Swap command");
            Assert.IsFalse(float.IsNaN(triggerTs), "expected a Trigger command");
            Assert.Less(swapTs, triggerTs, "Swap must precede Trigger in time");

            var redDestroyTs = result.Commands
                .Where(c => c.CommandType == Commands.DestroySelf && c.TileType == TileType.Red)
                .Select(c => c.startTimeStamp)
                .ToList();

            Assert.IsNotEmpty(redDestroyTs,
                "expected at least one DestroySelf(Red) emitted by ColorBomb");

            foreach (float t in redDestroyTs)
            {
                Assert.LessOrEqual(triggerTs - Eps, t,
                    $"every Red destroy must fire at or after the Trigger (got destroy.t={t}, trigger.t={triggerTs})");
            }
        }
    }
}
