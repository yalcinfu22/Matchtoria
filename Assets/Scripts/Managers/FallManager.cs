using System.Collections.Generic;
using UnityEngine;

public class FallManager
{
    public static FallManager Instance;

    public List<Command> FallIteration(NodeModel[,] m_board, float timeStamp, float fallTime)
    {
        List<Command> commands = new List<Command>();

        // Priming pass: every IMovable enters this iter as IsMoving=false. Below,
        // any tile we PHYSICALLY relocate (or spawn) flips back to true. A tile that
        // stays in place across one full FallIteration is "settled" — match-eligible
        // by the time the next FindMatches runs. Step 1 timestamp fix is the primary
        // guard; this flag is a fail-safe shield so MatchManager never includes a
        // mid-tween tile in a match even if a future timing change re-introduces a
        // window. See SUGGESTIONS.md "Cascade DestroySelf early-fire".
        int width = m_board.GetLength(0);
        int height = m_board.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                TileModel t = m_board[x, y].GetLayer(NodeLayer.Middle);
                if (t is IMovable mv) mv.IsMoving = false;
            }
        }

        for (int y = 0; y < height - 1; y++)
        {
            // Right-to-left scan: when right-diag pulls from (x+1, y+1), (x+1, y)
            // has already been visited so any straight-fall it wanted is resolved.
            for(int x = width - 1; x >= 0; x--)
            {
                NodeModel node = m_board[x, y];

                if(!node.HasTileAt(NodeLayer.Middle))
                {
                    if (m_board[x, y + 1].IsTileMovable())
                    {
                        TileModel pulled = m_board[x, y + 1].FallTile();
                        node.SetLayer(NodeLayer.Middle, pulled);
                        if (pulled is IMovable mv) mv.IsMoving = true;
                        commands.Add(new Command(new Vector2(x, y + 1), new Vector2(x, y), Commands.Fall, timeStamp, NodeLayer.Middle));
                    }
                    else if (m_board[x, y + 1].HasTileAt(NodeLayer.Middle))
                    {
                        // (x, y+1) is a non-movable obstacle (Stone/Box) — side-fall allowed.
                        if (x + 1 < width && m_board[x + 1, y + 1].IsTileMovable())
                        {
                            TileModel pulled = m_board[x + 1, y + 1].FallTile();
                            node.SetLayer(NodeLayer.Middle, pulled);
                            if (pulled is IMovable mv) mv.IsMoving = true;
                            commands.Add(new Command(new Vector2(x + 1, y + 1), new Vector2(x, y), Commands.FallRight, timeStamp, NodeLayer.Middle));
                        }
                        else if (x - 1 >= 0 && m_board[x - 1, y + 1].IsTileMovable())
                        {
                            TileModel pulled = m_board[x - 1, y + 1].FallTile();
                            node.SetLayer(NodeLayer.Middle, pulled);
                            if (pulled is IMovable mv) mv.IsMoving = true;
                            commands.Add(new Command(new Vector2(x - 1, y + 1), new Vector2(x, y), Commands.FallLeft, timeStamp, NodeLayer.Middle));
                        }
                    }
                }
            }
        }

        // Top-row refill spawns are emitted at timeStamp + COMMAND_TIME_BUMP so
        // BoardView processes the Fall(x, h-1)→(x, h-2) BEFORE Spawn(x, h)→(x, h-1).
        // Both touch cell (x, h-1) — Fall as source, Spawn as target — and without
        // an explicit timestamp gap the View would rely on LINQ stable-sort
        // insertion order to disambiguate. ~1 ms is imperceptible visually.
        float spawnTime = timeStamp + GameConfig.COMMAND_TIME_BUMP;
        for(int x = 0; x < width; x++)
        {
            NodeModel node = m_board[x, height - 1];

            if (!node.HasTileAt(NodeLayer.Middle))
            {
                TileModel tile = TileFactory.CreateTile("random");
                if (tile is IMovable mv) mv.IsMoving = true;
                node.SetLayer(NodeLayer.Middle, tile);

                commands.Add(new Command(new Vector2(x, height), new Vector2(x, height - 1), Commands.Spawn, spawnTime, NodeLayer.Middle, tile.TileType));
            }
        }

        return commands;
    }

}
