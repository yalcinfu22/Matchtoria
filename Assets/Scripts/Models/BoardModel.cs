using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BoardModel
{
    private FallManager m_fallManager;
    private MatchManager m_matchManager;

    private int m_width;
    private int m_height;

    private NodeModel[,] m_board;

    private float m_currentTimeStamp = 0f;
    private const float TIME_STEP = 0.1f;
    private const float FALL_TIME = GameConfig.FALL_TIME;
    // Used by BumpTimeStampPast so a DestroySelf and the Fall filling the
    // destroyed cell don't share a timestamp — without it View's sorted
    // iteration would rely on LINQ stable-sort insertion order to keep destroy
    // before fall. Imperceptible (~1 ms) but explicit.
    private const float BUMP_GAP = GameConfig.COMMAND_TIME_BUMP;
    public BoardModel()
    {
        m_fallManager = new FallManager();
        m_matchManager = new MatchManager();
    }

    // Test seam: bypasses LevelData/JSON loading so tests can inject ASCII-built boards.
    public void SetBoardForTests(NodeModel[,] board)
    {
        m_board = board;
        m_width = board.GetLength(0);
        m_height = board.GetLength(1);
    }

    // Test seam: returns TileType.None for out-of-bounds or empty layer instead of throwing.
    public TileType GetTileTypeAt(Vector2Int pos, NodeLayer layer)
    {
        if (pos.x < 0 || pos.x >= m_width || pos.y < 0 || pos.y >= m_height)
            return TileType.None;

        TileModel tile = m_board[pos.x, pos.y].GetLayer(layer);
        return tile == null ? TileType.None : tile.TileType;
    }

    public void BuildBoard(LevelData levelData)
    {
        m_width = levelData.grid_width;
        m_height = levelData.grid_height;
        m_board = new NodeModel[m_width, m_height];

        for (int y = 0; y < m_height; y++)
        {
            for (int x = 0; x < m_width; x++)
            {
                int index = y * m_width + x;

                TileModel top = TileFactory.CreateTile(levelData.grid_top[index]);
                TileModel middle = TileFactory.CreateTile(levelData.grid_middle[index]);
                TileModel bottom = TileFactory.CreateTile(levelData.grid_bottom[index]);

                m_board[x, y] = new NodeModel(top, middle, bottom);
            }
        }
    }
    public SwapResult ProcessSwap(Vector2Int pos1, Vector2Int pos2)
    {
        List<Command> allCommands = new List<Command>();
        MergeInfo? mergeInfo = null;
        m_currentTimeStamp = 0f;

#if UNITY_EDITOR
        Debug.Log($"[Model] ProcessSwap ENTER pos1={pos1}({GetTileTypeAt(pos1, NodeLayer.Middle)}) pos2={pos2}({GetTileTypeAt(pos2, NodeLayer.Middle)})");
#endif

        if (!IsValidSwap(pos1, pos2))
        {
#if UNITY_EDITOR
            Debug.Log($"[Model] ProcessSwap INVALID — not adjacent or not movable, EXIT 0 cmds");
#endif
            return new SwapResult { Commands = allCommands, Merge = null };
        }

        TileModel tile1 = m_board[pos1.x, pos1.y].GetLayer(NodeLayer.Middle);
        TileModel tile2 = m_board[pos2.x, pos2.y].GetLayer(NodeLayer.Middle);

        // Check merge BEFORE swapping
        bool isMerge = (tile1 is ITriggerable) && (tile2 is ITriggerable);

        if (isMerge)
        {
            // DESCOPED — view phase will not implement Merge. Two-triggerable
            // merge is unsupported in current scope. If this path fires at runtime,
            // we want to know so we can decide: keep + implement, or cut the block
            // entirely. See docs/SUGGESTIONS.md A1.
            Debug.LogWarning($"[BoardModel] Descoped merge path hit: pos1={pos1} ({tile1.TileType}), pos2={pos2} ({tile2.TileType}). View has no Merge handler — visual will likely break.");

            // Build merge info for the view
            mergeInfo = new MergeInfo
            {
                Position = pos2, // merge happens at destination
                SourceType1 = tile1.TileType,
                SourceType2 = tile2.TileType,
                TimeStamp = m_currentTimeStamp
            };

            // Phase 1: Slide tile1 into tile2's position (not a swap!)
            allCommands.Add(new Command(pos1, pos2, Commands.Move, m_currentTimeStamp, NodeLayer.Middle));
            AdvanceTime();

            /* 
             * Setting triggerables to null is delegated to ProcessTriggerCombination
             */

            // Merge both visually (single command carries both endpoints)
            allCommands.Add(new Command(pos1, pos2, Commands.Merge, m_currentTimeStamp, NodeLayer.Middle));

            AdvanceTime();

            // Apply damage combo 
            List<Command> combinationCommands = ProcessTriggerCombination(
                tile1, tile2, pos1, pos2, m_currentTimeStamp);

            if (combinationCommands != null)
                allCommands.AddRange(combinationCommands);

            // Cascade
            allCommands.AddRange(ProcessCascade());

            return new SwapResult { Commands = allCommands, Merge = mergeInfo };
        }

#if UNITY_EDITOR
        DumpBoard("PRE-SWAP");
#endif

        // CB + Matchable: Matchable is not ITriggerable, so isMerge is false
        // and ProcessTriggerCombination's CB+Matchable branch never runs.
        // Set the target color here so Phase-3 CollectTriggerIfExists picks
        // up a properly configured trigger; otherwise m_targetColor stays
        // None and ColorBombDamage destroys nothing.
        if (tile1 is ColorBomb cbA && tile2 is IMatchable)
            cbA.SetTargetColor(tile2.TileType);
        else if (tile2 is ColorBomb cbB && tile1 is IMatchable)
            cbB.SetTargetColor(tile1.TileType);

        // Phase 1: Swap
        SwapMiddleLayers(pos1, pos2);
        allCommands.Add(new Command(pos1, pos2, Commands.Swap, m_currentTimeStamp, NodeLayer.Middle));
        AdvanceTime();

        // Phase 2: Initialize fall command list
        List<Command> cascadeCommands = new List<Command>();

        // Phase 3: Collect individual triggers (not a combination)
        Damage trigger1 = CollectTriggerIfExists(pos2); // tile1 is now at pos2
        Damage trigger2 = CollectTriggerIfExists(pos1); // tile2 is now at pos1
        bool hasTriggers = trigger1 != null || trigger2 != null;

        // Phase 4: Find matches
        MatchResult matchResult = m_matchManager.FindMatchesAfterSwap(NodeLayer.Middle, m_board, pos1, pos2);
        bool hasMatches = matchResult.matches.Count > 0;

#if UNITY_EDITOR
        Debug.Log($"[Model] triggers: t1={(trigger1!=null)} t2={(trigger2!=null)} matches={matchResult.matches.Count}");
#endif

        // If no triggers and no matches, swap back (invalid swap)
        if (!hasTriggers && !hasMatches)
        {
            SwapMiddleLayers(pos1, pos2);
            allCommands.Add(new Command(pos1, pos2, Commands.Swap, m_currentTimeStamp, NodeLayer.Middle));
#if UNITY_EDITOR
            Debug.Log($"[Model] REVERT (no triggers, no matches) — emitted {allCommands.Count} cmds");
#endif
            return new SwapResult { Commands = allCommands, Merge = null };
        }

        // Phase 5: Mark matched tiles (protection from triggers)
        MarkMatchedTiles(matchResult);

#if UNITY_EDITOR
        DumpBoard("POST-MARK");
#endif

        // Phase 6: Apply triggers  (tiles protected)
        if (trigger1 != null)
        {
            List<Command> triggerCommands = trigger1.Invoke(pos2, 1, m_board, m_currentTimeStamp);
            if (triggerCommands != null) allCommands.AddRange(triggerCommands);
        }

        if (trigger2 != null)
        {
            List<Command> triggerCommands = trigger2.Invoke(pos1, 1, m_board, m_currentTimeStamp);
            if (triggerCommands != null) allCommands.AddRange(triggerCommands);
        }

        // Phase 7: Apply match damage
        if (hasMatches)
        {
            Damage matchDamage = DamagePatterns.CustomDamageMatchExplosion(matchResult);
            List<Command> matchCommands = matchDamage.Invoke(Vector2Int.zero, 1, m_board, m_currentTimeStamp);
            if (matchCommands != null)
            {
                // Stagger non-trigger commands so the swap-destination tile dies
                // on arrival; chain destroys + adjacent damage pop a beat later.
                // Cascade matches don't get this — only swap-induced matches have
                // a meaningful "trigger" cell.
                Vector2Int triggerPos = pos2;
                for (int i = 0; i < matchCommands.Count; i++)
                {
                    Command c = matchCommands[i];
                    int cx = Mathf.RoundToInt(c.StartPosition.x);
                    int cy = Mathf.RoundToInt(c.StartPosition.y);
                    if (cx != triggerPos.x || cy != triggerPos.y)
                    {
                        c.startTimeStamp += GameConfig.MATCH_TRIGGER_STAGGER;
                        matchCommands[i] = c;
                    }
                }
                allCommands.AddRange(matchCommands);
            }
        }

        BumpTimeStampPast(allCommands);

#if UNITY_EDITOR
        DumpBoard("POST-MATCH-DAMAGE");
#endif

        // Phase 8: Spawn special tiles
        List<Command> spawnCommands = SpawnSpecialTiles(matchResult);
        allCommands.AddRange(spawnCommands);

#if UNITY_EDITOR
        Debug.Log($"[Model] SpawnSpecials: emitted {spawnCommands.Count} spawn cmd(s)");
        DumpBoard("POST-SPAWN");
#endif

        // Pause after triggerable explosion before tiles start falling.
        // Match-only swaps skip this — only triggers earn the beat.
        if (hasTriggers)
            m_currentTimeStamp += GameConfig.TRIGGER_FALL_GAP;

        // Phase 9: Fall and cascade loop
        cascadeCommands = ProcessCascade();
        allCommands.AddRange(cascadeCommands);

#if UNITY_EDITOR
        DumpBoard("POST-CASCADE");
        Debug.Log($"[Model] ProcessSwap EXIT total={allCommands.Count} cmds, t={m_currentTimeStamp:F2}");
        LogCommandBreakdown(allCommands);
#endif

        return new SwapResult { Commands = allCommands, Merge = null };
    }
    private List<Command> ProcessCascade()
    {
        List<Command> allCommands = new List<Command>();
        int safety = 0;

        while (true)
        {
            if (++safety > 200)
            {
                Debug.LogError("ProcessCascade aborted by safety limit");
                break;
            }

#if UNITY_EDITOR
            DumpBoard($"CASCADE iter={safety}");
#endif

            // Single fall iteration
            List<Command> fallCommands = m_fallManager.FallIteration(m_board, m_currentTimeStamp, FALL_TIME);

            if (fallCommands.Count > 0)
            {
                allCommands.AddRange(fallCommands);
                // Advance to the moment the fall animation actually FINISHES in view.
                // FallManager emits commands at startTimeStamp = currentTime (animation
                // start), so the next match-check / destroy must be scheduled at
                // currentTime + FALL_TIME — otherwise the destroy callback fires while
                // the tile is still tweening into place. (See CascadeTimingTests.)
                m_currentTimeStamp += FALL_TIME;

#if UNITY_EDITOR
                foreach (var c in fallCommands)
                    Debug.Log($"[Model] iter={safety} {c.CommandType} {V2I(c.StartPosition)}→{V2I(c.TargetPosition)} t={c.startTimeStamp:F2} L={c.Layer}");
#endif
            }

            // Check for matches after EVERY fall step (even if tiles still falling)
            MatchResult cascadeResult = m_matchManager.FindMatches(NodeLayer.Middle, m_board);

            if (cascadeResult.matches.Count > 0)
            {
                // Process matches immediately
                MarkMatchedTiles(cascadeResult);

#if UNITY_EDITOR
                var matchedCells = cascadeResult.matches
                    .SelectMany(m => m.tilePositions)
                    .Select(p => $"({p.x},{p.y})");
                Debug.Log($"[Model] iter={safety} CASCADE-MATCH count={cascadeResult.matches.Count} cells=[{string.Join(",", matchedCells)}]");
#endif

                Damage matchDamage = DamagePatterns.CustomDamageMatchExplosion(cascadeResult);
                var matchCommands = matchDamage.Invoke(Vector2Int.zero, 1, m_board, m_currentTimeStamp);
                if (matchCommands != null) allCommands.AddRange(matchCommands);
                BumpTimeStampPast(allCommands);

                var spawnCommands = SpawnSpecialTiles(cascadeResult);
                allCommands.AddRange(spawnCommands);

#if UNITY_EDITOR
                if (matchCommands != null)
                    foreach (var c in matchCommands)
                        Debug.Log($"[Model] iter={safety} MATCH-DMG {c.CommandType} {V2I(c.StartPosition)} t={c.startTimeStamp:F2} L={c.Layer}");
                foreach (var c in spawnCommands)
                    Debug.Log($"[Model] iter={safety} SPECIAL-SPAWN {c.CommandType} {V2I(c.StartPosition)} t={c.startTimeStamp:F2} L={c.Layer}");
#endif

                // Continue falling after processing matches
                continue;
            }

            // No matches found - if nothing fell either, we're done
            if (fallCommands.Count == 0)
                break;
        }

#if UNITY_EDITOR
        Debug.Log($"[Model] cascade END after {safety} iter(s), emitted {allCommands.Count} cmds");
#endif

        return allCommands;
    }

    private List<Command> ProcessTriggerCombination(
        TileModel tile1,
        TileModel tile2,
        Vector2Int pos1,
        Vector2Int pos2,
        float timeStamp)
    {
        List<Command> commands = new List<Command>();

        TileType type1 = tile1?.TileType ?? TileType.None;
        TileType type2 = tile2?.TileType ?? TileType.None;

        bool is1Triggerable = tile1 is ITriggerable;
        bool is2Triggerable = tile2 is ITriggerable;

        // Both must be triggerable to be a combination
        if (!is1Triggerable || !is2Triggerable)
            return null; // Not a combination, handle normally

        Damage specialDamage = null;

        // TNT + TNT
        if (type1 == TileType.TNT && type2 == TileType.TNT)
        {
            specialDamage = DamagePatterns.DoubleTNT;
        }
        // Rocket + Rocket
        else if ((type1 == TileType.HorizontalRocket || type1 == TileType.VerticalRocket) && (type2 == TileType.HorizontalRocket || type2 == TileType.VerticalRocket))
        {
            specialDamage = DamagePatterns.DoubleRocket;
        }
        // Rocket + TNT (either order)
        else if (((type1 == TileType.HorizontalRocket || type1 == TileType.VerticalRocket)   && type2 == TileType.TNT) ||
                 (type1 == TileType.TNT && (type2 == TileType.HorizontalRocket || type2 == TileType.VerticalRocket)))
        {
            specialDamage = DamagePatterns.RocketTNT;
        }
        // ColorBomb + ColorBomb
        else if (type1 == TileType.ColorBomb && type2 == TileType.ColorBomb)
        {
            specialDamage = DamagePatterns.DoubleColorBomb;
        }
        // ColorBomb + Matchable
        else if ((type1 == TileType.ColorBomb && tile2 is IMatchable) || (tile1 is IMatchable && type2 == TileType.ColorBomb))
        {
            TileModel colorBomb = type1 == TileType.ColorBomb ? tile1 : tile2;
            TileModel other = type1 == TileType.ColorBomb ? tile2 : tile1;

            // Set target color and get effect
            if (colorBomb is ColorBomb cb)
            {
                cb.SetTargetColor(other.TileType);
                specialDamage = cb.GetTriggerEffect();
            }
        }
        // ColorBomb + Triggerable
        else if ((type1 == TileType.ColorBomb && tile2 is ITriggerable) || (tile1 is ITriggerable && type2 == TileType.ColorBomb))
        {
            TileModel colorBomb = type1 == TileType.ColorBomb ? tile1 : tile2;
            TileModel other = type1 == TileType.ColorBomb ? tile2 : tile1;
            Vector2Int colorBombPos = type1 == TileType.ColorBomb ? pos1 : pos2;
            Vector2Int triggerablePos = type1 == TileType.ColorBomb ? pos2 : pos1;
            TileType triggerableType = other.TileType;

            // Remove moved tile immediately (ColorBomb stays visible during spawn phase)

            if(pos1 == colorBombPos)
            {
                Vector2Int temp = colorBombPos;
                colorBombPos = triggerablePos;
                triggerablePos = temp;
                SwapMiddleLayers(pos1, pos2);
            }
            m_board[triggerablePos.x, triggerablePos.y].SetLayer(NodeLayer.Middle, null);

            commands.Add(new Command(triggerablePos, triggerablePos, Commands.DestroySelf, timeStamp, NodeLayer.Middle));

            // Get random matchable color
            TileType targetColor = GetRandomMatchableColor();

            // Find all tiles of that color (top to bottom, left to right)
            List<Vector2Int> targetPositions = FindTilesOfColor(targetColor);

            // Phase 1: Convert all tiles to triggerables with spawn delays
            const float SPAWN_DELAY = 0.05f;
            float spawnTime = timeStamp + 0.1f;

            foreach (Vector2Int pos in targetPositions)
            {
                OverwriteTileLayerWith(pos, triggerableType, NodeLayer.Middle);
                commands.Add(new Command(pos, pos, Commands.Spawn, spawnTime, NodeLayer.Middle, triggerableType));
                spawnTime += SPAWN_DELAY;
            }

            // Concurrently: Fall to fill empty position (triggerablePos)
            float fallTime = timeStamp;
            while (true)
            {
                List<Command> fallCommands = m_fallManager.FallIteration(m_board, timeStamp, FALL_TIME);
                if (fallCommands.Count == 0) break;
                commands.AddRange(fallCommands);
                fallTime += FALL_TIME;
            }

            // Phase 2: ColorBomb dies after all spawns complete
            m_board[colorBombPos.x, colorBombPos.y].SetLayer(NodeLayer.Middle, null);
            commands.Add(new Command(colorBombPos, colorBombPos, Commands.DestroySelf, spawnTime, NodeLayer.Middle));
            spawnTime += 0.1f;


            // Phase 4: Trigger all spawned triggerables in sequence
            const float TRIGGER_DELAY = 0.1f;
            float triggerTime = Math.Max(spawnTime, fallTime) + 0.2f;

            foreach (Vector2Int pos in targetPositions)
            {
                TileModel tile = m_board[pos.x, pos.y].GetLayer(NodeLayer.Middle);

                // Skip if already destroyed by previous chain reaction
                if (tile == null) continue;
                if (!(tile is ITriggerable triggerable)) continue;

                // Remove tile and trigger it
                m_board[pos.x, pos.y].SetLayer(NodeLayer.Middle, null);
                Damage triggerEffect = triggerable.GetTriggerEffect();

                var triggerCommands = triggerEffect?.Invoke(pos, 1, m_board, triggerTime);
                if (triggerCommands != null) commands.AddRange(triggerCommands);

                triggerTime += TRIGGER_DELAY;
            }

            BumpTimeStampPast(commands);
            return commands;
        }

        // this means previous else if block handled it without a delegate
        if (specialDamage == null)
            return commands;

        // Remove both tiles from board
        ClearIfPresent(pos1, NodeLayer.Middle);
        ClearIfPresent(pos2, NodeLayer.Middle);

        // User slides from pos1 to pos2 therefore explosion happens at pos2
        var damageCommands = specialDamage.Invoke(pos2, 1, m_board, m_currentTimeStamp);
        if (damageCommands != null) commands.AddRange(damageCommands);

        BumpTimeStampPast(commands);
        return commands;
    }

    private TileType GetRandomMatchableColor()
    {
        TileType[] matchableColors = { TileType.Red, TileType.Green, TileType.Blue, TileType.Yellow, TileType.Purple };
        return matchableColors[UnityEngine.Random.Range(0, matchableColors.Length)];
    }

    private List<Vector2Int> FindTilesOfColor(TileType color)
    {
        List<Vector2Int> positions = new List<Vector2Int>();

        // Top to bottom, left to right
        for (int y = m_height - 1; y >= 0; y--)
        {
            for (int x = 0; x < m_width; x++)
            {
                TileModel tile = m_board[x, y].GetLayer(NodeLayer.Middle);

                if (tile != null && tile.TileType == color)
                {
                    positions.Add(new Vector2Int(x, y));
                }
            }
        }

        return positions;
    }
    private bool IsValidSwap(Vector2Int pos1, Vector2Int pos2)
    {
        return (m_board[pos1.x, pos1.y].GetLayer(NodeLayer.Middle) is IMovable && m_board[pos2.x, pos2.y].GetLayer(NodeLayer.Middle) is IMovable)
                && AreAdjacent(pos1, pos2);
    }

    private void SwapMiddleLayers(Vector2Int pos1, Vector2Int pos2)
    {
        TileModel temp = m_board[pos1.x, pos1.y].GetLayer(NodeLayer.Middle);
        m_board[pos1.x, pos1.y].SetLayer(NodeLayer.Middle, m_board[pos2.x, pos2.y].GetLayer(NodeLayer.Middle));
        m_board[pos2.x, pos2.y].SetLayer(NodeLayer.Middle, temp);
    }

    private bool AreAdjacent(Vector2Int pos1, Vector2Int pos2)
    {
        int dx = Mathf.Abs(pos1.x - pos2.x);
        int dy = Mathf.Abs(pos1.y - pos2.y);
        return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
    }

    // === Phase 2: Collect Triggers ===

    private Damage CollectTriggerIfExists(Vector2Int pos)
    {
        TileModel tile = m_board[pos.x, pos.y].GetLayer(NodeLayer.Middle);

        if (tile is ITriggerable triggerable)
        {
            m_board[pos.x, pos.y].SetLayer(NodeLayer.Middle, null);
            return triggerable.GetTriggerEffect();
        }

        return null;
    }

    // === Phase 4: Mark Matched ===

    private void MarkMatchedTiles(MatchResult matchResult)
    {
        foreach (Match match in matchResult.matches)
        {
            foreach (Vector2Int pos in match.tilePositions)
            {
                TileModel tile = m_board[pos.x, pos.y].GetLayer(NodeLayer.Middle);

                if (tile is IMatchable matchable)
                {
                    matchable.MarkAsMatched();
                }
            }
        }
    }

    // === Phase 7: Spawn Specials ===

    private List<Command> SpawnSpecialTiles(MatchResult matchResult)
    {
        List<Command> commands = new List<Command>();

        for (int i = 0; i < matchResult.specialTileTypes.Count; i++)
        {
            Match match = matchResult.matches[i];
            TileType specialType = matchResult.specialTileTypes[i];
            Vector2Int? position = matchResult.specialTilePositions[i];

            if (specialType == TileType.None || position == null)
                continue;

            TileModel specialTile = TileFactory.CreateTileFromType(specialType);

            if (specialTile != null)
            {
                m_board[position.Value.x, position.Value.y].SetLayer(NodeLayer.Middle, specialTile);
                commands.Add(new Command(position.Value, position.Value, Commands.Spawn, m_currentTimeStamp, NodeLayer.Middle, specialType));
            }
        }

        return commands;
    }



    private void OverwriteTileLayerWith(Vector2Int pos, TileType type, NodeLayer layer)
    {
        TileModel newTile = TileFactory.CreateTileFromType(type);
        m_board[pos.x, pos.y].SetLayer(layer, newTile);
    }

    void ClearIfPresent(Vector2Int pos, NodeLayer l)
    {
        TileModel layer = m_board[pos.x, pos.y].GetLayer(l);
        if (layer != null)
        {
            m_board[pos.x, pos.y].SetLayer(l, null);
        }
    }

#if UNITY_EDITOR
    private void DumpBoard(string label)
    {
        int width = m_board.GetLength(0);
        int height = m_board.GetLength(1);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== {label} (t={m_currentTimeStamp:F2}) ===");
        for (int y = height - 1; y >= 0; y--)
        {
            sb.Append($"y{y}: ");
            for (int x = 0; x < width; x++)
            {
                TileModel mid = m_board[x, y].GetLayer(NodeLayer.Middle);
                sb.Append(mid == null ? " . " : ShortSym(mid)).Append(' ');
            }
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }

    private static Vector2Int V2I(Vector2 v) => new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));

    private static void LogCommandBreakdown(List<Command> cmds)
    {
        var groups = cmds.GroupBy(c => c.CommandType).Select(g => $"{g.Key}={g.Count()}");
        Debug.Log($"[Model] breakdown: {string.Join(", ", groups)}");
    }

    private static string ShortSym(TileModel t)
    {
        bool matched = t is Matchable m && m.IsMatched;
        string suffix = matched ? "*" : " ";
        switch (t.TileType)
        {
            case TileType.Red:    return "R" + suffix + " ";
            case TileType.Green:  return "G" + suffix + " ";
            case TileType.Blue:   return "B" + suffix + " ";
            case TileType.Yellow: return "Y" + suffix + " ";
            case TileType.Box:    return "Bx" + suffix;
            case TileType.Vase:   return "Vs" + suffix;
            case TileType.Rock:
            case TileType.Stone:  return "St" + suffix;
            case TileType.HorizontalRocket: return "RH" + suffix;
            case TileType.VerticalRocket:   return "RV" + suffix;
            case TileType.TNT:    return "TT" + suffix;
            case TileType.ColorBomb: return "CB" + suffix;
            default: return "? " + suffix;
        }
    }
#endif


    // === Timing ===

    private void AdvanceTime()
    {
        m_currentTimeStamp += TIME_STEP;
    }

    // Near-zero buffer: jump to the latest startTimeStamp emitted in this
    // phase + BUMP_GAP so the next phase (fall/cascade) starts the instant
    // the last damage fires — minus the old AdvanceTime's 100 ms pause. The
    // BUMP_GAP makes the destroy→fall ordering explicit instead of relying
    // on LINQ stable-sort insertion order in BoardView.ExecuteCommands.
    // Commands listed here are "point" events (DestroySelf/Trigger/Spawn);
    // Move uses += FALL_TIME elsewhere.
    private void BumpTimeStampPast(List<Command> commands)
    {
        float max = m_currentTimeStamp;
        for (int i = 0; i < commands.Count; i++)
            if (commands[i].startTimeStamp > max) max = commands[i].startTimeStamp;
        m_currentTimeStamp = max + BUMP_GAP;
    }

}
public struct SwapResult
{
    public List<Command> Commands;
    public MergeInfo? Merge;
}

public struct MergeInfo
{
    public Vector2Int Position;
    public TileType SourceType1;
    public TileType SourceType2;
    public float TimeStamp;
}