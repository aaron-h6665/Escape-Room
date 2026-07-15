using UnityEngine;
using TMPro;

public class PlayerUI : MonoBehaviour, IReplayObject
{
    [SerializeField]
    private TextMeshProUGUI promptText;
    string currentPromptMessage = string.Empty;
    public bool PromptVisible => promptText == null || promptText.gameObject.activeSelf;

    void Start()
    {
        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    public void UpdateText(string promptMessage)
    {
        currentPromptMessage = promptMessage ?? string.Empty;
        if (promptText != null)
        {
            promptText.text = currentPromptMessage;
        }
    }

    public void SaveSnapshot(ref GameData data)
    {
        data.playerPromptText = currentPromptMessage;
    }

    public void LoadSnapshot(GameData data)
    {
        UpdateText(ReplayManager.IsPlaybackActive() ? string.Empty : data.playerPromptText);
    }

    public void SetPromptVisible(bool visible)
    {
        if (!visible)
        {
            UpdateText(string.Empty);
        }

        if (promptText != null)
        {
            promptText.gameObject.SetActive(visible);
        }
    }
}
