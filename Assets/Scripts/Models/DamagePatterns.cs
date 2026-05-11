
using System.Collections.Generic;
using UnityEngine;

public static class DamagePatterns
{
    private const float ROCKET_STEP = 0.01f;

    public static readonly int[,] DoubleTNTExplosionPattern =
    {
        { 1, 1, 1 ,1, 1, 1, 1, 1, 1},
        { 1, 1, 1 ,1, 1, 1, 1, 1, 1},
        { 1, 1, 1 ,1, 1, 1, 1, 1, 1},
        { 1, 1, 1 ,1, 1, 1, 1, 1, 1},
        { 1, 1, 1 ,1, 1, 1, 1, 1, 1},
        { 1, 1, 1 ,1, 1, 1, 1, 1, 1},
        { 1, 1, 1 ,1, 1, 1, 1, 1, 1},
        { 1, 1, 1 ,1, 1, 1, 1, 1, 1},
        { 1, 1, 1 ,1, 1, 1, 1, 1, 1}
    };


    public static readonly int[,] TNTExplosionPattern = {
        { 1, 1, 1 ,1, 1},
        { 1, 1, 1 ,1, 1},
        { 1, 1, 1 ,1, 1},
        { 1, 1, 1 ,1, 1},
        { 1, 1, 1 ,1, 1}
    };

    public static List<Command> DestroyYourself(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        return new List<Command>
        {
            new Command(position, position, Commands.DestroySelf, timeStamp, NodeLayer.None)
        };
    }

    public static List<Command> DamageYourself(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        return new List<Command>
        {
            new Command(position, position, Commands.TakeDamage, timeStamp, NodeLayer.None)
        };
    }

    // Core methods without start/destroy commands
    private static List<Command> RocketDamageRightCore(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        List<Command> list = new List<Command>();

        for (int x = (int)position.x + 1; x < m_board.GetLength(0); x++)
        {
            float timePassed = (x - (int)position.x) * ROCKET_STEP;
            Damage toDamage = m_board[x, (int)position.y].DamageLayersWith(TileType.HorizontalRocket, damage);

            var result = toDamage?.Invoke(new Vector2Int(x, position.y), damage, m_board, timeStamp + timePassed);
            if (result != null) list.AddRange(result);
        }

        return list;
    }

    private static List<Command> RocketDamageLeftCore(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        List<Command> list = new List<Command>();

        for (int x = (int)position.x - 1; x >= 0; x--)
        {
            float timePassed = ((int)position.x - x) * ROCKET_STEP;
            Damage toDamage = m_board[x, (int)position.y].DamageLayersWith(TileType.HorizontalRocket, damage);

            var result = toDamage?.Invoke(new Vector2Int(x, position.y), damage, m_board, timeStamp + timePassed);
            if (result != null) list.AddRange(result);

        }

        return list;
    }

    private static List<Command> RocketDamageUpCore(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        List<Command> list = new List<Command>();

        for (int y = (int)position.y + 1; y < m_board.GetLength(1); y++)
        {
            float timePassed = (y - (int)position.y) * ROCKET_STEP;
            Damage toDamage = m_board[(int)position.x, y].DamageLayersWith(TileType.VerticalRocket, damage);

            var result = toDamage?.Invoke(new Vector2Int(position.x, y), damage, m_board, timeStamp + timePassed);
            if (result != null) list.AddRange(result);

        }

        return list;
    }

    private static List<Command> RocketDamageDownCore(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        List<Command> list = new List<Command>();

        for (int y = (int)position.y - 1; y >= 0; y--)
        {
            float timePassed = ((int)position.y - y) * ROCKET_STEP;
            Damage toDamage = m_board[(int)position.x, y].DamageLayersWith(TileType.VerticalRocket, damage);

            var result = toDamage?.Invoke(new Vector2Int(position.x, y), damage, m_board, timeStamp + timePassed);
            if (result != null) list.AddRange(result);

        }

        return list;
    }

    public static List<Command> HorizontalRocketDamage(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        List<Command> list = new List<Command>();
        list.Add(new Command(position, position, Commands.Trigger, timeStamp, NodeLayer.Middle));

        // Rocket itself was already nulled by the upstream caller (CollectTriggerIfExists / ClearIfPresent),
        // so DamageLayersWith may return null when Top/Bottom are also empty. Null-safe Invoke.
        Damage toDamage = m_board[position.x, position.y].DamageLayersWith(TileType.HorizontalRocket, 1);
        var selfResult = toDamage?.Invoke(position, damage, m_board, timeStamp);
        if (selfResult != null) list.AddRange(selfResult);

        list.AddRange(RocketDamageLeftCore(position, damage, m_board, timeStamp + ROCKET_STEP));
        list.AddRange(RocketDamageRightCore(position, damage, m_board, timeStamp + ROCKET_STEP));

        return list;
    }

