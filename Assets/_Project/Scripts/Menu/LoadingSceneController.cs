using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class LoadingSceneController : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private string fallbackSceneName = "MenuScene";

    [Header("Timing")]
    [SerializeField, Min(0.25f)] private float minimumDisplayTime = 1.35f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.35f;

    [Header("Existing Scene Art")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text loadingLabel;
    [SerializeField] private Image progressTrack;

    private static readonly string[] Tips =
    {
        "Every room tells you how to leave it.",
        "Listen before you move. The walls give things away.",
        "A stubborn lock may be waiting for a pattern, not a key.",
        "What changed can often be changed back.",
        "Keep track of the details that feel out of place."
    };

    private readonly List<GameObject> loadingVisuals = new List<GameObject>();
    private Image progressFill;
    private Image progressGlow;
    private Image blackout;
    private TMP_Text percentLabel;
    private TMP_Text statusLabel;
    private TMP_Text tipLabel;
    private TMP_Text protocolLabel;
    private RectTransform backgroundRect;
    private RectTransform progressGlowRect;
    private float displayedProgress;
    private float sceneStartTime;
    private float nextTipChangeTime;
    private int tipIndex;
    private string baseLoadingText = "Opening the next room";

    private void Awake()
    {
        ResolveExistingReferences();
        BuildInterface();
        sceneStartTime = Time.realtimeSinceStartup;
    }

    private void Start()
    {
        StartCoroutine(LoadDestination());
    }

    private void Update()
    {
        float time = Time.unscaledTime;

        if (loadingLabel != null)
        {
            int dotCount = 1 + Mathf.FloorToInt(time * 2f) % 3;
            loadingLabel.text = baseLoadingText + new string('.', dotCount);
        }

        if (backgroundRect != null)
        {
            float drift = Mathf.Sin(time * 0.35f);
            backgroundRect.localScale = Vector3.one * (1.035f + 0.008f * drift);
            backgroundRect.anchoredPosition = new Vector2(8f * drift, 3f * Mathf.Cos(time * 0.28f));
        }

        if (progressGlowRect != null)
        {
            float glowPulse = 0.85f + 0.15f * Mathf.Sin(time * 5f);
            progressGlow.color = new Color(0.93f, 0.73f, 0.3f, glowPulse);
        }

        if (tipLabel != null && time >= nextTipChangeTime)
        {
            tipIndex = (tipIndex + 1) % Tips.Length;
            tipLabel.text = Tips[tipIndex];
            nextTipChangeTime = time + 2.8f;
        }
    }

    private IEnumerator LoadDestination()
    {
        // Let the loading scene render once before beginning potentially heavy work.
        yield return null;

        string destination;
        if (!SceneTransitionService.TryTakePendingScene(out destination))
        {
            destination = fallbackSceneName;
        }

        if (string.IsNullOrWhiteSpace(destination) || !Application.CanStreamedLevelBeLoaded(destination))
        {
            Debug.LogError($"Loading scene cannot reach destination '{destination}'.", this);
            SceneTransitionService.CompleteTransition();
            yield break;
        }

        SetDestinationCopy(destination);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Debug.LogError($"Unity could not begin loading scene '{destination}'.", this);
            SceneTransitionService.CompleteTransition();
            yield break;
        }

        loadOperation.allowSceneActivation = false;

        while (loadOperation.progress < 0.9f || Time.realtimeSinceStartup - sceneStartTime < minimumDisplayTime)
        {
            float loadProgress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            float timeProgress = Mathf.Clamp01((Time.realtimeSinceStartup - sceneStartTime) / minimumDisplayTime);
            float pacedProgress = Mathf.Min(loadProgress, Mathf.SmoothStep(0f, 1f, timeProgress));
            displayedProgress = Mathf.MoveTowards(displayedProgress, pacedProgress, Time.unscaledDeltaTime * 1.6f);
            UpdateProgress(displayedProgress);
            yield return null;
        }

        while (displayedProgress < 1f)
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, 1f, Time.unscaledDeltaTime * 2.5f);
            UpdateProgress(displayedProgress);
            yield return null;
        }

        statusLabel.text = "DOOR READY";
        yield return new WaitForSecondsRealtime(0.12f);

        yield return FadeBlackout(0f, 1f);

        // Keep only the black overlay alive long enough to reveal the destination
        // with a smooth fade after Unity activates the new scene.
        DontDestroyOnLoad(gameObject);
        SceneTransitionService.CompleteTransition();
        loadOperation.allowSceneActivation = true;
        while (!loadOperation.isDone)
        {
            yield return null;
        }

        foreach (GameObject visual in loadingVisuals)
        {
            if (visual != null)
            {
                visual.SetActive(false);
            }
        }

        yield return null;
        yield return FadeBlackout(1f, 0f);
        Destroy(gameObject);
    }

    private void ResolveExistingReferences()
    {
        if (backgroundImage == null)
        {
            Transform candidate = transform.Find("LoadingImage");
            backgroundImage = candidate != null ? candidate.GetComponent<Image>() : null;
        }

        if (loadingLabel == null)
        {
            Transform candidate = transform.Find("Text (TMP)");
            loadingLabel = candidate != null ? candidate.GetComponent<TMP_Text>() : null;
        }

        if (progressTrack == null)
        {
            Transform candidate = transform.Find("Image");
            progressTrack = candidate != null ? candidate.GetComponent<Image>() : null;
        }
    }

    private void BuildInterface()
    {
        if (backgroundImage != null)
        {
            loadingVisuals.Add(backgroundImage.gameObject);
            backgroundRect = backgroundImage.rectTransform;
            Stretch(backgroundRect);
            backgroundImage.color = new Color(0.62f, 0.68f, 0.78f, 1f);
            backgroundImage.raycastTarget = false;
            backgroundImage.transform.SetAsFirstSibling();
        }

        Image veil = CreateImage("Atmosphere", transform, new Color(0.015f, 0.025f, 0.045f, 0.58f));
        Stretch(veil.rectTransform);
        veil.transform.SetSiblingIndex(backgroundImage != null ? 1 : 0);
        loadingVisuals.Add(veil.gameObject);

        Image topRule = CreateImage("TopRule", transform, new Color(0.93f, 0.73f, 0.3f, 0.9f));
        Place(topRule.rectTransform, new Vector2(0.08f, 0.875f), new Vector2(0.32f, 0.879f));
        loadingVisuals.Add(topRule.gameObject);

        protocolLabel = CreateText("Protocol", "ESCAPE PROTOCOL // TRANSIT", 17f, FontStyles.Bold);
        Place(protocolLabel.rectTransform, new Vector2(0.08f, 0.89f), new Vector2(0.62f, 0.94f));
        protocolLabel.color = new Color(0.93f, 0.73f, 0.3f, 1f);

        if (loadingLabel != null)
        {
            loadingVisuals.Add(loadingLabel.gameObject);
            Place(loadingLabel.rectTransform, new Vector2(0.08f, 0.19f), new Vector2(0.82f, 0.27f));
            loadingLabel.fontSize = 46f;
            loadingLabel.enableAutoSizing = true;
            loadingLabel.fontSizeMin = 28f;
            loadingLabel.fontSizeMax = 46f;
            loadingLabel.fontStyle = FontStyles.Bold;
            loadingLabel.alignment = TextAlignmentOptions.BottomLeft;
            loadingLabel.color = Color.white;
            loadingLabel.raycastTarget = false;
            loadingLabel.text = baseLoadingText + ".";
        }

        statusLabel = CreateText("Status", "SECURING LAST ROOM", 14f, FontStyles.Bold);
        Place(statusLabel.rectTransform, new Vector2(0.08f, 0.155f), new Vector2(0.55f, 0.19f));
        statusLabel.color = new Color(0.72f, 0.76f, 0.82f, 1f);

        percentLabel = CreateText("Percent", "00%", 14f, FontStyles.Bold);
        Place(percentLabel.rectTransform, new Vector2(0.74f, 0.155f), new Vector2(0.82f, 0.19f));
        percentLabel.alignment = TextAlignmentOptions.TopRight;
        percentLabel.color = new Color(0.93f, 0.73f, 0.3f, 1f);

        ConfigureProgressBar();

        tipLabel = CreateText("Tip", Tips[0], 16f, FontStyles.Italic);
        Place(tipLabel.rectTransform, new Vector2(0.08f, 0.06f), new Vector2(0.82f, 0.115f));
        tipLabel.color = new Color(0.76f, 0.8f, 0.86f, 0.92f);

        blackout = CreateImage("Blackout", transform, new Color(0f, 0f, 0f, 0f));
        Stretch(blackout.rectTransform);
        blackout.transform.SetAsLastSibling();
    }

    private void ConfigureProgressBar()
    {
        if (progressTrack == null)
        {
            progressTrack = CreateImage("ProgressTrack", transform, new Color(1f, 1f, 1f, 0.16f));
        }

        loadingVisuals.Add(progressTrack.gameObject);
        Place(progressTrack.rectTransform, new Vector2(0.08f, 0.132f), new Vector2(0.82f, 0.14f));
        progressTrack.color = new Color(1f, 1f, 1f, 0.16f);
        progressTrack.raycastTarget = false;

        progressFill = CreateImage("ProgressFill", progressTrack.transform, new Color(0.93f, 0.73f, 0.3f, 1f));
        Stretch(progressFill.rectTransform);
        progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);

        progressGlow = CreateImage("ProgressGlow", progressTrack.transform, new Color(0.93f, 0.73f, 0.3f, 1f));
        progressGlowRect = progressGlow.rectTransform;
        progressGlowRect.anchorMin = new Vector2(0f, 0.5f);
        progressGlowRect.anchorMax = new Vector2(0f, 0.5f);
        progressGlowRect.pivot = new Vector2(0.5f, 0.5f);
        progressGlowRect.sizeDelta = new Vector2(3f, 18f);
        progressGlowRect.anchoredPosition = Vector2.zero;
    }

    private void SetDestinationCopy(string destination)
    {
        protocolLabel.text = $"ESCAPE PROTOCOL // {destination.ToUpperInvariant()}";
        baseLoadingText = "Opening the next room";

        tipIndex = 0;
        foreach (char character in destination)
        {
            tipIndex = (tipIndex * 31 + character) % Tips.Length;
        }

        tipLabel.text = Tips[tipIndex];
        nextTipChangeTime = Time.unscaledTime + 2.8f;
    }

    private void UpdateProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        progressFill.rectTransform.anchorMax = new Vector2(progress, 1f);
        progressGlowRect.anchorMin = new Vector2(progress, 0.5f);
        progressGlowRect.anchorMax = new Vector2(progress, 0.5f);
        percentLabel.text = $"{Mathf.RoundToInt(progress * 100f):00}%";

        if (progress < 0.18f)
        {
            statusLabel.text = "SECURING LAST ROOM";
        }
        else if (progress < 0.7f)
        {
            statusLabel.text = "MAPPING THE NEXT CHAMBER";
        }
        else if (progress < 0.96f)
        {
            statusLabel.text = "SETTING THE LOCKS";
        }
        else
        {
            statusLabel.text = "DOOR READY";
        }
    }

    private IEnumerator FadeBlackout(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration));
            blackout.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }

        blackout.color = new Color(0f, 0f, 0f, to);
    }

    private TMP_Text CreateText(string objectName, string text, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(transform, false);
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        if (loadingLabel != null)
        {
            label.font = loadingLabel.font;
        }
        loadingVisuals.Add(textObject);
        return label;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = parent.gameObject.layer;
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void Place(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.pivot = new Vector2(0f, 0.5f);
    }
}
