using System;
using System.Collections.Generic;
using UnityEngine;

public class Level
{
    private int m_levelNumber;
    public int LevelNumber => m_levelNumber;

    private int m_moves;
    private Dictionary<TargetType, int> m_requirements;

    // Active requirement counter: number of requirement types whose remaining > 0.
    // Decremented exactly when a requirement transitions oldValue>0 → newValue=0.
    // CheckGameEnd reads this in O(1) instead of iterating the dict.
    private int m_ActiveRequirementCount;

    public IReadOnlyDictionary<TargetType, int> Requirements => m_requirements;
    public int RemainingMoves => m_moves;

    public event Action<int> OnMovesChanged;
    public event Action<TargetType, int> OnRequirementChanged;
    public event Action<bool> OnLevelEnded;
    public event Action OnLevelWon;
    public event Action OnLevelLost;

    public Level(int levelNumber, int moves, Dictionary<TargetType, int> requirements)
    {
        m_levelNumber = levelNumber;
        m_moves = moves;
        m_requirements = requirements;

        m_ActiveRequirementCount = 0;
        foreach (var kvp in m_requirements)
        {
            if (kvp.Value > 0) m_ActiveRequirementCount++;
        }
    }

    public LevelStatus GetStatus()
    {
        foreach (var kvp in m_requirements)
        {
            if (kvp.Value > 0)
            {
                return m_moves > 0 ? LevelStatus.Ongoing : LevelStatus.Lost;
            }
        }
        return m_moves >= 0 ? LevelStatus.Won : LevelStatus.Lost;
    }

    public void ConsumeMove()
    {
        m_moves--;
        OnMovesChanged?.Invoke(m_moves);
    }

    public void UpdateRequirement(TargetType type, int amount)
    {
        if (!m_requirements.ContainsKey(type)) return;

        int oldValue = m_requirements[type];
        int newValue = Math.Max(0, oldValue - amount);
        m_requirements[type] = newValue;

        if (oldValue > 0 && newValue == 0) m_ActiveRequirementCount--;

        OnRequirementChanged?.Invoke(type, newValue);
    }

    // External trigger: called by composition root once the board has fully settled.
    // Win takes priority over lose: clearing requirements on the last move still wins.
    public void CheckGameEnd()
    {
        if (m_ActiveRequirementCount <= 0)
            OnLevelWon?.Invoke();
        else if (m_moves <= 0)
            OnLevelLost?.Invoke();
    }

    public void NotifyLevelEnded(bool won)
    {
        OnLevelEnded?.Invoke(won);
    }

    public int GetRemainingMoves() { return m_moves; }
}

public enum LevelStatus
{
    None,
    Ongoing,
    Won,
    Lost,
}
