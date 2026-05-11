using Newtonsoft.Json;
using UnityEngine;

public class LevelLoader
{
    public static LevelLoader Instance { get; private set; }

    public LevelLoader()
    {
        Instance = this;
    }

    public LevelData LoadLevelData(int levelNumber)
    {
        string resourcePath = $"Levels/Level{levelNumber}";
        TextAsset jsonFile = Resources.Load<TextAsset>(resourcePath);

        if (jsonFile == null)
        {
            Debug.LogError($"Level file not found: Resources/{resourcePath}.json");
            return null;
        }

        LevelData data = JsonConvert.DeserializeObject<LevelData>(jsonFile.text);
        data.level_number = levelNumber;
        return data;
    }
}