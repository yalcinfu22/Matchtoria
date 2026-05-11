using System.Collections.Generic;
using UnityEngine;

public class TileViewTest : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private TileView m_MatchablePrefab;

    private BoardPoolManager m_Pool;
    private List<TileView> m_ActiveTiles;

    private void Start()
    {
        // Pool kur
        var prefabs = new Dictionary<PoolType, TileView>
        {
            { PoolType.Matchable, m_MatchablePrefab }
        };

        var fakeLevelData = new LevelData
        {
            grid_width = 4,
            grid_height = 1,
            grid_top = new string[4],
            grid_middle = new string[4],
            grid_bottom = new string[4]
        };
        
        m_ActiveTiles = new List<TileView>();
        m_Pool = new BoardPoolManager(prefabs, fakeLevelData);

        // 4 renk tile spawn et
        SpawnTiles();
    }

    private void SpawnTiles()
    {
        if (m_ActiveTiles.Count != 0) return;
        m_ActiveTiles = new List<TileView>();
        TileType[] colors = { TileType.Red, TileType.Blue, TileType.Green, TileType.Yellow };

        for (int i = 0; i < colors.Length; i++)
        {
            TileView tile = m_Pool.Get(colors[i]);
            tile.transform.position = new Vector3(i * 1.5f, 0, 0);
            m_ActiveTiles.Add(tile);
        }
    }

    private void Update()
    {
        // 1 → hepsini öldür
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            foreach (var tile in m_ActiveTiles)
                tile.ApplyCommand(new Command { CommandType = Commands.DestroySelf });

            m_ActiveTiles.Clear();
        }

        // 2 → yeniden spawn
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SpawnTiles();
        }
    }
}