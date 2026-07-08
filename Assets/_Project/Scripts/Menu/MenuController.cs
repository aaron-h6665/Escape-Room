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
        SceneManager.LoadScene(NewGameLevel);
    }

    public void LoadGameDialogYes()
    {
        if (SaveFileUtility.Exists(SaveFileName))
        {
            SceneManager.LoadScene(NewGameLevel);
            return;
        }

        Debug.LogWarning("No saved game was found at " + SaveFileUtility.GetPath(SaveFileName));
    }

    public void ExitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void WireButtons()
    {
        newGameButton = newGameButton != null ? newGameButton : FindButton("NewGameButton");
        loadGameButton = loadGameButton != null ? loadGameButton : FindButton("LoadGameButton (1)");
        exitButton = exitButton != null ? exitButton : FindButton("ExitButton");

        BindButton(newGameButton, NewGameDialogYes);
        BindButton(loadGameButton, LoadGameDialogYes);
        BindButton(exitButton, ExitButton);
    }

    private Button FindButton(string buttonName)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }

    private void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
    }
}
