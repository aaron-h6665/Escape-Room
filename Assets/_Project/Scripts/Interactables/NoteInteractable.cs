using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class NoteInteractable : Interactable, IDataPersistence, IReplayObject, IReplayHandoff
{
    [Header("Note UI")]
    [SerializeField] GameObject noteCanvas;
    [SerializeField] GameObject notePanel;
    [SerializeField] GameObject noteText;
    [SerializeField] Image clueImage;

    [Header("Player Lock")]
    [SerializeField] MonoBehaviour player;
    [SerializeField] InputManager inputManager;
    [SerializeField] PlayerInteract playerInteract;
    [SerializeField] InventoryUI inventoryUI;
    [SerializeField] PlayerUI playerUI;
    PlayerCrosshair playerCrosshair;

    bool noteOpen;
    [SerializeField] bool hasBeenRead;
    bool playerWasEnabled;
    bool playerInteractWasEnabled;
    bool playerControlWasLocked;
    bool inputSessionActive;
    bool wasPlaybackActive;
    int openedFrame = -1;
    Material generatedNoteMaterial;
    bool hudHidden;
    bool inventoryWasVisible;
    bool promptWasVisible;
    bool crosshairWasVisible;

    [Header("Replay Data")]
    [SerializeField] private string id;

    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

    string StateId => ReplayIdentity.Resolve(this, id);
    protected override string ReplayIdentityValue => StateId;
    protected override string ReplayCategoryValue => "Note";
    protected override string ReplayInteractionKind => "note_interacted";
    protected override string ReplayStateChangeKind => noteOpen ? "note_opened" : "note_closed";
    public override ReplayObjectState ReplayState => noteOpen ? ReplayObjectState.Open : ReplayObjectState.Closed;
    public bool HasBeenRead => hasBeenRead;
    public string ClueText
    {
        get
        {
            TMP_Text text = noteText != null ? noteText.GetComponentInChildren<TMP_Text>(true) : null;
            return text != null ? text.text : string.Empty;
        }
    }
    public Sprite ClueSprite
    {
        get
        {
            Image image = ResolveClueImage();
            return image != null ? image.sprite : null;
        }
    }

    void Awake()
    {
        EnsureHighlightMaterial();
        SetNoteVisible(false);
        wasPlaybackActive = ReplayManager.IsPlaybackActive();

        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    public void OnTakeover() { if (noteOpen) AcquireInputSession(null); }

    void Update()
    {
        bool playbackActive = ReplayManager.IsPlaybackActive();
        if (!wasPlaybackActive && playbackActive && inputSessionActive)
        {
            ReleaseInputSession();
        }
        if (wasPlaybackActive && !playbackActive && noteOpen)
        {
            bool takeoverActive = ReplayManager.instance != null && ReplayManager.instance.CurrentState == ReplayManager.State.Takeover;
            if (takeoverActive)
            {
                AcquireInputSession(null);
            }
            else
            {
                CloseNote(false);
            }
        }
        wasPlaybackActive = playbackActive;

        if (playbackActive)
        {
            return;
        }

        if (!noteOpen || Time.timeScale == 0f || (inputManager?.GameplayInputSuppressed ?? false) || Time.frameCount == openedFrame)
        {
            return;
        }

        if (CloseInputPressed())
        {
            CloseNote();
        }
    }

    public void SaveSnapshot(ref GameData data)
    {
        SaveNoteState(ref data, StateId, true);
    }

    public void LoadSnapshot(GameData data)
    {
        LoadNoteState(data, StateId, true);
    }

    public void SaveData(ref GameData data)
    {
        SaveNoteState(ref data, StateId, false);
    }

    public void LoadData(GameData data)
    {
        LoadNoteState(data, StateId, false);
    }

    public override string GetPromptMessage()
    {
        return noteOpen ? "Press E to Close Note." : "Press E to Read Note.";
    }

    public override bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        if (replayEvent == null)
        {
            return false;
        }

        switch (replayEvent.eventKind)
        {
            case "note_opened":
                hasBeenRead = true;
                noteOpen = true;
                openedFrame = Time.frameCount;
                SetNoteVisible(true);
                return true;
            case "note_closed":
                CloseNote(false);
                return true;
            default:
                return false;
        }
    }

    protected override void Interact(GameObject interactor)
    {
        if (noteOpen)
        {
            CloseNote();
        }
        else
        {
            OpenNote(interactor);
        }
    }

    void OpenNote(GameObject interactor)
    {
        noteOpen = true;
        hasBeenRead = true;
        openedFrame = Time.frameCount;
        SetNoteVisible(true);
        AcquireInputSession(interactor);
    }

    void AcquireInputSession(GameObject interactor)
    {
        if (inputSessionActive)
        {
            return;
        }
        ResolvePlayerReferences(interactor);
        if (inputManager != null)
        {
            playerControlWasLocked = inputManager.PlayerControlLocked;
            inputManager.AcquireControl(this);
        }

        if (player != null)
        {
            playerWasEnabled = player.enabled;
            player.enabled = false;
        }

        if (playerInteract != null)
        {
            playerInteractWasEnabled = playerInteract.enabled;
            playerInteract.enabled = false;
        }
        inputSessionActive = true;
    }

    void ReleaseInputSession()
    {
        if (!inputSessionActive)
        {
            return;
        }

        if (player != null)
        {
            player.enabled = playerWasEnabled;
        }
        if (inputManager != null)
        {
            inputManager.ReleaseControl(this);
        }
        if (playerInteract != null)
        {
            playerInteract.enabled = playerInteractWasEnabled;
        }
        inputSessionActive = false;
    }

    void CloseNote(bool recordEvent = true)
    {
        bool wasOpen = noteOpen;
        noteOpen = false;
        SetNoteVisible(false);
        ReleaseInputSession();

        if (wasOpen && recordEvent)
        {
            ReplayEventBus.Publish(this, "note_closed", ReplayObjectState.Closed, true, true);
        }
    }

    void SetNoteVisible(bool visible)
    {
        SetGameplayHudVisible(!visible);

        if (noteCanvas != null)
        {
            noteCanvas.SetActive(visible);
        }

        if (notePanel != null)
        {
            notePanel.SetActive(visible);
        }

        if (noteText != null)
        {
            noteText.SetActive(visible);
        }
    }

    void SetGameplayHudVisible(bool visible)
    {
        ResolveHudReferences();

        if (!visible && !hudHidden)
        {
            inventoryWasVisible = inventoryUI == null || inventoryUI.IsVisible;
            promptWasVisible = playerUI == null || playerUI.PromptVisible;
            crosshairWasVisible = playerCrosshair == null || playerCrosshair.IsVisible;
            if (inventoryUI != null) inventoryUI.SetVisible(false);
            if (playerUI != null) playerUI.SetPromptVisible(false);
            if (playerCrosshair != null) playerCrosshair.SetPresentationVisible(false);
            hudHidden = true;
            return;
        }

        if (visible && hudHidden)
        {
            if (inventoryUI != null) inventoryUI.SetVisible(inventoryWasVisible);
            if (playerUI != null) playerUI.SetPromptVisible(promptWasVisible);
            if (playerCrosshair != null) playerCrosshair.SetPresentationVisible(crosshairWasVisible);
            hudHidden = false;
        }
    }

    void ResolveHudReferences()
    {
        if (inventoryUI == null)
        {
#if UNITY_2023_1_OR_NEWER
            inventoryUI = FindAnyObjectByType<InventoryUI>();
#else
            inventoryUI = FindObjectOfType<InventoryUI>();
#endif
        }

        if (playerUI == null)
        {
#if UNITY_2023_1_OR_NEWER
            playerUI = FindAnyObjectByType<PlayerUI>();
#else
            playerUI = FindObjectOfType<PlayerUI>();
#endif
        }

        if (playerCrosshair == null)
        {
#if UNITY_2023_1_OR_NEWER
            playerCrosshair = FindAnyObjectByType<PlayerCrosshair>();
#else
            playerCrosshair = FindObjectOfType<PlayerCrosshair>();
#endif
        }
    }

    void LoadNoteState(GameData data, string stateId, bool restoreOpenState)
    {
        if (data == null || string.IsNullOrEmpty(stateId) || data.noteStates == null)
        {
            if (!restoreOpenState)
            {
                CloseNote(false);
            }
            return;
        }

        NoteSaveData noteData = data.noteStates.Find(noteState => noteState.id == stateId);
        if (noteData == null)
        {
            if (!restoreOpenState)
            {
                CloseNote(false);
            }
            return;
        }

        hasBeenRead = noteData.hasBeenRead;
        bool shouldOpen = restoreOpenState && noteData.isOpen;
        if (!shouldOpen && noteOpen)
        {
            CloseNote(false);
            return;
        }

        noteOpen = shouldOpen;
        openedFrame = shouldOpen ? Time.frameCount : -1;
        SetNoteVisible(shouldOpen);
    }

    void SaveNoteState(ref GameData data, string stateId, bool includeOpenState)
    {
        if (string.IsNullOrEmpty(stateId))
        {
            return;
        }

        if (data.noteStates == null)
        {
            data.noteStates = new List<NoteSaveData>();
        }

        NoteSaveData noteData = data.noteStates.Find(noteState => noteState.id == stateId);
        if (noteData == null)
        {
            noteData = new NoteSaveData();
            noteData.id = stateId;
            data.noteStates.Add(noteData);
        }

        noteData.isOpen = includeOpenState && noteOpen;
        noteData.hasBeenRead = hasBeenRead;
    }

    Image ResolveClueImage()
    {
        if (clueImage != null)
        {
            return clueImage;
        }

        if (noteCanvas == null)
        {
            return null;
        }

        foreach (Image candidate in noteCanvas.GetComponentsInChildren<Image>(true))
        {
            if (candidate.sprite != null && candidate.gameObject.name.ToLowerInvariant().Contains("note"))
            {
                clueImage = candidate;
                return clueImage;
            }
        }

        return null;
    }

    void ResolvePlayerReferences(GameObject interactor)
    {
        if (inputManager == null && interactor != null)
        {
            inputManager = interactor.GetComponent<InputManager>();
        }

        if (playerInteract == null && interactor != null)
        {
            playerInteract = interactor.GetComponent<PlayerInteract>();
        }

        if (playerUI == null && interactor != null)
        {
            playerUI = interactor.GetComponent<PlayerUI>();
        }

        if (player == null && interactor != null)
        {
            player = interactor.GetComponent<PlayerMotor>();
        }

        if (inputManager == null) inputManager = FindAnyObjectByType<InputManager>();
        if (playerInteract == null) playerInteract = FindAnyObjectByType<PlayerInteract>();
        if (playerUI == null) playerUI = FindAnyObjectByType<PlayerUI>();
        if (player == null) player = FindAnyObjectByType<PlayerMotor>();

        ResolveHudReferences();
    }

    bool CloseInputPressed()
    {
        if (inputManager != null && inputManager.OnFoot.Interact.triggered)
        {
            return true;
        }

        return Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
    }

    void EnsureHighlightMaterial()
    {
        Renderer noteRenderer = GetComponentInChildren<Renderer>(true);
        if (noteRenderer == null || noteRenderer.sharedMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            Debug.LogWarning("NoteInteractable could not find a shader for its focus highlight.", this);
            return;
        }

        generatedNoteMaterial = new Material(shader)
        {
            name = "Runtime Note Material"
        };
        noteRenderer.material = generatedNoteMaterial;
    }

    void OnDestroy()
    {
        if (generatedNoteMaterial != null)
        {
            Destroy(generatedNoteMaterial);
        }
    }

    protected override void OnDisable()
    {
        if (noteOpen || inputSessionActive || hudHidden)
        {
            CloseNote(false);
        }
        base.OnDisable();
    }
}
