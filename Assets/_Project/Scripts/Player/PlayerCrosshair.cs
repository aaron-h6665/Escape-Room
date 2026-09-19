using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerCrosshair : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private Color crosshairColor = new Color(1f, 1f, 1f, 0.94f);
    [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField, Min(1f)] private float armLength = 8f;
    [SerializeField, Min(1f)] private float armThickness = 2f;
    [SerializeField, Min(0f)] private float centerGap = 5f;
    [SerializeField, Min(0f)] private float outlineThickness = 1.5f;
    [SerializeField, Min(0f)] private float centerDotSize = 2f;

    [Header("Visibility")]
    [Tooltip("Hides the crosshair while the pause menu, a note, or a puzzle has control of the player.")]
    [SerializeField] private bool hideWhenPlayerControlIsLocked = true;

    private Canvas crosshairCanvas;
    private InputManager inputManager;
    private bool presentationVisible = true;

    public bool IsVisible => crosshairCanvas != null
        && crosshairCanvas.enabled
        && crosshairCanvas.gameObject.activeInHierarchy;

    private void Awake()
    {
        inputManager = GetComponent<InputManager>();
        CreateCrosshair();
        RefreshVisibility();
    }

    private void LateUpdate()
    {
        if (inputManager == null)
        {
            inputManager = GetComponent<InputManager>();
        }

        RefreshVisibility();
    }

    private void OnValidate()
    {
        armLength = Mathf.Max(1f, armLength);
        armThickness = Mathf.Max(1f, armThickness);
        centerGap = Mathf.Max(0f, centerGap);
        outlineThickness = Mathf.Max(0f, outlineThickness);
        centerDotSize = Mathf.Max(0f, centerDotSize);
    }

    public void SetPresentationVisible(bool visible)
    {
        presentationVisible = visible;
        RefreshVisibility();
    }

    private void CreateCrosshair()
    {
        GameObject canvasObject = new GameObject(
            "Player Crosshair Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        crosshairCanvas = canvasObject.GetComponent<Canvas>();
        crosshairCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        crosshairCanvas.sortingOrder = 900;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject reticleObject = new GameObject("Crosshair", typeof(RectTransform));
        reticleObject.layer = gameObject.layer;
        reticleObject.transform.SetParent(canvasObject.transform, false);
        RectTransform reticle = reticleObject.GetComponent<RectTransform>();
        reticle.anchorMin = new Vector2(0.5f, 0.5f);
        reticle.anchorMax = new Vector2(0.5f, 0.5f);
        reticle.pivot = new Vector2(0.5f, 0.5f);
        reticle.anchoredPosition = Vector2.zero;
        reticle.sizeDelta = new Vector2(48f, 48f);

        float armOffset = centerGap * 0.5f + armLength * 0.5f;
        CreateArm(reticle, "Left", new Vector2(-armOffset, 0f), new Vector2(armLength, armThickness));
        CreateArm(reticle, "Right", new Vector2(armOffset, 0f), new Vector2(armLength, armThickness));
        CreateArm(reticle, "Top", new Vector2(0f, armOffset), new Vector2(armThickness, armLength));
        CreateArm(reticle, "Bottom", new Vector2(0f, -armOffset), new Vector2(armThickness, armLength));

        if (centerDotSize > 0f)
        {
            CreateOutlinedRectangle(reticle, "Center Dot", Vector2.zero, Vector2.one * centerDotSize);
        }
    }

    private void CreateArm(RectTransform parent, string armName, Vector2 position, Vector2 size)
    {
        CreateOutlinedRectangle(parent, armName, position, size);
    }

    private void CreateOutlinedRectangle(RectTransform parent, string elementName, Vector2 position, Vector2 size)
    {
        if (outlineThickness > 0f)
        {
            CreateRectangle(
                parent,
                elementName + " Outline",
                position,
                size + Vector2.one * (outlineThickness * 2f),
                outlineColor);
        }

        CreateRectangle(parent, elementName, position, size, crosshairColor);
    }

    private static void CreateRectangle(
        RectTransform parent,
        string elementName,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject rectangleObject = new GameObject(
            elementName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        rectangleObject.layer = parent.gameObject.layer;
        rectangleObject.transform.SetParent(parent, false);

        RectTransform rectangle = rectangleObject.GetComponent<RectTransform>();
        rectangle.anchorMin = new Vector2(0.5f, 0.5f);
        rectangle.anchorMax = new Vector2(0.5f, 0.5f);
        rectangle.pivot = new Vector2(0.5f, 0.5f);
        rectangle.anchoredPosition = position;
        rectangle.sizeDelta = size;

        Image image = rectangleObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void RefreshVisibility()
    {
        if (crosshairCanvas == null)
        {
            return;
        }

        bool controlIsLocked = inputManager != null && inputManager.PlayerControlLocked;
        crosshairCanvas.enabled = presentationVisible
            && (!hideWhenPlayerControlIsLocked || !controlIsLocked);
    }
}
