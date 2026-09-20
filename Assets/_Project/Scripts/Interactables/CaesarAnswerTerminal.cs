using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;

public sealed class CaesarAnswerTerminal : Interactable, IReplayObject, IReplayTimeline
{
    const string EntryOpenedEvent = "caesar_answer_entry_opened";
    const string EntryClosedEvent = "caesar_answer_entry_closed";
    const string EntryChangedEvent = "caesar_answer_entry_changed";
    const string EntrySubmittedEvent = "caesar_answer_entry_submitted";

    [SerializeField] ColorKeyChoicePuzzle puzzle;
    [SerializeField] GameObject entryCanvas;
    [SerializeField] TMP_Text entryText;
    [SerializeField] TMP_Text statusText;

    InputManager inputManager;
    PlayerInteract playerInteract;
    PlayerUI playerUI;
    InventoryUI inventoryUI;
    PlayerCrosshair playerCrosshair;
    bool playerControlWasLocked;
    bool playerInteractWasEnabled;
    bool inventoryWasVisible;
    bool promptWasVisible;
    bool crosshairWasVisible;
    bool isOpen;
    bool inputSessionActive;
    bool hudHidden;
    bool wasPlaybackActive;
    int openedFrame = -1;
    string entered = string.Empty;
    float successRemaining;
    GameObject keyboardPanel;
    bool cursorWasVisible;
    CursorLockMode cursorWasLocked;
    string selectedKey = "A";
    void OnTakeover()
    {
        if (!isOpen) return;
        AcquireInputSession();
        SetKeyboardEnabled(true);
        var selected = keyboardPanel.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == selectedKey);
        selected?.Select();
    }
    void Start()
    {
        if (ReplayManager.instance != null) ReplayManager.instance.TakenOver += OnTakeover;
    }
    void OnDestroy()
    {
        if (ReplayManager.instance != null) ReplayManager.instance.TakenOver -= OnTakeover;
    }

    protected override string ReplayCategoryValue => "CaesarAnswerTerminal";
    protected override string ReplayStateChangeKind => isOpen ? EntryOpenedEvent : EntryClosedEvent;
    protected override bool RecordReplayInteraction => false;
    public override ReplayObjectState ReplayState => isOpen ? ReplayObjectState.Open : ReplayObjectState.Closed;
    public bool IsOpen => isOpen;
    public string EnteredText => entered;

    void Awake()
    {
        if (entryCanvas != null) entryCanvas.SetActive(false);
        wasPlaybackActive = ReplayManager.IsPlaybackActive();
    }

    void Update()
    {
        bool playbackActive = ReplayManager.IsPlaybackActive();
        if (!playbackActive && !(ReplayManager.instance?.IsHandoffFrame ?? false) && Time.timeScale > 0f && successRemaining > 0f)
        {
            successRemaining = Mathf.Max(0f, successRemaining - Time.deltaTime);
            if (successRemaining == 0f) Close(true);
        }
        if (isOpen && !playbackActive && EventSystem.current?.currentSelectedGameObject != null && keyboardPanel != null
            && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(keyboardPanel.transform)) selectedKey = EventSystem.current.currentSelectedGameObject.name;
        if (wasPlaybackActive && !playbackActive && isOpen)
        {
            bool takeoverActive = ReplayManager.instance != null
                && ReplayManager.instance.CurrentState == ReplayManager.State.Takeover;
            if (takeoverActive)
            {
                AcquireInputSession();
            }
            else
            {
                Close(false);
            }
        }
        wasPlaybackActive = playbackActive;

        if (playbackActive || !isOpen || Time.timeScale == 0f || successRemaining > 0f || Time.frameCount == openedFrame || (inputManager?.GameplayInputSuppressed ?? false)) return;
        if (Gamepad.current?.buttonEast.wasPressedThisFrame == true) { Close(true); return; }
        if (Keyboard.current == null) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            Close(true);
            return;
        }
        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            Submit();
            return;
        }
        if (keyboard.backspaceKey.wasPressedThisFrame && entered.Length > 0)
        {
            entered = entered.Substring(0, entered.Length - 1);
            RefreshEntry();
            RecordEntryChanged();
            return;
        }
        if (keyboard.spaceKey.wasPressedThisFrame && entered.Length > 0 && !entered.EndsWith(" ") && entered.Length < 24)
        {
            entered += " ";
            RefreshEntry();
            RecordEntryChanged();
        }

        foreach (UnityEngine.InputSystem.Controls.KeyControl key in keyboard.allKeys)
        {
            if (!key.wasPressedThisFrame) continue;
            int code = (int)key.keyCode;
            int first = (int)Key.A;
            int last = (int)Key.Z;
            if (code < first || code > last || entered.Length >= 24) continue;
            entered += (char)('A' + code - first);
            RefreshEntry();
            RecordEntryChanged();
            break;
        }
    }

    public override string GetPromptMessage()
    {
        if (puzzle != null && puzzle.AnswerVerified) return "Decoded phrase verified";
        return "Press E to enter the decoded phrase";
    }

    protected override void Interact(GameObject interactor)
    {
        if (puzzle != null && puzzle.AnswerVerified) return;
        if (isOpen)
        {
            Close(false);
            return;
        }

        Open(interactor, true);
    }

    void Open(GameObject interactor, bool acquireInput)
    {
        if (isOpen) return;
        ResolvePlayerReferences(interactor);
        HideGameplayHud();
        if (acquireInput)
        {
            AcquireInputSession();
        }
        entered = string.Empty;
        if (statusText != null) statusText.text = "Type the decoded phrase, then press Enter.";
        RefreshEntry();
        if (entryCanvas != null) entryCanvas.SetActive(true);
        BuildKeyboard();
        SetKeyboardEnabled(acquireInput);
        isOpen = true;
        openedFrame = Time.frameCount;
    }

    void ResolvePlayerReferences(GameObject interactor)
    {
        if (inputManager == null) inputManager = interactor != null ? interactor.GetComponent<InputManager>() : FindAnyObjectByType<InputManager>();
        if (playerInteract == null) playerInteract = interactor != null ? interactor.GetComponent<PlayerInteract>() : FindAnyObjectByType<PlayerInteract>();
        if (playerUI == null) playerUI = interactor != null ? interactor.GetComponent<PlayerUI>() : FindAnyObjectByType<PlayerUI>();
        if (inventoryUI == null) inventoryUI = FindAnyObjectByType<InventoryUI>();
        if (playerCrosshair == null) playerCrosshair = interactor != null ? interactor.GetComponent<PlayerCrosshair>() : FindAnyObjectByType<PlayerCrosshair>();
    }

    void AcquireInputSession()
    {
        if (inputSessionActive) return;
        ResolvePlayerReferences(null);
        playerControlWasLocked = inputManager != null && inputManager.PlayerControlLocked;
        playerInteractWasEnabled = playerInteract != null && playerInteract.enabled;
        if (inputManager != null) inputManager.AcquireControl(this);
        if (playerInteract != null) playerInteract.enabled = false;
        cursorWasVisible = Cursor.visible; cursorWasLocked = Cursor.lockState;
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        inputSessionActive = true;
    }

    void ReleaseInputSession()
    {
        if (!inputSessionActive) return;
        if (inputManager != null) inputManager.ReleaseControl(this);
        if (playerInteract != null) playerInteract.enabled = playerInteractWasEnabled;
        Cursor.visible = cursorWasVisible; Cursor.lockState = cursorWasLocked;
        inputSessionActive = false;
    }

    void HideGameplayHud()
    {
        if (hudHidden) return;
        inventoryWasVisible = inventoryUI == null || inventoryUI.IsVisible;
        promptWasVisible = playerUI == null || playerUI.PromptVisible;
        crosshairWasVisible = playerCrosshair == null || playerCrosshair.IsVisible;
        if (inventoryUI != null) inventoryUI.SetVisible(false);
        if (playerUI != null) playerUI.SetPromptVisible(false);
        if (playerCrosshair != null) playerCrosshair.SetPresentationVisible(false);
        hudHidden = true;
    }

    void RestoreGameplayHud()
    {
        if (!hudHidden) return;
        if (inventoryUI != null) inventoryUI.SetVisible(inventoryWasVisible);
        if (playerUI != null) playerUI.SetPromptVisible(promptWasVisible);
        if (playerCrosshair != null) playerCrosshair.SetPresentationVisible(crosshairWasVisible);
        hudHidden = false;
    }

    void Submit()
    {
        string submittedAnswer = entered;
        bool succeeded = puzzle != null && puzzle.VerifyDecodedAnswer(submittedAnswer);
        ReplayEventBus.Publish(this, EntrySubmittedEvent,
            succeeded ? ReplayObjectState.Activated : ReplayObjectState.Attempted,
            succeeded, false, textValue: submittedAnswer);
        if (succeeded)
        {
            if (entryText != null) entryText.text = ColorKeyChoicePuzzle.SolutionPhrase;
            if (statusText != null) statusText.text = "PHRASE VERIFIED — PASSAGE UNLOCKED";
            successRemaining = 0.85f;
            entered = ColorKeyChoicePuzzle.SolutionPhrase;
            return;
        }

        entered = string.Empty;
        RefreshEntry();
        if (statusText != null) statusText.text = "Incorrect phrase. Recheck the shift and try again.";
    }

    public void AdvanceReplayPresentation(float seconds) => successRemaining = Mathf.Max(0f, successRemaining - seconds);

    void BuildKeyboard()
    {
        if (keyboardPanel != null || entryCanvas == null) return;
        keyboardPanel = new GameObject("Letter keyboard", typeof(RectTransform), typeof(CanvasGroup));
        keyboardPanel.transform.SetParent(entryCanvas.transform, false);
        var rect = (RectTransform)keyboardPanel.transform; rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, -125); rect.sizeDelta = new Vector2(760, 240);
        string[] keys = "A B C D E F G H I J K L M N O P Q R S T U V W X Y Z Space Delete Submit Cancel".Split(' ');
        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            var button = StudyMenuPanel.ButtonAt(keyboardPanel.transform, key, new Vector2((i % 10 - 4.5f) * 74, 80 - i / 10 * 60), new Vector2(70, 52), () => PressKey(key));
            button.GetComponentInChildren<TMP_Text>().fontSize = key.Length > 1 ? 15 : 24;
        }
    }
    void SetKeyboardEnabled(bool enabled)
    {
        if (keyboardPanel == null) return;
        var group = keyboardPanel.GetComponent<CanvasGroup>(); group.interactable = true; group.blocksRaycasts = enabled;
        foreach (var button in keyboardPanel.GetComponentsInChildren<Button>())
            button.navigation = new Navigation { mode = enabled ? Navigation.Mode.Automatic : Navigation.Mode.None };
        if (enabled && StudyOptions.UsingGamepad) keyboardPanel.GetComponentInChildren<Button>()?.Select();
    }
    public void PressKey(string key)
    {
        if (!isOpen || ReplayManager.IsPlaybackActive() || Time.timeScale == 0 || successRemaining > 0f || (inputManager?.GameplayInputSuppressed ?? false)) return;
        if (key == "Cancel") { Close(true); return; }
        if (key == "Submit") { Submit(); return; }
        if (key == "Delete") { if (entered.Length > 0) entered = entered.Substring(0, entered.Length - 1); }
        else if (entered.Length < 24) entered += key == "Space" ? " " : key;
        RefreshEntry(); RecordEntryChanged();
    }
    public void SaveSnapshot(ref GameData data)
    {
        data.uiStates.Add(new UiSnapshot { id = ReplayTargetId, isOpen = isOpen, text = entered,
            feedback = statusText != null ? statusText.text : "", selected = selectedKey });
        // Dedicated field avoids ambiguous free-text timing encodings.
        data.caesarSuccessRemaining = successRemaining;
    }
    public void LoadSnapshot(GameData data)
    {
        var saved = data.uiStates?.Find(v => v.id == ReplayTargetId);
        if (saved == null) return;
        if (saved.isOpen && !isOpen) Open(null, false);
        else if (!saved.isOpen && isOpen) Close(false);
        entered = saved.text ?? ""; selectedKey = saved.selected ?? "A";
        successRemaining = data.caesarSuccessRemaining;
        RefreshEntry();
        if (statusText != null) statusText.text = saved.feedback;
        SetKeyboardEnabled(false);
        if (isOpen && keyboardPanel != null) keyboardPanel.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == selectedKey)?.Select();
    }

    void RefreshEntry()
    {
        if (entryText != null) entryText.text = successRemaining > 0f ? entered : string.IsNullOrEmpty(entered) ? "_" : entered + "_";
    }

    void RecordEntryChanged()
    {
        ReplayEventBus.Publish(this, EntryChangedEvent, ReplayObjectState.Activated,
            true, false, textValue: entered);
    }

    void Close(bool recordEvent)
    {
        if (!isOpen) return;
        isOpen = false;
        if (entryCanvas != null) entryCanvas.SetActive(false);
        ReleaseInputSession();
        RestoreGameplayHud();
        if (recordEvent)
        {
            ReplayEventBus.Publish(this, EntryClosedEvent, ReplayObjectState.Closed, true, true);
        }
    }

    public override bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        if (replayEvent == null) return false;
        switch (replayEvent.eventKind)
        {
            case EntryOpenedEvent:
                Open(null, false);
                return true;
            case EntryClosedEvent:
                Close(false);
                return true;
            case EntryChangedEvent:
                entered = replayEvent.textValue ?? string.Empty;
                RefreshEntry();
                return true;
            case EntrySubmittedEvent:
                if (replayEvent.succeeded)
                {
                    entered = ColorKeyChoicePuzzle.SolutionPhrase;
                    if (entryText != null) entryText.text = entered;
                    if (statusText != null) statusText.text = "PHRASE VERIFIED — PASSAGE UNLOCKED";
                }
                else
                {
                    entered = string.Empty;
                    RefreshEntry();
                    if (statusText != null) statusText.text = "Incorrect phrase. Recheck the shift and try again.";
                }
                return true;
            case "caesaranswerterminal_changed":
                if (replayEvent.state == ReplayObjectState.Open) Open(null, false);
                else if (replayEvent.state == ReplayObjectState.Closed) Close(false);
                return true;
            default:
                return false;
        }
    }

    protected override void OnDisable()
    {
        if (isOpen || inputSessionActive || hudHidden)
        {
            Close(false);
        }
        base.OnDisable();
    }
}
