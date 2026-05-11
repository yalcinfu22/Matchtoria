using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private void Awake()
    {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }
    /*
    // opS = operation status
    public void StartGame(int currentLevel)
    {
        bool opS = true;
        // Load and create the current level using LevelManager
        LevelData levelData = LevelLoader.Instance.LoadLevelData();

        opS = LevelManager.Instance.StartLevel(levelData.Clone());
        if (opS == false)
        {
            Debug.Log($"Can not start the level {levelData.level_number}");
            return;
        } else
        {
            Debug.Log("Level loaded successfully");
        }
        // pool necessary objects using poolmanager
        // create the boardModel and view for the level
        // handle uı using level details
    }
    */
}
