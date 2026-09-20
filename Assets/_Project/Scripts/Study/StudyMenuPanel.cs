using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Neutral optional menus, shared by main menu, pause and completion.</summary>
public sealed class StudyMenuPanel : MonoBehaviour
{
    static StudyMenuPanel instance;
    Canvas canvas;
    GameObject shortcuts, panel;
    bool completion;
    string page = "";
    bool? controllerOverride;
    TMP_Text controlsText, errorText;
    GameObject previousSelection;
    bool cursorVisible;
    CursorLockMode cursorLock;
    InputManager lockedInput;
    readonly List<Selectable> suspendedControls = new List<Selectable>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance == null) new GameObject("Menu Accessories").AddComponent<StudyMenuPanel>();
    }
    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this; DontDestroyOnLoad(gameObject);
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 3000;
        var scale = gameObject.AddComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scale.referenceResolution = new Vector2(1280, 720); scale.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        shortcuts = new GameObject("Options Shortcut", typeof(RectTransform)); shortcuts.transform.SetParent(transform, false);
        ButtonAt(shortcuts.transform, "Options", new Vector2(-95, 40), new Vector2(165, 44), OpenOptions, new Vector2(1, 0));
        errorText = Label(transform, "", new Vector2(0, 95), new Vector2(800, 35), 20, new Vector2(0.5f, 0));
        SceneManager.sceneLoaded += SceneLoaded;
        StudyOptions.Current.Apply(true);
    }
    void OnDestroy() { SceneManager.sceneLoaded -= SceneLoaded; if (instance == this) instance = null; Release(); }
    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) { Close(); completion = false; }
        EnsureEventSystem();
    }
    static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        else if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
        {
            var old = EventSystem.current.GetComponent<StandaloneInputModule>();
            if (old != null) old.enabled = false;
            EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }
    void Update()
    {
        StudyOptions.PollDevice();
        bool main = SceneManager.GetActiveScene().name == "MenuScene";
        bool paused = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
        shortcuts.SetActive(panel == null && !completion && (main || paused));
        shortcuts.transform.Find("Options").gameObject.SetActive(!main);
        if (page == "Controls") UpdateControls();
        if (panel != null && !completion && (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.buttonEast.wasPressedThisFrame == true)) Close();
        if (page != "" && !main && !paused && !completion) Close();
        var manager = ReplayManager.instance;
        errorText.text = manager != null ? manager.LastError : "";
        if (panel == null && !string.IsNullOrEmpty(errorText.text) && !main && !paused && !completion) OpenError();
        if (completion && manager?.SavePending == true) errorText.text = "Could not save. Please retry.";
        if ((main || paused || panel != null) && StudyOptions.UsingGamepad && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
        {
            var first = panel != null ? panel.GetComponentInChildren<Button>() : shortcuts.GetComponentInChildren<Button>();
            first?.Select();
        }
    }
    public void OpenControls()
    {
        BuildPanel("Controls");
        controlsText = Label(panel.transform, "", new Vector2(0, 0), new Vector2(880, 430), 24);
        controlsText.alignment = TextAlignmentOptions.TopLeft;
        ButtonAt(panel.transform, "Keyboard / mouse", new Vector2(-160, -245), new Vector2(245, 42), () => { controllerOverride = false; UpdateControls(); });
        ButtonAt(panel.transform, "Controller", new Vector2(150, -245), new Vector2(245, 42), () => { controllerOverride = true; UpdateControls(); });
        ButtonAt(panel.transform, "Back to options", new Vector2(0, -300), new Vector2(245, 42), OpenOptions);
        UpdateControls();
    }
    void UpdateControls()
    {
        if (controlsText == null) return;
        bool pad = controllerOverride ?? StudyOptions.UsingGamepad;
        controlsText.text = pad
            ? "<b>CONTROLLER</b>\nLeft stick     Move                  Right stick     Look\nSouth button     Jump             East button     Interact / close\nRT     Sprint                           Right stick press     Crouch\nLB / RB     Select item             West button     Drop\nSelect / View     Pause             RT     Take over\n\n<b>INSPECTION AND MENUS</b>\nD-pad left / right     Rotate decoder\nD-pad / stick     Navigate         South button     Select\nEast button     Close                 Letter keys     Enter text"
            : "<b>KEYBOARD / MOUSE</b>\nW A S D     Move                     Mouse     Look\nSpace     Jump                         E     Interact / close\nLeft Shift     Sprint                   C     Crouch\nScroll / 1–5     Select item         X     Drop\nP     Pause                               T / button     Take over\n\n<b>INSPECTION AND MENUS</b>\nA / D or ← / →     Rotate decoder\nMouse / arrows     Navigate     Enter     Select / submit\nEsc     Close                              Letters / Backspace     Edit text";
    }
    public static void ShowOptions() { Install(); instance.OpenOptions(); }
    public void OpenOptions()
    {
        BuildPanel("Options");
        OptionRow("Volume", 165, () => StudyOptions.Current.volume.ToString("P0"), v => StudyOptions.Current.volume += v * 0.1f);
        OptionRow("Mouse sensitivity", 80, () => StudyOptions.Current.mouseSensitivity.ToString("0.00"), v => StudyOptions.Current.mouseSensitivity += v * 0.25f);
        OptionRow("Controller sensitivity", -5, () => StudyOptions.Current.controllerSensitivity.ToString("0.00"), v => StudyOptions.Current.controllerSensitivity += v * 0.25f);
        ButtonAt(panel.transform, "Invert look: " + (StudyOptions.Current.invertLook ? "On" : "Off"), new Vector2(-205, -110), new Vector2(345, 45), () => { StudyOptions.Current.invertLook = !StudyOptions.Current.invertLook; StudyOptions.Current.Apply(); OpenOptions(); });
        ButtonAt(panel.transform, "Display: " + (StudyOptions.Current.fullscreen ? "Fullscreen" : "Windowed"), new Vector2(205, -110), new Vector2(345, 45), () => { StudyOptions.Current.fullscreen = !StudyOptions.Current.fullscreen; StudyOptions.Current.Apply(true); OpenOptions(); });
        ButtonAt(panel.transform, "Resolution: " + StudyOptions.Current.width + " × " + StudyOptions.Current.height, new Vector2(0, -205), new Vector2(400, 45), () =>
        {
            int[] widths = { 1024, 1280, 1920 }; int[] heights = { 768, 720, 1080 };
            int index = (Array.IndexOf(widths, StudyOptions.Current.width) + 1) % widths.Length;
            StudyOptions.Current.width = widths[index]; StudyOptions.Current.height = heights[index];
            StudyOptions.Current.Apply(true); OpenOptions();
        });
        ButtonAt(panel.transform, "Controls", new Vector2(0, -270), new Vector2(245, 45), OpenControls);
    }
    void OptionRow(string name, float y, Func<string> value, Action<int> change)
    {
        var label = Label(panel.transform, name + "   " + value(), new Vector2(-95, y), new Vector2(570, 48), 25);
        ButtonAt(panel.transform, "−", new Vector2(255, y), new Vector2(60, 45), () => { change(-1); StudyOptions.Current.Apply(); label.text = name + "   " + value(); });
        ButtonAt(panel.transform, "+", new Vector2(335, y), new Vector2(60, 45), () => { change(1); StudyOptions.Current.Apply(); label.text = name + "   " + value(); });
    }
    void OpenError()
    {
        BuildPanel("Unable to continue"); completion = true;
        panel.transform.Find("Close")?.gameObject.SetActive(false);
        Label(panel.transform, "Please contact the operator.", Vector2.zero, new Vector2(800, 100), 28);
        ButtonAt(panel.transform, "Retry save", new Vector2(-160, -130), new Vector2(220, 48), () => { if (ReplayManager.instance?.RetrySave() == true && ReplayManager.IsRecordingActive()) { completion = false; Close(); } });
        ButtonAt(panel.transform, "Menu", new Vector2(160, -130), new Vector2(220, 48), ReturnToMenu);
    }
    public static void ShowCompletion()
    {
        Install(); instance.BuildPanel("Complete"); instance.completion = true;
        instance.panel.transform.Find("Close")?.gameObject.SetActive(false);
        Label(instance.panel.transform, "You have completed the escape room.", new Vector2(0, 75), new Vector2(850, 90), 30);
        ButtonAt(instance.panel.transform, "Menu", new Vector2(-230, -65), new Vector2(190, 48), instance.ReturnToMenu);
        ButtonAt(instance.panel.transform, "Retry save", new Vector2(0, -65), new Vector2(190, 48), () => ReplayManager.instance?.RetrySave());
        ButtonAt(instance.panel.transform, "Exit", new Vector2(230, -65), new Vector2(190, 48), GameExitUtility.ExitGame);
    }
    void ReturnToMenu()
    {
        var manager = ReplayManager.instance;
        manager?.Stop();
        if (manager != null && !manager.RetrySave()) return;
        completion = false; Close(); Time.timeScale = 1f;
        SceneTransitionService.LoadScene("MenuScene");
    }
    void BuildPanel(string title)
    {
        Close(); EnsureEventSystem(); page = title;
        previousSelection = EventSystem.current?.currentSelectedGameObject;
        cursorVisible = Cursor.visible; cursorLock = Cursor.lockState; Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        lockedInput = FindAnyObjectByType<InputManager>(); lockedInput?.AcquireControl(this);
        foreach (var selectable in Selectable.allSelectablesArray)
            if (selectable.interactable) { suspendedControls.Add(selectable); selectable.interactable = false; }
        panel = new GameObject(title, typeof(RectTransform), typeof(Image)); panel.transform.SetParent(transform, false);
        var rect = (RectTransform)panel.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.055f, 0.065f, 0.08f, 1f);
        Label(panel.transform, title, new Vector2(0, 285), new Vector2(800, 65), 40);
        var close = ButtonAt(panel.transform, "Close", new Vector2(470, 285), new Vector2(130, 42), Close);
        close.Select();
    }
    void Release()
    {
        if (lockedInput != null) lockedInput.ReleaseControl(this);
        lockedInput = null;
    }
    public void Close()
    {
        if (panel == null) return;
        panel.SetActive(false); Destroy(panel); panel = null; controlsText = null; page = "";
        foreach (var selectable in suspendedControls) if (selectable != null) selectable.interactable = true;
        suspendedControls.Clear();
        Release(); Cursor.visible = cursorVisible; Cursor.lockState = cursorLock;
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy) EventSystem.current.SetSelectedGameObject(previousSelection);
    }
    public static TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Vector2? anchor = null)
    {
        var obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
        var label = obj.GetComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center; label.color = new Color(0.94f, 0.95f, 0.96f); label.raycastTarget = false;
        return label;
    }
    public static Button ButtonAt(Transform parent, string text, Vector2 position, Vector2 size, Action action, Vector2? anchor = null)
    {
        var obj = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
        obj.GetComponent<Image>().color = new Color(0.23f, 0.28f, 0.34f);
        var button = obj.GetComponent<Button>(); var colors = button.colors; colors.selectedColor = new Color(0.95f, 0.76f, 0.38f); colors.highlightedColor = colors.selectedColor; button.colors = colors;
        button.onClick.AddListener(() => action()); Label(obj.transform, text, Vector2.zero, size - new Vector2(8, 4), 22);
        return button;
    }
}
