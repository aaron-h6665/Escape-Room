using UnityEngine;
using UnityEngine.InputSystem;

public class NoteInteractable : Interactable
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

    void Awake()
    {
        SetNoteVisible(false);
    }

    void Update()
    {
        if (!noteOpen || Time.frameCount == openedFrame)
        {
            return;
        }

        if (CloseInputPressed())
        {
            CloseNote();
        }
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
