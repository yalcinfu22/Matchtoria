using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button m_PlayButton;
    [SerializeField] private TextMeshProUGUI m_PlayButtonText;

    private void OnEnable()
    {
        // Always start clean, then add exactly one listener
        m_PlayButton.onClick.RemoveListener(OnPlayButtonClicked);
        m_PlayButton.onClick.AddListener(OnPlayButtonClicked);

        Refresh();
    }

    private void OnDisable()
    {
        // Good hygiene: remove when UI is disabled/destroyed
        m_PlayButton.onClick.RemoveListener(OnPlayButtonClicked);
    }

    private void Refresh()
    {
        int currentLevel = PlayerDataManager.Instance.CurrentLevel;

        if (PlayerDataManager.Instance.IsMaxLevelReached)
        {
            m_PlayButtonText.text = "Coming soon!";
            m_PlayButton.interactable = false;
        }
        else
        {
            m_PlayButtonText.text = $"Level {currentLevel}";
            m_PlayButton.interactable = true;
        }
    }

    private void OnPlayButtonClicked()
    {
        m_PlayButton.interactable = false;
        SceneLoader.Instance.LoadScene("LevelScene", LoadingScreenType.Level);
    }
}
