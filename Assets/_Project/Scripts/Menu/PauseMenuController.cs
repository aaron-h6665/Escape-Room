using UnityEngine;
using System.Linq;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private string fallbackLevelSceneName = "Level";

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button saveGameButton;
    [SerializeField] private Button exitButton;

    private void Awake()
    {
        foreach (Canvas canvas in gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))) canvas.sortingOrder = 2500;
        resumeButton = MenuButtonBinder.BindByName(this, resumeButton, "ResumeButton", ResumeGame);
        saveGameButton = MenuButtonBinder.BindByName(this, saveGameButton, "SaveGameButton", SaveGame);
        exitButton = MenuButtonBinder.BindByName(this, exitButton, "ExitButton", ExitGame);
    }

    public void ResumeGame()
    {
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.ResumeGame();
            return;
        }

        SceneTransitionService.LoadScene(fallbackLevelSceneName);
    }

    public void SaveGame()
    {
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.SaveGame();
            return;
        }

        if (DataPersistenceManager.instance != null)
        {
            DataPersistenceManager.instance.SaveGame();
            return;
        }

        Debug.LogWarning("Save Game was pressed, but no DataPersistenceManager is available.");
    }

    public void ExitGame()
    {
        GameExitUtility.ExitGame();
    }
}
