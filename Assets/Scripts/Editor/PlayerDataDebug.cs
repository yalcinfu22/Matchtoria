#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PlayerDataDebug
{
    [MenuItem("Tools/Reset Player Data")]
    public static void Reset()
    {
        PlayerPrefs.DeleteKey("CurrentLevel");
        PlayerPrefs.Save();
        Debug.Log("Player data reset to level 1.");
    }
}
#endif
