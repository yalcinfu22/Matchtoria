using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader
{
    public static SceneLoader Instance { get; private set; }

    private MonoBehaviour m_CoroutineRunner;

    private float m_MinLoadingDuration;        // for menus, etc.
    private float m_MinLevelLoadingDuration;   // for levels

    private float m_CurrentMinDuration;
    private float m_LoadStartTime;

    private Canvas m_DreamGamesScreen;
    private Canvas m_RoyalMatchScreen;
    private Canvas m_LevelLoadingScreen;

    private Canvas m_ActiveLoadingScreen;
    private string m_CurrentLoadedScene;

    public SceneLoader(
        MonoBehaviour coroutineRunner,
        float minLoadingDuration,
        float minLevelLoadingDuration,
        Canvas dreamGames,
        Canvas royalMatch,
        Canvas levelLoading)
    {
        Instance = this;
        m_CoroutineRunner = coroutineRunner;

        m_MinLoadingDuration = minLoadingDuration;
        m_MinLevelLoadingDuration = minLevelLoadingDuration;

        m_DreamGamesScreen = dreamGames;
        m_RoyalMatchScreen = royalMatch;
        m_LevelLoadingScreen = levelLoading;
    }

    public void LoadScene(string sceneName, LoadingScreenType screenType)
    {
        Debug.Log($"LoadScene called with {screenType}");

        m_ActiveLoadingScreen = GetScreen(screenType);

        if (m_ActiveLoadingScreen == null)
        {
            Debug.LogError($"Loading screen for {screenType} is NULL!");
            return;
        }
        m_ActiveLoadingScreen.gameObject.SetActive(true);

        // Choose min duration based on what we're loading
        m_CurrentMinDuration = (screenType == LoadingScreenType.Level)
            ? m_MinLevelLoadingDuration
            : m_MinLoadingDuration;

        m_LoadStartTime = Time.time;

        if (!string.IsNullOrEmpty(m_CurrentLoadedScene))
        {
            var unloadOp = SceneManager.UnloadSceneAsync(m_CurrentLoadedScene);
            unloadOp.completed += _ => LoadAdditive(sceneName);
        }
        else
        {
            LoadAdditive(sceneName);
        }
    }

    private Canvas GetScreen(LoadingScreenType type)
    {
        return type switch
        {
            LoadingScreenType.DreamGames => m_DreamGamesScreen,
            LoadingScreenType.RoyalMatch => m_RoyalMatchScreen,
            LoadingScreenType.Level => m_LevelLoadingScreen,
            _ => null
        };
    }

    private void LoadAdditive(string sceneName)
    {
        var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        loadOp.completed += _ =>
        {
            m_CurrentLoadedScene = sceneName;

            float elapsed = Time.time - m_LoadStartTime;
            float remaining = m_CurrentMinDuration - elapsed;

            if (remaining > 0f)
                m_CoroutineRunner.StartCoroutine(HideAfterDelay(remaining));
            else
                HideLoadingScreen();
        };
    }

    private IEnumerator HideAfterDelay(float remaining)
    {
        yield return new WaitForSeconds(remaining);
        HideLoadingScreen();
    }

    private void HideLoadingScreen()
    {
        if (m_ActiveLoadingScreen)
            m_ActiveLoadingScreen.gameObject.SetActive(false);

        m_ActiveLoadingScreen = null;
    }
}

public enum LoadingScreenType
{
    DreamGames,
    RoyalMatch,
    Level
}