using System.Collections;
using UnityEngine;

public class GameInitiator : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int m_MaxLevel = 10;
    [SerializeField] private float m_SplashDuration = 2f;
    [SerializeField] private float m_MinLoadingDuration = 2f;
    [SerializeField] private float m_MinLevelLoadingDuration = 1f;

    [Header("Scene References")]
    [SerializeField] private Canvas m_DreamGamesScreen;
    [SerializeField] private Canvas m_RoyalMatchScreen;
    [SerializeField] private Canvas m_LevelLoadingScreen;

    private PlayerDataManager m_PlayerDataManager;
    private LevelLoader m_LevelLoader;
    private SceneLoader m_SceneLoader;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject); // children come with it

        m_PlayerDataManager = new PlayerDataManager(m_MaxLevel);
        m_LevelLoader = new LevelLoader();

        // Make sure all start hidden
        m_DreamGamesScreen.gameObject.SetActive(false);
        m_RoyalMatchScreen.gameObject.SetActive(false);
        m_LevelLoadingScreen.gameObject.SetActive(false);

        // Give canvases to SceneLoader ONCE
        m_SceneLoader = new SceneLoader(
            this,
            m_MinLoadingDuration,
            m_MinLevelLoadingDuration,
            m_DreamGamesScreen,
            m_RoyalMatchScreen,
            m_LevelLoadingScreen
        );
        StartCoroutine(StartupSequence());
    }

    private IEnumerator StartupSequence()
    {
        // Splash
        m_DreamGamesScreen.gameObject.SetActive(true);
        yield return new WaitForSeconds(m_SplashDuration);
        m_DreamGamesScreen.gameObject.SetActive(false);

        // Go to main menu with RoyalMatch loading screen
        SceneLoader.Instance.LoadScene("MainMenuScene", LoadingScreenType.RoyalMatch);
    }
}