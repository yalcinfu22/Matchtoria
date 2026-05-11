using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public class BoardView : MonoBehaviour
{
    [SerializeField] private NodeView _nodeViewPrefab;

    private int _width;
    private int _height;
    private NodeView[,] _board;
    private BoardPoolManager _poolManager;
    private SpriteMask _gridMask;
    private static Sprite s_maskSprite;

    public int Width => _width;
    public int Height => _height;
    public bool IsBusy { get; private set; }

    public event Action<Vector2Int> OnTileClicked;

    private Camera _cam;

    private void Awake()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        if (IsBusy) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 worldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridPos = WorldToGrid(worldPos);

            if (IsInsideGrid(gridPos))
                OnTileClicked?.Invoke(gridPos);
        }
    }

    private Vector2Int WorldToGrid(Vector2 worldPos)
    {
        Vector2 local = (Vector2)transform.InverseTransformPoint(worldPos);

        float cellSize = GameConfig.CELL_SIZE;
        float originX = -(_width * cellSize) / 2f;
        float originY = -(_height * cellSize) / 2f;

        int col = Mathf.FloorToInt((local.x - originX) / cellSize);
        int row = Mathf.FloorToInt((local.y - originY) / cellSize);

        return new Vector2Int(col, row);
    }

    private bool IsInsideGrid(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < _width
            && pos.y >= 0 && pos.y < _height;
    }

    public void Init(BoardPoolManager poolManager, LevelData levelData)
    {
        _poolManager = poolManager;
        _width = levelData.grid_width;
        _height = levelData.grid_height;
        _board = new NodeView[_width, _height];

        float cellSize = GameConfig.CELL_SIZE;

        Vector2 gridOffset = new Vector2(
            -(_width * cellSize) / 2f + cellSize / 2f,
            -(_height * cellSize) / 2f + cellSize / 2f
        );

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                NodeView node = Instantiate(_nodeViewPrefab, transform);

                Vector3 position = new Vector3(
                    x * cellSize + gridOffset.x,
                    y * cellSize + gridOffset.y,
                    0f
                );
                node.transform.localPosition = position;

                int idx = y * _width + x;
                string topRaw    = levelData.grid_top[idx];
                string middleRaw = levelData.grid_middle[idx];
                string bottomRaw = levelData.grid_bottom[idx];

                node.Init(_poolManager, topRaw, middleRaw, bottomRaw, y, _height);
                _board[x, y] = node;
            }
        }

        CreateGridMask();
    }

    // Hides any tile whose SpriteRenderer is set to VisibleInsideMask while it
    // sits outside the grid (e.g. top-row refill tiles spawned 1 cell above).
    // Programmatic — no scene/prefab dependency.
    private void CreateGridMask()
    {
        var go = new GameObject("GridMask");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = new Vector3(
            _width  * GameConfig.CELL_SIZE,
            _height * GameConfig.CELL_SIZE,
            1f);

        _gridMask = go.AddComponent<SpriteMask>();
        _gridMask.sprite = GetSolidSquareSprite();
    }

    private static Sprite GetSolidSquareSprite()
    {
        if (s_maskSprite != null) return s_maskSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        s_maskSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return s_maskSprite;
    }

    // ============================================================
    // Command playback — Two-Pass DOTween Sequence integration.
    //
    // Loop is single-pass over time-sorted commands:
    //   1. Read tile reference from _board (logical state at command's start time)
    //   2. Mutate _board immediately (clear source, set destination with worldPositionStays=true)
    //   3. Insert DOMove tween on the sequence at cmd.startTimeStamp
    //
    // L-shape guarantee for chained falls (Fall→FallLeft on same tile) comes from
    // two distinct tweens chained via Insert(timestamp) — NOT a single hypotenuse move.
    // ============================================================

    public void ExecuteCommands(List<Command> commands, Dictionary<float, Action> timedCallbacks, Action onComplete)
    {
        if (commands == null || commands.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        IsBusy = true;

        // Stable order: OrderBy preserves insertion order on equal timestamps,
        // which keeps Fall-before-Spawn semantics within one fall iteration.
        List<Command> sorted = commands.OrderBy(c => c.startTimeStamp).ToList();

#if UNITY_EDITOR
        Debug.Log($"[View] ExecuteCommands ENTER count={sorted.Count}");
        foreach (var c in sorted)
            Debug.Log($"[View]   t={c.startTimeStamp:F2} {c.CommandType} L={c.Layer} {ToInt(c.StartPosition)}→{ToInt(c.TargetPosition)}");
#endif

        Sequence seq = DOTween.Sequence();

        foreach (Command cmd in sorted)
        {
            switch (cmd.CommandType)
            {
                case Commands.Move:
                case Commands.Fall:
                case Commands.FallLeft:
                case Commands.FallRight:
                    HandleMove(cmd, seq);
                    break;

                case Commands.Swap:
                    HandleSwap(cmd, seq);
                    break;

                case Commands.Spawn:
                    HandleSpawn(cmd, seq);
                    break;

                case Commands.DestroySelf:
                case Commands.TakeDamage:
                case Commands.Trigger:
                    HandleStatic(cmd, seq);
                    break;

                case Commands.Merge:
                    Debug.LogWarning("[BoardView] Merge command (descoped) — view has no handler. Skipping.");
                    break;
            }
        }

        // External timed callbacks (e.g. BoardManager fires OnTilesDestroyed at each destroy timestamp).
        if (timedCallbacks != null)
        {
            foreach (var kvp in timedCallbacks)
                seq.InsertCallback(kvp.Key, kvp.Value.Invoke);
        }

        seq.OnComplete(() =>
        {
            IsBusy = false;
#if UNITY_EDITOR
            Debug.Log($"[View] ExecuteCommands COMPLETE — IsBusy=false");
#endif
            onComplete?.Invoke();
        });
    }

    private void HandleMove(Command cmd, Sequence seq)
    {
        Vector2Int start = ToInt(cmd.StartPosition);
        Vector2Int target = ToInt(cmd.TargetPosition);

        TileView tile = _board[start.x, start.y].GetTile(cmd.Layer);
        if (tile == null)
        {
            Debug.LogWarning($"[BoardView] HandleMove: no tile at {start}/{cmd.Layer} for {cmd.CommandType}");
            return;
        }

        _board[start.x, start.y].SetTile(cmd.Layer, null);
        _board[target.x, target.y].SetTile(cmd.Layer, tile, worldPositionStays: true);

        Vector3 endWorld = _board[target.x, target.y].transform.position;
        float duration = (cmd.CommandType == Commands.Move) ? GameConfig.MOVE_TIME : GameConfig.FALL_TIME;
        seq.Insert(cmd.startTimeStamp, tile.transform.DOMove(endWorld, duration).SetEase(Ease.Linear));
    }

    private void HandleSwap(Command cmd, Sequence seq)
    {
        Vector2Int p1 = ToInt(cmd.StartPosition);
        Vector2Int p2 = ToInt(cmd.TargetPosition);

        // Read both BEFORE write — local atomicity for the two-tile exchange.
        TileView t1 = _board[p1.x, p1.y].GetTile(cmd.Layer);
        TileView t2 = _board[p2.x, p2.y].GetTile(cmd.Layer);
        if (t1 == null || t2 == null)
        {
            Debug.LogWarning($"[BoardView] HandleSwap: missing tile at {p1} or {p2} for layer {cmd.Layer}");
            return;
        }

        _board[p1.x, p1.y].SetTile(cmd.Layer, t2, worldPositionStays: true);
        _board[p2.x, p2.y].SetTile(cmd.Layer, t1, worldPositionStays: true);

        Vector3 worldP1 = _board[p1.x, p1.y].transform.position;
        Vector3 worldP2 = _board[p2.x, p2.y].transform.position;

        seq.Insert(cmd.startTimeStamp, t1.transform.DOMove(worldP2, GameConfig.MOVE_TIME).SetEase(Ease.OutQuad));
        seq.Insert(cmd.startTimeStamp, t2.transform.DOMove(worldP1, GameConfig.MOVE_TIME).SetEase(Ease.OutQuad));
    }

    private void HandleSpawn(Command cmd, Sequence seq)
    {
        Vector2Int start = ToInt(cmd.StartPosition);
        Vector2Int target = ToInt(cmd.TargetPosition);

        TileType type = cmd.TileType;
        if (type == TileType.None)
        {
            Debug.LogWarning($"[BoardView] HandleSpawn: cmd.TileType is None for {target}/{cmd.Layer}");
            return;
        }

        TileView newTile = _poolManager.Get(type);
        if (newTile == null) return;

        bool isTopRowRefill = start.y >= _height;
        if (isTopRowRefill)
        {
            // Position above the top row in world space, then reparent keeping world pos.
            Vector3 topCellWorld = _board[target.x, _height - 1].transform.position;
            float cellsAbove = start.y - (_height - 1);
            Vector3 startWorld = topCellWorld + Vector3.up * GameConfig.CELL_SIZE * cellsAbove;
            newTile.transform.position = startWorld;

            _board[target.x, target.y].SetTile(cmd.Layer, newTile, worldPositionStays: true);

            Vector3 endWorld = _board[target.x, target.y].transform.position;
            seq.Insert(cmd.startTimeStamp, newTile.transform.DOMove(endWorld, GameConfig.SPAWN_DROP_TIME).SetEase(Ease.Linear));
        }
        else
        {
            // In-place spawn (special tile pop) — center on target node, scale up from 0.
            _board[target.x, target.y].SetTile(cmd.Layer, newTile);

            Vector3 finalScale = newTile.transform.localScale;
            newTile.transform.localScale = Vector3.zero;
            seq.Insert(cmd.startTimeStamp, newTile.transform.DOScale(finalScale, GameConfig.SPAWN_DROP_TIME).SetEase(Ease.OutBack));
        }
    }

    private void HandleStatic(Command cmd, Sequence seq)
    {
        Vector2Int pos = ToInt(cmd.StartPosition);
        TileView tile = _board[pos.x, pos.y].GetTile(cmd.Layer);
        if (tile == null)
        {
            Debug.LogWarning($"[BoardView] HandleStatic: no tile at {pos}/{cmd.Layer} for {cmd.CommandType}");
            return;
        }

        // DestroySelf/Trigger: clear logical reference now so subsequent commands at this cell don't see a corpse.
        // The TileView returns itself to the pool from inside its own PlayDestroy/PlayTrigger callback.
        if (cmd.CommandType == Commands.DestroySelf || cmd.CommandType == Commands.Trigger)
        {
            _board[pos.x, pos.y].SetTile(cmd.Layer, null);
        }

        seq.InsertCallback(cmd.startTimeStamp, () => tile.ApplyCommand(cmd));
    }

    private static Vector2Int ToInt(Vector2 v) => new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));
}
