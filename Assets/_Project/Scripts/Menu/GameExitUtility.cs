using UnityEngine;

public static class GameExitUtility
{
    public static void ExitGame()
    {
        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Stop();
            if (!ReplayManager.instance.RetrySave()) return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
