using UnityEngine;
using TMPro;

public class PlayerUI : MonoBehaviour, IReplayObject
{
    [SerializeField]
    private TextMeshProUGUI promptText;
    string currentPromptMessage = string.Empty;
    public string CurrentPromptMessage => currentPromptMessage;
    public bool PromptVisible => promptText == null || promptText.gameObject.activeSelf;

    void Awake()
    {
        if (promptText == null)
        {
            promptText = CreateRuntimePrompt();
        }
    }

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
        UpdateText(data != null ? data.playerPromptText : string.Empty);
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

    TextMeshProUGUI CreateRuntimePrompt()
    {
        GameObject canvasObject = new GameObject(
            "Runtime Interaction Prompt Canvas",
            typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        GameObject textObject = new GameObject(
            "Interaction Prompt",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.15f, 0.04f);
        rectTransform.anchorMax = new Vector2(0.85f, 0.12f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI runtimePrompt = textObject.GetComponent<TextMeshProUGUI>();
        runtimePrompt.text = string.Empty;
        runtimePrompt.alignment = TextAlignmentOptions.Center;
        runtimePrompt.fontSize = 28f;
        runtimePrompt.color = Color.white;
        runtimePrompt.raycastTarget = false;
        return runtimePrompt;
    }
}
