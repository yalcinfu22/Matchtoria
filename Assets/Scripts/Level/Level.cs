using System;
using System.Collections.Generic;

public class Level
{
    private readonly int m_LevelNumber;
    public int LevelNumber => m_LevelNumber;

    private int m_Moves;
    public int RemainingMoves => m_Moves;

    // Sadece tamamlanmamış (>0) hedefler tutulur; 0'a düşen silinir.
    private readonly Dictionary<TargetType, int> m_Requirements;
    public IReadOnlyDictionary<TargetType, int> Requirements => m_Requirements;

    public bool IsWon => m_Requirements.Count == 0;
    public bool IsOutOfMoves => m_Moves <= 0;

    public event Action<int> OnMovesChanged;
    public event Action<TargetType, int> OnRequirementChanged;
    public event Action OnLevelWon;
    public event Action OnLevelLost;

    public Level(int levelNumber, int moves, Dictionary<TargetType, int> requirements)
    {
        m_LevelNumber = levelNumber;
        m_Moves = moves;

        // Dışarıdan gelen dict'i kopyala: silme işlemi level datasını bozmasın.
        m_Requirements = new Dictionary<TargetType, int>();
        foreach (var kvp in requirements)
        {
            if (kvp.Value > 0) m_Requirements.Add(kvp.Key, kvp.Value);
        }
    }

    public LevelStatus GetStatus()
    {
        if (IsWon) return LevelStatus.Won;
        return IsOutOfMoves ? LevelStatus.Lost : LevelStatus.Ongoing;
    }

    public void ConsumeMove()
    {
        if (IsOutOfMoves) return;
        m_Moves--;
        OnMovesChanged?.Invoke(m_Moves);
    }

    public void UpdateRequirement(TargetType type, int amount)
    {
        if (!m_Requirements.TryGetValue(type, out int oldValue)) return;

        int newValue = Math.Max(0, oldValue - amount);
        if (newValue == 0) m_Requirements.Remove(type);
        else m_Requirements[type] = newValue;

        OnRequirementChanged?.Invoke(type, newValue);
    }

    // Board tamamen oturduktan sonra composition root tarafından çağrılır.
    // Kazanma önceliklidir: son hamlede hedefler bittiyse yine kazanır.
    public void CheckGameEnd()
    {
        if (IsWon) OnLevelWon?.Invoke();
        else if (IsOutOfMoves) OnLevelLost?.Invoke();
    }

}

public enum LevelStatus
{
    None,
    Ongoing,
    Won,
    Lost,
}
