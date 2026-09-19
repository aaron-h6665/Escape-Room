using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CaesarAnswerTerminal : Interactable
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

        if (playbackActive || !isOpen || Time.frameCount == openedFrame || Keyboard.current == null) return;
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
        inputManager?.SetPlayerControlLocked(true);
        if (playerInteract != null) playerInteract.enabled = false;
        inputSessionActive = true;
    }

    void ReleaseInputSession()
    {
        if (!inputSessionActive) return;
        inputManager?.SetPlayerControlLocked(playerControlWasLocked);
        if (playerInteract != null) playerInteract.enabled = playerInteractWasEnabled;
        inputSessionActive = false;
    }

    void HideGameplayHud()
    {
        if (hudHidden) return;
        inventoryWasVisible = inventoryUI == null || inventoryUI.IsVisible;
        promptWasVisible = playerUI == null || playerUI.PromptVisible;
        crosshairWasVisible = playerCrosshair == null || playerCrosshair.IsVisible;
        inventoryUI?.SetVisible(false);
        playerUI?.SetPromptVisible(false);
        playerCrosshair?.SetPresentationVisible(false);
        hudHidden = true;
    }

    void RestoreGameplayHud()
    {
        if (!hudHidden) return;
        inventoryUI?.SetVisible(inventoryWasVisible);
        playerUI?.SetPromptVisible(promptWasVisible);
        playerCrosshair?.SetPresentationVisible(crosshairWasVisible);
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
            if (entryText != null) entryText.text = "BLUE KEY";
            if (statusText != null) statusText.text = "PHRASE VERIFIED — KEY SUBMISSIONS UNLOCKED";
            StartCoroutine(CloseAfterSuccess());
            return;
        }

        entered = string.Empty;
        RefreshEntry();
        if (statusText != null) statusText.text = "Incorrect phrase. Recheck the shift and try again.";
    }

    IEnumerator CloseAfterSuccess()
    {
        yield return new WaitForSecondsRealtime(0.85f);
        Close(true);
    }

    void RefreshEntry()
    {
        if (entryText != null) entryText.text = string.IsNullOrEmpty(entered) ? "_" : entered + "_";
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
                    entered = "BLUE KEY";
                    if (entryText != null) entryText.text = entered;
                    if (statusText != null) statusText.text = "PHRASE VERIFIED — KEY SUBMISSIONS UNLOCKED";
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
