using System.Collections.Generic;
using UnityEngine;

public class LevelManager
{
    private Level m_currentLevel;
    public Level Level => m_currentLevel;

    // Initialize a new level for gameplay
    public bool StartLevel(LevelData levelData)
    {
        if (levelData == null)
        {
            Debug.LogError($"Failed to load level");
            return false;
        }

        // Create Level logic object
        m_currentLevel = CreateLevelFromData(levelData);

        Debug.Log($"Level {levelData.level_number} started: {m_currentLevel.GetRemainingMoves()} moves");
        return true;
    }

    // Factory: Convert LevelData → Level
    private Level CreateLevelFromData(LevelData data)
    {
        Dictionary<TargetType, int> targets = new Dictionary<TargetType, int>();

        foreach (var req in data.requirements)
        {
            switch (req.type)
            {
                case "red":
                    targets[TargetType.Red] = req.value;
                    break;
                case "blue":
                    targets[TargetType.Blue] = req.value;
                    break;
                case "green":
                    targets[TargetType.Green] = req.value;
                    break;
                case "yellow":
                    targets[TargetType.Yellow] = req.value;
                    break;
                case "box":
                    targets[TargetType.Box] = req.value;
                    break;
                case "vase":
                    targets[TargetType.Vase] = req.value;
                    break;
                case "rock":
                    targets[TargetType.Rock] = req.value;
                    break;
                default:
                    Debug.LogWarning($"Unknown requirement type: {req.type}");
                    break;
            }
        }

        return new Level(data.level_number, data.move_count, targets);
    }

    // Check win/lose state
    public LevelStatus GetLevelStatus()
    {
        if(Level == null)
        {
            return LevelStatus.None;
        } else
        {
            return Level.GetStatus();
        }
    }

    // Clean up on level end
    public void EndLevel()
    {
        m_currentLevel = null;
    }
}

public enum TargetType
{
    None,
    Score,
    Red,
    Green,
    Blue,
    Yellow,
    Box,
    Rock,
    Vase,
}