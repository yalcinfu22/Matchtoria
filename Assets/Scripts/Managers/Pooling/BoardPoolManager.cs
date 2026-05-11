using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class BoardPoolManager
{
    private Dictionary<PoolType, ObjectPool<TileView>> m_Pools;
    private Dictionary<PoolType, TileView> m_Prefabs;
    private Dictionary<PoolType, Transform> m_Parents;
    private Transform m_Root;
    private LevelData m_LevelData;

    public BoardPoolManager(Dictionary<PoolType, TileView> prefabs, LevelData levelData)
    {
        m_Prefabs = prefabs;
        m_LevelData = levelData;
        m_Pools = new Dictionary<PoolType, ObjectPool<TileView>>();
        m_Parents = new Dictionary<PoolType, Transform>();

        SetupHierarchy();
        InitializePools(levelData);
    }

    private void SetupHierarchy()
    {
        m_Root = new GameObject("_Pools").transform;

        foreach (PoolType type in System.Enum.GetValues(typeof(PoolType)))
        {
            if (type == PoolType.None) continue;
            if (!m_Prefabs.ContainsKey(type)) continue;

            Transform parent = new GameObject(type.ToString()).transform;
            parent.SetParent(m_Root);
            m_Parents[type] = parent;
        }
    }

    private void InitializePools(LevelData levelData)
    {
        int totalNodes = levelData.grid_width * levelData.grid_height;
        int extraRow = levelData.grid_width;

        foreach (var kvp in m_Prefabs)
        {
            PoolType poolType = kvp.Key;
            TileView prefab = kvp.Value;
            int capacity = CalculateCapacity(poolType, totalNodes, extraRow);

            m_Pools[poolType] = new ObjectPool<TileView>(
                createFunc: () =>
                {
                    TileView tile = Object.Instantiate(prefab, m_Parents[poolType]);
                    tile.gameObject.SetActive(false);
                    return tile;
                },
                actionOnGet: tile => tile.gameObject.SetActive(true),
                actionOnRelease: tile =>
                {
                    if (tile is MatchableTileView m) m.ResetView();
                    tile.transform.SetParent(m_Parents[poolType], false);
                    tile.gameObject.SetActive(false);
                },
                actionOnDestroy: tile => Object.Destroy(tile.gameObject),
                defaultCapacity: capacity,
                maxSize: capacity * 2
            );

            // Direkt yarat ve release et — tek döngü
            for (int i = 0; i < capacity; i++)
            {
                TileView tile = Object.Instantiate(prefab, m_Parents[poolType]);
                tile.gameObject.SetActive(false);
                m_Pools[poolType].Release(tile);
            }
        }
    }

    private int CalculateCapacity(PoolType type, int totalNodes, int extraRow)
    {
        return type switch
        {
            PoolType.Matchable => totalNodes + extraRow,
            PoolType.VerticalRocket or
            PoolType.HorizontalRocket or
            PoolType.TNT => Mathf.CeilToInt(totalNodes * 0.2f),
            PoolType.ColorBomb => Mathf.CeilToInt(totalNodes * 0.1f),
            _ => CountInLevel(type) // Obstacle — level'daki sayı kadar
        };
    }

    // Counts how many cells in the level resolve to a given pool type. Cell ids
    // can carry health suffixes ("box3", "vase5"); parsing routes them through
    // PoolTypeMap so a "box3" cell still increments the Box pool's capacity.
    private int CountInLevel(PoolType type)
    {
        int count = 0;

        for (int i = 0; i < m_LevelData.grid_top.Length; i++)
        {
            if (CellResolvesTo(m_LevelData.grid_top[i], type)) count++;
            if (CellResolvesTo(m_LevelData.grid_middle[i], type)) count++;
            if (CellResolvesTo(m_LevelData.grid_bottom[i], type)) count++;
        }

        return Mathf.Max(count, 1);
    }

    private static bool CellResolvesTo(string rawId, PoolType type)
    {
        TileIdParser.Result parsed = TileIdParser.Parse(rawId);
        if (!parsed.Valid) return false;
        return PoolTypeMap.FromTileType(parsed.Type) == type;
    }

    // === Public API ===

    // Used by spawn cmd path (cascade refill) where the source is a TileType, not raw id.
    // Matchables and special tiles spawned mid-game don't carry an HP override.
    public TileView Get(TileType tileType)
    {
        if (tileType == TileType.None) return null;
        PoolType poolType = TileTypeToPoolType(tileType);
        TileView tile = m_Pools[poolType].Get();
        tile.Setup(tileType, this, GameConfig.CELL_SIZE);
        return tile;
    }

    // Used by level build (BoardView/NodeView). Parses the raw id, draws from the
    // matching pool, and forwards the parsed health to Setup so the view can pick
    // its initial sprite (e.g. "box3" → BoxView with m_health=3 → "Box3" label).
    public TileView Get(string rawId)
    {
        if (string.IsNullOrEmpty(rawId) || rawId == "null") return null;

        TileIdParser.Result parsed = TileIdParser.Parse(rawId);
        if (!parsed.Valid)
        {
            Debug.LogError($"BoardPoolManager: Invalid tile id '{rawId}'");
            return null;
        }

        PoolType poolType = PoolTypeMap.FromTileType(parsed.Type);
        if (poolType == PoolType.None) return null;

        TileView tile = m_Pools[poolType].Get();
        tile.Setup(parsed.Type, this, GameConfig.CELL_SIZE, parsed.Health);
        return tile;
    }

    public void Return(TileView tile)
    {
        PoolType poolType = TileTypeToPoolType(tile.TileType);
        m_Pools[poolType].Release(tile);
    }

    // === Mapping ===

    private static PoolType TileTypeToPoolType(TileType tileType)
    {
        return tileType switch
        {
            TileType.Red or TileType.Green or
            TileType.Blue or TileType.Yellow => PoolType.Matchable,
            TileType.Rock => PoolType.Rock,
            TileType.Box => PoolType.Box,
            TileType.Vase => PoolType.Vase,
            TileType.Stone => PoolType.Stone,
            TileType.VerticalRocket => PoolType.VerticalRocket,
            TileType.HorizontalRocket => PoolType.HorizontalRocket,
            TileType.TNT => PoolType.TNT,
            TileType.ColorBomb => PoolType.ColorBomb,
            _ => PoolType.None
        };
    }
}
