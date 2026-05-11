using UnityEngine;

public class PlayerDataManager
{
    public static PlayerDataManager Instance { get; private set; }

    private const string SAVE_KEY_LEVEL = "CurrentLevel";

    private int m_CurrentLevel;
    private int m_MaxLevel;

    public int CurrentLevel => m_CurrentLevel;
    public bool IsMaxLevelReached => m_CurrentLevel > m_MaxLevel;

    public PlayerDataManager(int maxLevel)
    {
        Instance = this;
        m_MaxLevel = maxLevel;
        Load();
    }

    private void Load()
    {
        m_CurrentLevel = PlayerPrefs.GetInt(SAVE_KEY_LEVEL, 1);
        // Future: load coins, lives, etc.
    }

    public void CompleteLevel()
    {
        if (m_CurrentLevel > m_MaxLevel) return;

        m_CurrentLevel++;
        PlayerPrefs.SetInt(SAVE_KEY_LEVEL, m_CurrentLevel);
        PlayerPrefs.Save();
    }
}