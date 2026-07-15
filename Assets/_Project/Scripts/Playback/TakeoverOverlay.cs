using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TakeoverOverlay : MonoBehaviour
{
    GameObject canvasObject;
    Button takeoverButton;
    bool wasVisible;
    bool previousCursorVisible;
    CursorLockMode previousCursorLockMode;

    void Start()
    {
        BuildOverlay();
        SetVisible(false);
    }

    void Update()
    {
        bool shouldShow = ReplayManager.IsPlaybackActive() && (PauseManager.Instance == null || !PauseManager.Instance.IsPaused);
        if (shouldShow == wasVisible)
        {
            return;
        }

        SetVisible(shouldShow);
    }

    void OnDestroy()
    {
        if (wasVisible)
        {
            RestoreCursor();
        }
    }

    void BuildOverlay()
    {
        canvasObject = new GameObject("Takeover Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject buttonObject = new GameObject("Take Over Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvasObject.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 70f);
        buttonRect.sizeDelta = new Vector2(420f, 120f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.94f, 0.58f, 0.08f, 0.96f);
        image.raycastTarget = true;

        takeoverButton = buttonObject.GetComponent<Button>();
        ColorBlock colors = takeoverButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.72f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        takeoverButton.colors = colors;
        takeoverButton.onClick.AddListener(RequestTakeover);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = "TAKE OVER\n<size=28>Click, press T, or pull RT</size>";
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.08f, 0.06f, 0.03f, 1f);
        label.fontSize = 42f;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
    }

    void RequestTakeover()
    {
        ReplayManager.instance?.TakeOver();
    }

    void SetVisible(bool visible)
    {
        if (canvasObject == null)
        {
            return;
        }

        if (visible)
        {
            previousCursorVisible = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else if (wasVisible)
        {
            RestoreCursor();
        }

        canvasObject.SetActive(visible);
        wasVisible = visible;
    }

    void RestoreCursor()
    {
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
    }
}
