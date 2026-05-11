using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    private LevelData _levelData;
    private BoardPoolManager _Pool;
    private BoardView _View;
    private BoardModel _Model;

    private Vector2Int? _firstSelection;

    public event Action OnSwapCompleted;
    public event Action<Dictionary<TargetType, int>> OnTilesDestroyed;
    public event Action OnBoardSettled;

    public void Initialize(LevelData levelData, BoardView view, BoardPoolManager pool)
    {
        _levelData = levelData;
        _View = view;
        _Pool = pool;
        _Model = new BoardModel();
        _Model.BuildBoard(levelData);
        _View.Init(pool, _levelData);
        _View.OnTileClicked += HandleTileClicked;
    }

    private void HandleTileClicked(Vector2Int gridPos)
    {
        // Input lock — ignore clicks while the view is animating.
        if (_View.IsBusy) return;

        // 1. İlk tıklama
        if (_firstSelection == null)
        {
            _firstSelection = gridPos;
            return;
        }

        // 2. İkinci tıklama
        Vector2Int first = _firstSelection.Value;
        _firstSelection = null;

        // 3. Yan yana olma kontrolü
        if (!IsAdjacent(first, gridPos)) return;

        // 4. Modele gönder
        SwapResult result = _Model.ProcessSwap(first, gridPos);
        if (result.Commands.Count == 0) return;

        // 5. Geçersiz swap (sadece Swap+Swap-back revert) move düşürmemeli.
        bool isValidMatch = result.Commands.Any(c => c.CommandType != Commands.Swap);

        // 6. Destroy'ları timestamp'e göre grupla, View'a timed callback olarak geçir.
        // onComplete: tüm DOTween Sequence bitince fire — board fully settled.
        var timedCallbacks = BuildDestroyCallbacks(result.Commands);
        _View.ExecuteCommands(result.Commands, timedCallbacks, () => OnBoardSettled?.Invoke());

        // 7. Match geçerliyse animasyonu beklemeden anında move'u düş.
        if (isValidMatch) OnSwapCompleted?.Invoke();
    }

    private Dictionary<float, Action> BuildDestroyCallbacks(List<Command> commands)
    {
        var grouped = new Dictionary<float, Dictionary<TargetType, int>>();
        foreach (var cmd in commands)
        {
            if (cmd.CommandType != Commands.DestroySelf) continue;
            TargetType target = MapTileToTarget(cmd.TileType);
            if (target == TargetType.None) continue;

            if (!grouped.TryGetValue(cmd.startTimeStamp, out var bucket))
                grouped[cmd.startTimeStamp] = bucket = new Dictionary<TargetType, int>();

            bucket[target] = bucket.TryGetValue(target, out int n) ? n + 1 : 1;
        }

        var callbacks = new Dictionary<float, Action>();
        foreach (var kvp in grouped)
        {
            var snapshot = kvp.Value;
            callbacks[kvp.Key] = () => OnTilesDestroyed?.Invoke(snapshot);
        }
        return callbacks;
    }

    private static TargetType MapTileToTarget(TileType type) => type switch
    {
        TileType.Red    => TargetType.Red,
        TileType.Green  => TargetType.Green,
        TileType.Blue   => TargetType.Blue,
        TileType.Yellow => TargetType.Yellow,
        TileType.Box    => TargetType.Box,
        TileType.Vase   => TargetType.Vase,
        TileType.Rock   => TargetType.Rock,
        _               => TargetType.None,
    };

    private bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    public void LockInput()
    {
        if (_View != null) _View.OnTileClicked -= HandleTileClicked;
    }

    private void OnDestroy()
    {
        if (_View != null)
            _View.OnTileClicked -= HandleTileClicked;
    }
}
