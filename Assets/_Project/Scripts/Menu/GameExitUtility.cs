using UnityEngine;

public static class GameExitUtility
{
    public static void ExitGame()
    {
        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.StopRecording();
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
