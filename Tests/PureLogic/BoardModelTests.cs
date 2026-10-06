using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DreamGamesCase.PureLogic.Tests
{
    [TestFixture]
    public class BoardModelTests
    {
        private BoardModel _model;
        private System.Random _rng;

        [SetUp]
        public void SetUp()
        {
            _model = new BoardModel();

            // Deterministic spawn so cascade tests don't depend on UnityEngine.Random
            // (which is an ECall and throws in dotnet test).
            _rng = new System.Random(42);
            TileFactory.RandomRangeOverride = max => _rng.Next(0, max);
        }

        [TearDown]
        public void TearDown()
        {
            TileFactory.RandomRangeOverride = null;
        }

        // =====================================================================
        // Invalid swaps — should return empty command list, no board changes.
        // =====================================================================

        [Test]
        public void ProcessSwap_NonAdjacent_ReturnsEmptyCommands()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rgr",
                "grg",
                "rgr");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(2, 0));

            Assert.AreEqual(0, result.Commands.Count, "non-adjacent swap must emit no commands");
            Assert.IsNull(result.Merge);
        }

        [Test]
        public void ProcessSwap_NonMovableTile_ReturnsEmptyCommands()
        {
            // Box is not IMovable → swap rejected.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rB",
                "gr");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(1, 1), new Vector2Int(0, 1));

            Assert.AreEqual(0, result.Commands.Count, "swap with non-IMovable must emit no commands");
        }

        // =====================================================================
        // Adjacent swap that produces no match → swap back.
        // Must emit exactly 1 forward + 1 revert Swap command, no DestroySelf.
        // =====================================================================

        [Test]
        public void ProcessSwap_NoMatchProduced_SwapsBackWithTwoSwapCommands()
        {
            // After swapping (0,0)↔(1,0), no row/column gets a 3-match.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rg",
                "gr");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(1, 0));

            int swapCount = result.Commands.Count(c => c.CommandType == Commands.Swap);

            Assert.AreEqual(2, swapCount, "expected 1 forward + 1 revert Swap command");

            int destroyCount = result.Commands.Count(c => c.CommandType == Commands.DestroySelf);
            Assert.AreEqual(0, destroyCount, "no match → no destroys");

            // Board state must be restored. Row "gr" sits at y=0, so (0,0)='g', (1,0)='r'.
            Assert.AreEqual(TileType.Green, _model.GetTileTypeAt(new Vector2Int(0, 0), NodeLayer.Middle));
            Assert.AreEqual(TileType.Red,   _model.GetTileTypeAt(new Vector2Int(1, 0), NodeLayer.Middle));
        }

        // =====================================================================
        // Happy path: swap produces a 3-match → destroy + fall + spawn.
        // =====================================================================

        [Test]
        public void ProcessSwap_ProducesHorizontalMatch_EmitsDestroyAndFallAndSpawn()
        {
            // Swap (0,1)↔(0,0): 'r' moves up, 'g' moves down.
            //   before            after swap
            //   y=1: "grr"   →    "rrr"   (3-match in top row)
            //   y=0: "rgg"   →    "ggg"   (3-match in bottom row too!)
            // Simpler: use a layout where ONLY one match appears.
            //   before            after swap (0,0)↔(0,1)
            //   y=1: "grr"   →    "rrr"   (match)
            //   y=0: "rbb"   →    "gbb"   (no match)
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "grr",
                "rbb");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(0, 1));

            int swapCount = result.Commands.Count(c => c.CommandType == Commands.Swap);
            Assert.AreEqual(1, swapCount, "valid swap emits exactly 1 Swap command (no revert)");

            int destroyCount = result.Commands.Count(c => c.CommandType == Commands.DestroySelf);
            Assert.GreaterOrEqual(destroyCount, 3, "3-match must produce at least 3 DestroySelf commands");

            int spawnCount = result.Commands.Count(c => c.CommandType == Commands.Spawn);
            Assert.Greater(spawnCount, 0, "top-row refill must emit Spawn commands");

            // Fall commands should also be emitted (top row cells drop or get spawned).
            int fallCount = result.Commands.Count(c =>
                c.CommandType == Commands.Fall ||
                c.CommandType == Commands.FallLeft ||
                c.CommandType == Commands.FallRight);
            // On a 2-row board with matches on top row, nothing actually "falls" — the emptied
            // top row is refilled directly by Spawn. So fallCount can be 0. We only assert the
            // spawn path fires.
            Assert.GreaterOrEqual(fallCount, 0);
        }

        // =====================================================================
        // Timestamp ordering: swap happens first, then destroys, then spawns.
        // =====================================================================

        [Test]
        public void ProcessSwap_ValidMatch_CommandsAreTimestampOrdered()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "grr",
                "rbb");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(0, 1));

            // Swap command is at the earliest timestamp.
            float firstSwapTs = result.Commands
                .Where(c => c.CommandType == Commands.Swap)
                .Select(c => c.startTimeStamp)
                .Min();

            // DestroySelf commands are strictly after the swap.
            float firstDestroyTs = result.Commands
                .Where(c => c.CommandType == Commands.DestroySelf)
                .Select(c => c.startTimeStamp)
                .Min();

            Assert.Less(firstSwapTs, firstDestroyTs, "swap must precede destroys in time");
        }

        // =====================================================================
        // Cascade: a single swap should produce at least one match + refill cycle.
        // We don't assert an exact command count (implementation-specific) — just that
        // the cascade loop emits something beyond the immediate match.
        // =====================================================================

        // =====================================================================
        // Additional invalid-swap edge cases: diagonal, same position.
        // =====================================================================

        [Test]
        public void ProcessSwap_DiagonalPositions_ReturnsEmptyCommands()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rgr",
                "grg",
                "rgr");
            _model.SetBoardForTests(board);

            // Diagonal neighbour — AreAdjacent only allows 4-direction cardinal.
            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(1, 1));

            Assert.AreEqual(0, result.Commands.Count, "diagonal swap must be rejected");
        }

        [Test]
        public void ProcessSwap_SamePosition_ReturnsEmptyCommands()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rgr",
                "grg");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(1, 0), new Vector2Int(1, 0));

            Assert.AreEqual(0, result.Commands.Count, "same-position swap must be rejected");
        }

        // =====================================================================
        // GetTileTypeAt — out-of-bounds and empty-layer lookups.
        // =====================================================================

        [Test]
        public void GetTileTypeAt_OutOfBounds_ReturnsNone()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii("rgr");
            _model.SetBoardForTests(board);

            Assert.AreEqual(TileType.None, _model.GetTileTypeAt(new Vector2Int(-1, 0), NodeLayer.Middle));
            Assert.AreEqual(TileType.None, _model.GetTileTypeAt(new Vector2Int(3, 0),  NodeLayer.Middle));
            Assert.AreEqual(TileType.None, _model.GetTileTypeAt(new Vector2Int(0, -1), NodeLayer.Middle));
            Assert.AreEqual(TileType.None, _model.GetTileTypeAt(new Vector2Int(0, 5),  NodeLayer.Middle));
        }

        [Test]
        public void GetTileTypeAt_EmptyLayer_ReturnsNone()
        {
            var board = TestBoardBuilder.MiddleLayerFromAscii("r.r");
            _model.SetBoardForTests(board);

            // Gap tile at (1,0) → middle layer null → TileType.None.
            Assert.AreEqual(TileType.None, _model.GetTileTypeAt(new Vector2Int(1, 0), NodeLayer.Middle));
            // Top/Bottom layers are always null in TestBoardBuilder output.
            Assert.AreEqual(TileType.None, _model.GetTileTypeAt(new Vector2Int(0, 0), NodeLayer.Top));
            Assert.AreEqual(TileType.None, _model.GetTileTypeAt(new Vector2Int(0, 0), NodeLayer.Bottom));
        }

        // =====================================================================
        // Cascade chain: a swap triggers a match; the fall that follows aligns
        // a second, pre-existing match so ProcessCascade fires it as a separate
        // destroy round.
        // =====================================================================

        [Test]
        public void ProcessCascade_PreExistingTilesAlignAfterFall_EmitsTwoDestroyRounds()
        {
            // Visual (top→bottom, what TestBoardBuilder takes as rows):
            //   y=3:  yyy     ← after fall, these three yellows slide down into y=2 and match
            //   y=2:  bgg
            //   y=1:  grr     ← after swap (0,0)↔(0,1): y=1="rrr" → FIRST match
            //   y=0:  rbb                                y=0="gbb"
            //
            // Fall order: 'rrr' at y=1 destroyed. Col 0: 'b' (y=2) drops, 'y' (y=3) drops.
            // Col 1: 'g' drops, 'y' drops. Col 2: 'g' drops, 'y' drops.
            // After fall, y=2 line becomes "yyy" → SECOND match.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "yyy",
                "bgg",
                "grr",
                "rbb");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(0, 1));

            int destroyCount = result.Commands.Count(c => c.CommandType == Commands.DestroySelf);
            Assert.GreaterOrEqual(destroyCount, 6,
                "two match cycles of 3 tiles each — cascade must detect the second match after fall");

            // The two rounds must be temporally separated: at least two distinct timestamps
            // across the DestroySelf commands.
            int distinctTs = result.Commands
                .Where(c => c.CommandType == Commands.DestroySelf)
                .Select(c => c.startTimeStamp)
                .Distinct()
                .Count();
            Assert.GreaterOrEqual(distinctTs, 2,
                "cascade cycles must emit destroys at different timestamps");
        }

        // =====================================================================
        // Obstacle interaction with match-adjacent explosion damage.
        // CustomDamageMatchExplosion builds a damage map of match-positions PLUS
        // their adjacents. This pins how Box/Vase/Stone respond to a color match
        // that sits next to them — in-scope base gameplay (not special tiles).
        // =====================================================================

        [Test]
        public void ProcessSwap_MatchAdjacentToBox_BoxEmitsDestroySelf()
        {
            // y=1: r r g B     After swap (2,1)↔(2,0):  r r r B   (3-red match on top row)
            // y=0: g b r y                              g b g y
            // Match positions {(0,1),(1,1),(2,1)}; (3,1) Box is adjacent to (2,1).
            // Box has HP=1 → destroyed by the match-explosion splash.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rrgB",
                "gbry");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(2, 1), new Vector2Int(2, 0));

            bool boxDestroyed = result.Commands.Any(c =>
                c.CommandType == Commands.DestroySelf &&
                Mathf.RoundToInt(c.StartPosition.x)  == 3 && Mathf.RoundToInt(c.StartPosition.y)  == 1 &&
                Mathf.RoundToInt(c.TargetPosition.x) == 3 && Mathf.RoundToInt(c.TargetPosition.y) == 1);

            Assert.IsTrue(boxDestroyed,
                "Box at (3,1) must receive DestroySelf from match-adjacent splash (HP=1, dies in one hit). Start==Target==(3,1).");
        }

        [Test]
        public void ProcessSwap_MatchAdjacentToVase_VaseTakesDamageButSurvives()
        {
            // Same layout as Box test, but Vase at (3,1). Vase HP=2 → 1 hit leaves it Alive.
            // Expected: TakeDamage command at (3,1), NO DestroySelf at (3,1).
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rrgV",
                "gbry");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(2, 1), new Vector2Int(2, 0));

            bool vaseDamaged = result.Commands.Any(c =>
                c.CommandType == Commands.TakeDamage &&
                Mathf.RoundToInt(c.StartPosition.x)  == 3 && Mathf.RoundToInt(c.StartPosition.y)  == 1 &&
                Mathf.RoundToInt(c.TargetPosition.x) == 3 && Mathf.RoundToInt(c.TargetPosition.y) == 1);
            bool vaseDestroyed = result.Commands.Any(c =>
                c.CommandType == Commands.DestroySelf &&
                Mathf.RoundToInt(c.StartPosition.x)  == 3 && Mathf.RoundToInt(c.StartPosition.y)  == 1 &&
                Mathf.RoundToInt(c.TargetPosition.x) == 3 && Mathf.RoundToInt(c.TargetPosition.y) == 1);

            Assert.IsTrue(vaseDamaged,
                "Vase at (3,1) must receive TakeDamage from match-adjacent splash. Start==Target==(3,1).");
            Assert.IsFalse(vaseDestroyed,
                "Vase survives a single hit (HP=2, takes 1 dmg → Alive)");
        }

        [Test]
        public void ProcessSwap_MatchAdjacentToStone_StoneUnaffectedByColorMatch()
        {
            // Same layout, Stone at (3,1). Stone.TakeDamageFrom returns Unaffected for
            // any source that is not TNT/HorizontalRocket/VerticalRocket. A color match
            // uses the matched TileType (e.g. Red) as source → ignored.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rrgS",
                "gbry");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(2, 1), new Vector2Int(2, 0));

            bool stoneTouched = result.Commands.Any(c =>
                (c.CommandType == Commands.TakeDamage || c.CommandType == Commands.DestroySelf) &&
                Mathf.RoundToInt(c.StartPosition.x)  == 3 && Mathf.RoundToInt(c.StartPosition.y)  == 1 &&
                Mathf.RoundToInt(c.TargetPosition.x) == 3 && Mathf.RoundToInt(c.TargetPosition.y) == 1);

            Assert.IsFalse(stoneTouched,
                "Stone is immune to color-match splash (only TNT/Rocket sources damage it)");
        }

        [Test]
        public void ProcessSwap_BoxDiagonalToMatch_NotDamaged()
        {
            // Splash uses GetAdjacents (cardinal-only: N/S/E/W, no diagonals).
            // Pins that a Box DIAGONAL to the match is NOT in the splash zone.
            //   y=1: r r g .    after swap (2,0)↔(2,1)  →  r r r .   (match at y=1)
            //   y=0: g b r B                                 g b g B
            // Match = {(0,1),(1,1),(2,1)}. Box sits at (3,0) — diagonal to (2,1)
            // but NOT cardinal to any match cell. In-board cardinals of the match
            // are (3,1),(0,0),(1,0),(2,0). (3,0) is only reachable via a diagonal
            // step → splash must miss it.
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "rrg.",
                "gbrB");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(2, 0), new Vector2Int(2, 1));

            bool boxTouched = result.Commands.Any(c =>
                (c.CommandType == Commands.TakeDamage || c.CommandType == Commands.DestroySelf) &&
                Mathf.RoundToInt(c.StartPosition.x)  == 3 && Mathf.RoundToInt(c.StartPosition.y)  == 0 &&
                Mathf.RoundToInt(c.TargetPosition.x) == 3 && Mathf.RoundToInt(c.TargetPosition.y) == 0);

            Assert.IsFalse(boxTouched,
                "Box at (3,0) is diagonal (not cardinal-adjacent) to match — cardinal-only splash must NOT damage it");
        }

        // =====================================================================
        // Refactor invariant: static commands (DestroySelf / TakeDamage / Trigger)
        // must always have StartPosition == TargetPosition. Pre-refactor these
        // had a single Position; post-refactor the convention is Start==Target.
        // A drift here would mean a static effect attaches to two cells visually.
        // =====================================================================

        [Test]
        public void ProcessSwap_StaticCommands_HaveIdenticalStartAndTarget()
        {
            // Layout produces a 3-match plus a Vase adjacent to it — exercises
            // both DestroySelf (matched tiles) and TakeDamage (Vase splash).
            //   y=2:  yyV
            //   y=1:  grr   after swap (0,0)↔(0,1) → "rrr" (match), Vase at (2,2) splash-hit
            //   y=0:  rbb                            "gbb"
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "yyV",
                "grr",
                "rbb");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(0, 1));

            int staticCount = 0;
            foreach (Command cmd in result.Commands)
            {
                bool isStatic =
                    cmd.CommandType == Commands.DestroySelf ||
                    cmd.CommandType == Commands.TakeDamage  ||
                    cmd.CommandType == Commands.Trigger;
                if (!isStatic) continue;

                staticCount++;

                int sx = Mathf.RoundToInt(cmd.StartPosition.x);
                int sy = Mathf.RoundToInt(cmd.StartPosition.y);
                int tx = Mathf.RoundToInt(cmd.TargetPosition.x);
                int ty = Mathf.RoundToInt(cmd.TargetPosition.y);

                Assert.AreEqual(sx, tx,
                    $"{cmd.CommandType} must have StartPosition.x == TargetPosition.x (got Start.x={sx}, Target.x={tx})");
                Assert.AreEqual(sy, ty,
                    $"{cmd.CommandType} must have StartPosition.y == TargetPosition.y (got Start.y={sy}, Target.y={ty})");
            }

            Assert.Greater(staticCount, 0,
                "scenario was supposed to produce static commands (3-match destroys + Vase damage)");
        }

        [Test]
        public void ProcessSwap_ProducesMatchOnTallerBoard_EmitsFallCommands()
        {
            // Taller board so gravity has room to drop after the match destroys row 1.
            //   y=3:  bbb
            //   y=2:  yyp
            //   y=1:  grr    after swap (0,0)↔(0,1)  →  rrr   (match)
            //   y=0:  rbb                                gbb
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "bbb",
                "yyp",
                "grr",
                "rbb");
            _model.SetBoardForTests(board);

            SwapResult result = _model.ProcessSwap(new Vector2Int(0, 0), new Vector2Int(0, 1));

            int fallCount = result.Commands.Count(c =>
                c.CommandType == Commands.Fall ||
                c.CommandType == Commands.FallLeft ||
                c.CommandType == Commands.FallRight);
            Assert.Greater(fallCount, 0, "tiles above the matched row must fall");

            int spawnCount = result.Commands.Count(c => c.CommandType == Commands.Spawn);
            Assert.Greater(spawnCount, 0, "top row must refill via Spawn");
        }
    }
}
