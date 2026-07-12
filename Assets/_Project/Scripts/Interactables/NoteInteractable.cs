using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class NoteInteractable : Interactable, IReplayObject
{
    [Header("Note UI")]
    [SerializeField] GameObject noteCanvas;
    [SerializeField] GameObject notePanel;
    [SerializeField] GameObject noteText;

    [Header("Player Lock")]
    [SerializeField] MonoBehaviour player;
    [SerializeField] InputManager inputManager;
    [SerializeField] PlayerInteract playerInteract;

    bool noteOpen;
    bool playerWasEnabled;
    bool playerInteractWasEnabled;
    bool playerControlWasLocked;
    int openedFrame = -1;

    [Header("Replay Data")]
    [SerializeField] private string id;

    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

    string StateId => ReplayIdentity.Resolve(this, id);

    void Awake()
    {
        SetNoteVisible(false);

        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    void Update()
    {
        if (ReplayManager.IsPlaybackActive())
        {
            return;
        }

        if (!noteOpen || Time.frameCount == openedFrame)
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
        SaveNoteState(ref data, StateId);
    }

    public void LoadSnapshot(GameData data)
    {
        LoadNoteState(data, StateId);
    }

    public override string GetPromptMessage()
    {
        return noteOpen ? "Press E to Close Note." : "Press E to Read Note.";
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
        ResolvePlayerReferences(interactor);

        if (player == null)
        {
            player = interactor.GetComponent<PlayerMotor>();
        }

        noteOpen = true;
        openedFrame = Time.frameCount;
        SetNoteVisible(true);

        if (inputManager != null)
        {
            playerControlWasLocked = inputManager.PlayerControlLocked;
            inputManager.SetPlayerControlLocked(true);
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
    }

    void CloseNote()
    {
        noteOpen = false;
        SetNoteVisible(false);

        if (player != null)
        {
            player.enabled = playerWasEnabled;
        }

        if (inputManager != null)
        {
            inputManager.SetPlayerControlLocked(playerControlWasLocked);
        }

        if (playerInteract != null)
        {
            playerInteract.enabled = playerInteractWasEnabled;
        }
    }

    void SetNoteVisible(bool visible)
    {
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

    void LoadNoteState(GameData data, string stateId)
    {
        if (string.IsNullOrEmpty(stateId) || data.noteStates == null)
        {
            return;
        }

        NoteSaveData noteData = data.noteStates.Find(noteState => noteState.id == stateId);
        if (noteData == null)
        {
            return;
        }

        noteOpen = noteData.isOpen;
        openedFrame = noteOpen ? Time.frameCount : -1;
        SetNoteVisible(noteOpen);
    }

    void SaveNoteState(ref GameData data, string stateId)
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

        noteData.isOpen = noteOpen;
    }

    void ResolvePlayerReferences(GameObject interactor)
    {
        if (inputManager == null)
        {
            inputManager = interactor.GetComponent<InputManager>();
        }

        if (playerInteract == null)
        {
            playerInteract = interactor.GetComponent<PlayerInteract>();
        }
    }

    bool CloseInputPressed()
    {
        if (inputManager != null && inputManager.OnFoot.Interact.triggered)
        {
            return true;
        }

        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }
}
