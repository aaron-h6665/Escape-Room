using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerMotor))]
public sealed class DeathCounterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text counterText;

    private PlayerMotor playerMotor;
    private GameObject runtimeCanvas;

    private void Awake()
    {
        playerMotor = GetComponent<PlayerMotor>();

        if (counterText == null)
        {
            CreateCounterUI();
        }

        UpdateCounter(playerMotor.DeathCount);
    }

    private void OnEnable()
    {
        if (playerMotor == null)
        {
            playerMotor = GetComponent<PlayerMotor>();
        }

        playerMotor.DeathCountChanged += UpdateCounter;
    }

    private void OnDisable()
    {
        if (playerMotor != null)
        {
            playerMotor.DeathCountChanged -= UpdateCounter;
        }
    }

    private void OnDestroy()
    {
        if (runtimeCanvas != null)
        {
            Destroy(runtimeCanvas);
        }
    }

    private void UpdateCounter(int deathCount)
    {
        if (counterText != null)
        {
            counterText.text = $"DEATHS  {deathCount}";
        }
    }

    private void CreateCounterUI()
    {
        runtimeCanvas = new GameObject(
            "Death Counter Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        Canvas canvas = runtimeCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = runtimeCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject(
            "Death Counter Panel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        panelObject.transform.SetParent(runtimeCanvas.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(24f, -24f);
        panelRect.sizeDelta = new Vector2(230f, 58f);

        Image panel = panelObject.GetComponent<Image>();
        panel.color = new Color(0.03f, 0.04f, 0.06f, 0.82f);
        panel.raycastTarget = false;

        GameObject textObject = new GameObject(
            "Death Counter Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 8f);
        textRect.offsetMax = new Vector2(-18f, -8f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.color = new Color(1f, 0.28f, 0.35f, 1f);
        text.fontSize = 27f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        counterText = text;
    }
}
