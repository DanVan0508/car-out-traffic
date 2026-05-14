using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public GameObject winPanel;
    public TextMeshProUGUI winText;
    public Button nextLevelButton;
    public Button hintButton;

    public TextMeshProUGUI levelText;

    public Button restartButton;

    public GameObject mainMenuPanel;
    public Button startButton;

    void Start()
    {
        winPanel.SetActive(false);
        
        // Show main menu if it's the first time in this session or always
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            startButton.onClick.AddListener(OnStartClicked);
        }

        UpdateLevelUI();
        if (hintButton != null)
{
            hintButton.onClick.AddListener(OnHintClicked);
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
        }
    }

    public void OnStartClicked()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
    }

    public void OnRestartClicked()
{
        GridManager gm = Object.FindAnyObjectByType<GridManager>();
        if (gm != null)
        {
            gm.RestartLevel();
            winPanel.SetActive(false);
        }
    }

    public void UpdateLevelUI()
    {
        GridManager gm = Object.FindAnyObjectByType<GridManager>();
        if (gm != null && levelText != null)
        {
            levelText.text = "LEVEL " + (gm.GetCurrentLevelIndex() + 1);
        }
    }

    public void OnHintClicked()
    {
        GridManager gm = Object.FindAnyObjectByType<GridManager>();
        if (gm != null)
        {
            gm.ShowHint();
        }
    }

    public void ShowWinPanel()
    {
        winPanel.SetActive(true);
    }

    public void OnNextLevelClicked()
    {
        GridManager gm = Object.FindAnyObjectByType<GridManager>();
        if (gm != null)
        {
            gm.NextLevel();
            winPanel.SetActive(false);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
}
