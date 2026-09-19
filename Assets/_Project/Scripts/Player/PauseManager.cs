using UnityEngine;
using UnityEngine.SceneManagement;
using Action = System.Action;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }
    public static event Action PauseStarting;

    [SerializeField] private string pauseSceneName = "PauseScene";
    [SerializeField] private InputManager inputManager;

    private bool isPaused;
    public bool IsPaused => isPaused;
    private bool previousPlayerControlLocked;
    private bool previousCursorVisible;
    private float previousTimeScale = 1f;
    private CursorLockMode previousCursorLockState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Found more than one PauseManager in the scene.");
            enabled = false;
            return;
        }

        Instance = this;
        ResolveInputManager();
    }

    private void OnEnable()
    {
        ResolveInputManager();
        if (inputManager != null)
        {
            inputManager.PausePressed += TogglePause;
        }
    }

    private void OnDisable()
    {
        if (inputManager != null)
        {
            inputManager.PausePressed -= TogglePause;
        }
    }

    private void OnDestroy()
    {
        if (isPaused)
        {
            RestoreGameplayState();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
            return;
        }

        PauseGame();
    }

    public void PauseGame()
    {
        if (isPaused || SceneManager.GetSceneByName(pauseSceneName).isLoaded)
        {
            return;
        }

        ReplayManager.instance?.RecordSessionEvent("paused");
        PauseStarting?.Invoke();
        ResolveInputManager();

        previousTimeScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLockState = Cursor.lockState;
        previousPlayerControlLocked = inputManager != null && inputManager.PlayerControlLocked;

        isPaused = true;
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (inputManager != null)
        {
            inputManager.AcquireControl(this);
        }

        SceneManager.LoadScene(pauseSceneName, LoadSceneMode.Additive);
    }

    public void ResumeGame()
    {
        if (!isPaused)
        {
            return;
        }

        Scene pauseScene = SceneManager.GetSceneByName(pauseSceneName);
        if (pauseScene.isLoaded)
        {
            SceneManager.UnloadSceneAsync(pauseScene);
        }

        RestoreGameplayState();
        isPaused = false;
        ReplayManager.instance?.RecordSessionEvent("resumed");
    }

    public void SaveGame()
    {
        if (DataPersistenceManager.instance == null)
        {
            Debug.LogWarning("Save Game was pressed, but no DataPersistenceManager is available.");
            return;
        }

        DataPersistenceManager.instance.SaveGame();
    }

    private void RestoreGameplayState()
    {
        Time.timeScale = previousTimeScale;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockState;

        if (inputManager != null)
        {
            inputManager.ReleaseControl(this);
        }
    }

    private void ResolveInputManager()
    {
        if (inputManager == null)
        {
#if UNITY_2023_1_OR_NEWER
            inputManager = Object.FindFirstObjectByType<InputManager>();
#else
            inputManager = Object.FindObjectOfType<InputManager>();
#endif
        }
    }
}