    public static List<Command> VerticalRocketDamage(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        List<Command> list = new List<Command>();
        list.Add(new Command(position, position, Commands.Trigger, timeStamp, NodeLayer.Middle));

        // See HorizontalRocketDamage — same null-safety rationale.
        Damage toDamage = m_board[position.x, position.y].DamageLayersWith(TileType.VerticalRocket, 1);
        var selfResult = toDamage?.Invoke(position, damage, m_board, timeStamp);
        if (selfResult != null) list.AddRange(selfResult);

        list.AddRange(RocketDamageUpCore(position, damage, m_board, timeStamp + ROCKET_STEP));
        list.AddRange(RocketDamageDownCore(position, damage, m_board, timeStamp + ROCKET_STEP));

        return list;
    }

    public static List<Command> TNTDamage(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        List<Command> list = new List<Command>();
        list.Add(new Command(position, position, Commands.Trigger, timeStamp, NodeLayer.Middle));

        // Bug fix: previously `return explosion.Invoke(...)` discarded the Trigger we
        // just added → TNT view never PlayTriggers, never returns to pool → orphan
        // overlapping tiles after the explosion.
        Damage explosion = CustomDamageInstaExplosion(TNTExplosionPattern, new Vector2Int(2, 2));
        var explosionResult = explosion.Invoke(position, damage, m_board, timeStamp);
        if (explosionResult != null) list.AddRange(explosionResult);
        return list;
    }


    public static List<Command> DoubleRocket(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        List<Command> list = new List<Command>();

        // Both rockets are nulled by ProcessTriggerCombination's ClearIfPresent calls before this runs,
        // so DamageLayersWith may return null. Null-safe Invoke; also capture result (was previously dropped).
        Damage toDamage = m_board[position.x, position.y].DamageLayersWith(TileType.VerticalRocket, 1);
        var selfResult = toDamage?.Invoke(position, damage, m_board, timeStamp);
        if (selfResult != null) list.AddRange(selfResult);

        list.AddRange(RocketDamageLeftCore(position, damage, m_board, timeStamp + ROCKET_STEP));
        list.AddRange(RocketDamageRightCore(position, damage, m_board, timeStamp + ROCKET_STEP));
        list.AddRange(RocketDamageUpCore(position, damage, m_board, timeStamp + ROCKET_STEP));
        list.AddRange(RocketDamageDownCore(position, damage, m_board, timeStamp + ROCKET_STEP));

        return list;
    }

    public static List<Command> DoubleTNT(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        Damage doubleTNTDamage = CustomDamageInstaExplosion(DoubleTNTExplosionPattern, new Vector2Int(5, 5));
        return doubleTNTDamage.Invoke(position, damage, m_board, timeStamp);
    }

    public static List<Command> DoubleColorBomb(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        List<Command> list = new List<Command>();

        for (int x = 0; x < m_board.GetLength(0); x++)
        {
            for (int y = 0; y < m_board.GetLength(1); y++)
            {
                List<Command> result = m_board[x, y].DamageLayers(damage)?.Invoke(new Vector2Int(x, y), damage, m_board, timeStamp);
                list.AddRange(result);
            }
        }

        return list;
    }


