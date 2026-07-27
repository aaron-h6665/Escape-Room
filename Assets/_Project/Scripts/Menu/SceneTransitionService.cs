using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Routes single-scene changes through the project's loading scene.
/// The pending destination survives the short-lived loading scene because it is
/// stored here rather than on an object in the outgoing scene.
/// </summary>
public static class SceneTransitionService
{
    public const string LoadingSceneName = "LoadingScene";

    private static string pendingSceneName;
    private static bool transitionInProgress;

    public static bool IsTransitionInProgress => transitionInProgress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        pendingSceneName = null;
        transitionInProgress = false;
    }

    public static void LoadScene(string sceneName, LoadSceneMode loadMode = LoadSceneMode.Single)
    {
        if (transitionInProgress)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("A scene transition was requested without a destination scene name.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Cannot load scene '{sceneName}'. Add it to Build Settings and verify the name.");
            return;
        }

        // Additive loading has different lifetime semantics: replacing the source
        // with an intermediate scene would silently turn it into a single load.
        if (loadMode != LoadSceneMode.Single)
        {
            SceneManager.LoadScene(sceneName, loadMode);
            return;
        }

        if (sceneName == LoadingSceneName)
        {
            SceneManager.LoadScene(sceneName, loadMode);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(LoadingSceneName))
        {
            Debug.LogWarning(
                $"Loading scene '{LoadingSceneName}' is unavailable. Loading '{sceneName}' directly instead.");
            SceneManager.LoadScene(sceneName, loadMode);
            return;
        }

        pendingSceneName = sceneName;
        transitionInProgress = true;
        SceneManager.LoadScene(LoadingSceneName, LoadSceneMode.Single);
    }

    public static bool TryTakePendingScene(out string sceneName)
    {
        sceneName = pendingSceneName;
        pendingSceneName = null;
        return !string.IsNullOrWhiteSpace(sceneName);
    }

    public static void CompleteTransition()
    {
        transitionInProgress = false;
        pendingSceneName = null;
    }
}
