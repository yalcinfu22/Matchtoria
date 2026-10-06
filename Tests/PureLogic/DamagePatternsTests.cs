using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

namespace DreamGamesCase.PureLogic.Tests
{
    [TestFixture]
    public class DamagePatternsTests
    {
        // ---- Helpers --------------------------------------------------------

        // Builds an all-Red 5x5 middle layer board, then nulls the middle layer
        // at `triggerPos` to simulate the upstream caller (CollectTriggerIfExists
        // for single rockets, ClearIfPresent for combos) having already removed
        // the rocket from its own cell BEFORE the damage pattern runs.
        private static NodeModel[,] BuildBoardWithNulledCenter(Vector2Int triggerPos)
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rrrrr",
                "rrrrr",
                "rrrrr",
                "rrrrr",
                "rrrrr");
            board[triggerPos.x, triggerPos.y].SetLayer(NodeLayer.Middle, null);
            return board;
        }

        // ---- A3: HorizontalRocketDamage / VerticalRocketDamage NRE ----------

        [Test]
        public void HorizontalRocketDamage_AtPositionWithNullMiddle_DoesNotThrow()
        {
            Vector2Int pos = new Vector2Int(2, 2);
            var board = BuildBoardWithNulledCenter(pos);

            List<Command> cmds = null;
            Assert.DoesNotThrow(() =>
                cmds = DamagePatterns.HorizontalRocketDamage(pos, 1, board, 0f),
                "HorizontalRocketDamage must not NRE when its own Middle was nulled by upstream.");

            Assert.IsNotNull(cmds);
            // Trigger command at center is always emitted first.
            Assert.AreEqual(Commands.Trigger, cmds[0].CommandType);
            // Sweep both directions reaches all 4 other cells in the row.
            int destroyCount = 0;
            foreach (var c in cmds)
                if (c.CommandType == Commands.DestroySelf) destroyCount++;
            Assert.AreEqual(4, destroyCount, "Row sweep should destroy 4 matchables (left+right of center).");
        }

        [Test]
        public void VerticalRocketDamage_AtPositionWithNullMiddle_DoesNotThrow()
        {
            Vector2Int pos = new Vector2Int(2, 2);
            var board = BuildBoardWithNulledCenter(pos);

            List<Command> cmds = null;
            Assert.DoesNotThrow(() =>
                cmds = DamagePatterns.VerticalRocketDamage(pos, 1, board, 0f),
                "VerticalRocketDamage must not NRE when its own Middle was nulled by upstream.");

            Assert.IsNotNull(cmds);
            Assert.AreEqual(Commands.Trigger, cmds[0].CommandType);
            int destroyCount = 0;
            foreach (var c in cmds)
                if (c.CommandType == Commands.DestroySelf) destroyCount++;
            Assert.AreEqual(4, destroyCount, "Column sweep should destroy 4 matchables (up+down of center).");
        }

        // ---- A2: DoubleRocket NRE -------------------------------------------

        [Test]
        public void DoubleRocket_AtPositionWithNullMiddle_DoesNotThrow()
        {
            Vector2Int pos = new Vector2Int(2, 2);
            var board = BuildBoardWithNulledCenter(pos);

            List<Command> cmds = null;
            Assert.DoesNotThrow(() =>
                cmds = DamagePatterns.DoubleRocket(pos, 1, board, 0f),
                "DoubleRocket must not NRE when both rockets were nulled by combo upstream.");

            Assert.IsNotNull(cmds);
            // 4 cores hit row+col cells: 4 horizontal + 4 vertical = 8 destroys.
            int destroyCount = 0;
            foreach (var c in cmds)
                if (c.CommandType == Commands.DestroySelf) destroyCount++;
            Assert.AreEqual(8, destroyCount, "Cross sweep should destroy 4 (row) + 4 (column) matchables.");
        }

        // ---- Existing IsInBoundary tests below ------------------------------

        [Test]
        public void IsInBoundary_OriginOfNonEmptyBoard_True()
        {
            Assert.IsTrue(DamagePatterns.IsInBoundary(new Vector2Int(0, 0), 5, 5));
        }

        [Test]
        public void IsInBoundary_LastCellInside_True()
        {
            Assert.IsTrue(DamagePatterns.IsInBoundary(new Vector2Int(4, 4), 5, 5));
        }

        [Test]
        public void IsInBoundary_XBelowZero_False()
        {
            Assert.IsFalse(DamagePatterns.IsInBoundary(new Vector2Int(-1, 0), 5, 5));
        }

        [Test]
        public void IsInBoundary_YBelowZero_False()
        {
            Assert.IsFalse(DamagePatterns.IsInBoundary(new Vector2Int(0, -1), 5, 5));
        }

        [Test]
        public void IsInBoundary_XAtWidth_False()
        {
            Assert.IsFalse(DamagePatterns.IsInBoundary(new Vector2Int(5, 2), 5, 5));
        }

        [Test]
        public void IsInBoundary_YAtHeight_False()
        {
            Assert.IsFalse(DamagePatterns.IsInBoundary(new Vector2Int(2, 5), 5, 5));
        }

        [Test]
        public void IsInBoundary_NonSquareBoard_RespectsDims()
        {
            // 3 wide, 7 tall
            Assert.IsTrue(DamagePatterns.IsInBoundary(new Vector2Int(2, 6), 3, 7));
            Assert.IsFalse(DamagePatterns.IsInBoundary(new Vector2Int(3, 6), 3, 7));
            Assert.IsFalse(DamagePatterns.IsInBoundary(new Vector2Int(2, 7), 3, 7));
        }
    }
}