    public static List<Command> RocketTNT(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
    {
        List<Command> list = new List<Command>();

        // 3 rows
        for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
        {
            int y = (int)position.y + rowOffset;
            if (y < 0 || y >= m_board.GetLength(1)) continue;

            Vector2Int rowPos = new Vector2Int(position.x, y);
            list.AddRange(HorizontalRocketDamage(rowPos, damage, m_board, timeStamp));
        }

        // 3 columns
        for (int colOffset = -1; colOffset <= 1; colOffset++)
        {

            int x = (int)position.x + colOffset;
            if (x < 0 || x >= m_board.GetLength(0)) continue;

            Vector2Int colPos = new Vector2Int(x, position.y);
            list.AddRange(VerticalRocketDamage(colPos, damage, m_board, timeStamp));
        }

        return list;
    }
    public static Damage CustomDamageInstaExplosion(int[,] damages, Vector2Int offset)
    {
        List<Command> ApplyDamage(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
        {
            List<Command> list = new List<Command>();

            Vector2Int start = position - offset;

            for (int y = 0; y < damages.GetLength(1); y++)
            {
                for (int x = 0; x < damages.GetLength(0); x++)
                {
                    int boardX = (int)start.x + x;
                    int boardY = (int)start.y + y;

                    if (boardX < 0 || boardX >= m_board.GetLength(0) ||
                        boardY < 0 || boardY >= m_board.GetLength(1))
                    {
                        continue;
                    }

                    Damage toDamage = m_board[boardX, boardY].DamageLayersWith(TileType.TNT, damages[x, y]);

                    var result = toDamage?.Invoke(new Vector2Int(boardX, boardY), damages[x, y], m_board, timeStamp);
                    if (result != null) list.AddRange(result);
                }
            }
            return list;
        }
        return ApplyDamage;
    }

    public static Damage CustomDamageMatchExplosion(MatchResult matchResult)
    {
        Dictionary<(Vector2Int, TileType), int> totalDamageArea = new Dictionary<(Vector2Int, TileType), int>();

        foreach (Match match in matchResult.matches)
        {
            HashSet<Vector2Int> singleDamageArea = new HashSet<Vector2Int>();

            foreach (Vector2Int pos in match.tilePositions)
            {
                singleDamageArea.Add(pos);
                singleDamageArea.UnionWith(GetAdjacents(pos));
            }

            foreach (Vector2Int pos in singleDamageArea)
            {
                if (totalDamageArea.ContainsKey((pos, match.matchableType)))
                    totalDamageArea[(pos, match.matchableType)]++;
                else
                    totalDamageArea[(pos, match.matchableType)] = 1;
            }
        }

        List<Command> ApplyDamage(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
        {
            List<Command> commands = new List<Command>();
            int m = m_board.GetLength(0);
            int n = m_board.GetLength(1);

            foreach (var kvp in totalDamageArea)
            {
                Vector2Int pos = kvp.Key.Item1;
                TileType matchType = kvp.Key.Item2;
                int totalDamage = kvp.Value;

                NodeModel node = null;
                if(IsInBoundary(pos, m, n))
                {
                    node = m_board[pos.x, pos.y];
                }
                if (node == null) continue;

                Damage toDamage = node.DamageLayersWith(matchType, totalDamage);
                var result = toDamage?.Invoke(pos, totalDamage, m_board, timeStamp);

                if (result != null)
                    commands.AddRange(result);
            }

            return commands;
        }

        return ApplyDamage;
    }
    


    public static Damage ColorBombDamage(TileType targetColor)
    {
        List<Command> ApplyDamage(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
        {
            List<Command> list = new List<Command>();
            list.Add(new Command(position, position, Commands.Trigger, timeStamp, NodeLayer.Middle));

            // Find and destroy all tiles of target color
            for (int x = 0; x < m_board.GetLength(0); x++)
            {
                for (int y = 0; y < m_board.GetLength(1); y++)
                {
                    NodeModel node = m_board[x, y];
                    TileModel middle = node.GetLayer(NodeLayer.Middle);

                    if (middle != null && middle.TileType == targetColor)
                    {
                        Damage toDamage = node.DamageLayer(NodeLayer.Middle, 999);
                        var result = toDamage?.Invoke(new Vector2Int(x, y), damage, m_board, timeStamp);
                        if (result != null) list.AddRange(result);
                    }
                }
            }

            return list;
        }
        return ApplyDamage;
    }

    public static Damage SumDamages(params Damage[] damages)
    {
        List<Command> CombinedDamage(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp)
        {
            List<Command> combinedCommands = new List<Command>();

            foreach (var dmg in damages)
            {
                var result = dmg?.Invoke(position, damage, m_board, timeStamp);
                if (result != null) combinedCommands.AddRange(result);
            }

            return combinedCommands;
        }
        return CombinedDamage;
    }

    private static HashSet<Vector2Int> GetAdjacents(Vector2Int pos)
    {
        HashSet<Vector2Int> adjacents = new HashSet<Vector2Int>();

        int x = pos.x;
        int y = pos.y;

        adjacents.Add(new Vector2Int(x + 1, y));
        adjacents.Add(new Vector2Int(x - 1, y));
        adjacents.Add(new Vector2Int(x, y + 1));
        adjacents.Add(new Vector2Int(x, y - 1));

        return adjacents;


    }

    public static bool IsInBoundary(Vector2Int pos, int m, int n)
    {
        return pos.x >= 0 && pos.y >= 0 && pos.x < m && pos.y < n;
    }
}

public delegate List<Command> Damage(Vector2Int position, int damage, NodeModel[,] m_board, float timeStamp);