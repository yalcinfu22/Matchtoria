using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelEndPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text m_Title;
    [SerializeField] private TMP_Text m_LevelText;
    [SerializeField] private Button m_RetryButton;
    [SerializeField] private Button m_MainMenuButton;

    private void Awake()
    {
        m_RetryButton.onClick.AddListener(() => SceneLoader.Instance.LoadScene("LevelScene", LoadingScreenType.Level));
        m_MainMenuButton.onClick.AddListener(() => SceneLoader.Instance.LoadScene("MainMenuScene", LoadingScreenType.RoyalMatch));
    }

    public void Show(bool isWon, int levelNumber)
    {
        m_LevelText.text = $"Level {levelNumber}";
        m_Title.text = isWon ? "You Won!" : "You Lost!";
        m_RetryButton.gameObject.SetActive(!isWon);
        gameObject.SetActive(true);
    }
}
