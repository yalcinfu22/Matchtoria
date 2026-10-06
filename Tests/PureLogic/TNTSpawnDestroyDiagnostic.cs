using NUnit.Framework;
using UnityEngine;
using System.Linq;

namespace DreamGamesCase.PureLogic.Tests
{
    // Diagnostic: not a regression test — just dumps DestroySelf commands emitted
    // for a T-shape swap that creates a TNT, so we can verify which positions
    // actually die when matchableType=TNT vs matchableType=color.
    [TestFixture]
    public class TNTSpawnDestroyDiagnostic
    {
        [Test]
        public void TShapeSwap_DumpDestroyCommands()
        {
            var rng = new System.Random(42);
            TileFactory.RandomRangeOverride = max => rng.Next(0, max);

            // T-shape: row y=2 = "rrr.", column x=1 has reds at y=0,y=1 already.
            // Pre-swap: swap the (1,2) position into a 'r' to complete the T.
            // Layout (top-down):
            //   y=3: . . . .
            //   y=2: r b r .   (need to swap b at (1,2) for an r)
            //   y=1: . r . .
            //   y=0: . r . .
            // Swap (1,2) ↔ (0,2): wait that won't work. Let me set it up more directly.
            //
            // Actually use a layout where after swap (1,2)↔(2,2) we get T:
            //   y=2: r r r .  ← horizontal
            //   y=1: . r . .  ← part of vertical
            //   y=0: . r . .  ← part of vertical
            // Pre-swap layout: r r ? . where ? at (2,2) is non-r so swap brings it.
            // Let's pre-place rrr.  ., r , ., . at y=1, and ., r, ., . at y=0.
            // Pre-swap (1,2)↔(2,2): need (1,2)=non-r, (2,2)=r.
            // So pre: y=2: r,?,r,. — but that's already T-ish horizontally.
            // Easier: pre y=2: r,b,r,.  swap b@(1,2) with r@(1,3)? Then need r@(1,3) pre.
            //
            // Simplest setup: pre-swap layout where one swap completes T.
            //   y=3: . r . .       ← red at (1,3)
            //   y=2: r b r .       ← swap (1,2)<->(1,3) brings r to (1,2)
            //   y=1: . r . .
            //   y=0: . r . .
            // After swap: T at (0,2),(1,2),(2,2) horizontal + (1,0),(1,1),(1,2),(1,3) vertical.
            // That's 4-vertical + 3-horizontal. Hmm 4-v makes a Rocket not TNT.
            //
            // Let me make it cleaner: 3-h + 3-v, intersection at corner.
            // L-shape (corner at (0,2)):
            //   y=3: . . . .
            //   y=2: r r r .   ← horizontal at y=2
            //   y=1: r . . .
            //   y=0: r . . .
            // To get this from a swap, pre: replace (0,2) with non-r, swap.
            //   y=2: b r r .   pre
            //   y=1: r . . .
            //   y=0: r . . .
            //   y=3: r . . .   ← swap (0,2)<->(0,3): brings r to (0,2)
            var board = TestBoardBuilder.MiddleLayerFromAscii(
                "r...",   // y=3
                "brr.",   // y=2
                "r...",   // y=1
                "r...");  // y=0

            var model = new BoardModel();
            model.SetBoardForTests(board);

            SwapResult result = model.ProcessSwap(new Vector2Int(0, 2), new Vector2Int(0, 3));

            TestContext.WriteLine($"Total commands: {result.Commands.Count}");
            foreach (var c in result.Commands)
            {
                TestContext.WriteLine($"  t={c.startTimeStamp:F3} {c.CommandType} pos=({(int)c.StartPosition.x},{(int)c.StartPosition.y})->({(int)c.TargetPosition.x},{(int)c.TargetPosition.y}) layer={c.Layer} type={c.TileType}");
            }

            var destroys = result.Commands
                .Where(c => c.CommandType == Commands.DestroySelf)
                .Select(c => $"({(int)c.StartPosition.x},{(int)c.StartPosition.y})={c.TileType}")
                .ToList();

            TestContext.WriteLine($"\nDestroySelf commands: {string.Join(", ", destroys)}");
            TestContext.WriteLine($"DestroySelf count: {destroys.Count}");

            TileFactory.RandomRangeOverride = null;

            // Always pass — we just want the diagnostic output.
            Assert.Pass($"Destroys: [{string.Join(", ", destroys)}]");
        }
    }
}
