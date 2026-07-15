using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header("Levels To Load")]
    [SerializeField] private string newGameLevel = "Level";
    [SerializeField] private string saveFileName = SaveFileUtility.DefaultSaveFileName;

    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button exitButton;

    // Keep the old field name so existing prefab/scene data still has a place to deserialize.
    [HideInInspector] public string _newGameLevel;

    private string NewGameLevel => string.IsNullOrWhiteSpace(newGameLevel) ? _newGameLevel : newGameLevel;

    private string SaveFileName => string.IsNullOrWhiteSpace(saveFileName) ? SaveFileUtility.DefaultSaveFileName : saveFileName;

    private void Awake()
    {
        WireButtons();
    }

    public void NewGameDialogYes()
    {
        SaveFileUtility.Delete(SaveFileName);
        ReplayManager.QueueRecordingOnNextScene();
        SceneManager.LoadScene(NewGameLevel);
    }

    public void LoadGameDialogYes()
    {
        ReplayManager.QueuePlaybackOnNextScene();
        SceneManager.LoadScene(NewGameLevel);
    }

    public void ExitButton()
    {
        GameExitUtility.ExitGame();
    }

    private void WireButtons()
    {
        newGameButton = MenuButtonBinder.BindByName(this, newGameButton, "NewGameButton", NewGameDialogYes);
        loadGameButton = MenuButtonBinder.BindByName(this, loadGameButton, "LoadGameButton (1)", LoadGameDialogYes);
        exitButton = MenuButtonBinder.BindByName(this, exitButton, "ExitButton", ExitButton);
    }
}
