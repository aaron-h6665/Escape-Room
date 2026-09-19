using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class HingeDoor : Interactable, IDataPersistence, IReplayObject
{
    [SerializeField] private Animator myDoor = null;

    [SerializeField] private string doorOpen = "HingeDoorOpen";
    [SerializeField, Min(0.1f)] private float openAnimationDuration = 1.1f;
    [SerializeField] private string id;
    [SerializeField] private bool isOpen;
    Coroutine holdOpenRoutine;

    [Header("Puzzle Lock")]
    [Tooltip("When assigned, the door cannot be opened until this Simon Says puzzle is solved.")]
    [SerializeField] private SimonSaysController requiredPuzzle;
    [SerializeField] private string lockedPrompt = "Door locked - solve Simon Says";

    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

    string StateId => ReplayIdentity.Resolve(this, id);
    protected override string ReplayIdentityValue => StateId;
    protected override string ReplayCategoryValue => "Door";
    protected override string ReplayInteractionKind => "door_interacted";
    protected override string ReplayStateChangeKind => "door_opened";
    public override ReplayObjectState ReplayState => isOpen ? ReplayObjectState.Open : ReplayObjectState.Closed;
    public bool IsLocked => requiredPuzzle != null && !requiredPuzzle.IsSolved;
    public bool IsOpen => isOpen;

    void Awake()
    {
        ResolveDoorAnimator();

        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    protected override void Interact(GameObject interactor)
    {
        if (IsLocked) return;
        OpenDoor();
    }

    public void Open() => SetOpen(true, true);

    public void SetOpen(bool open, bool recordEvent)
    {
        if (!open) return;
        if (!OpenDoor()) return;
        if (recordEvent)
        {
            ReplayEventBus.Publish(this, "door_opened", ReplayObjectState.Open, true, true,
                position: transform.position, rotation: transform.rotation);
        }
    }

    bool OpenDoor()
    {
        if (isOpen) return false;

        ResolveDoorAnimator();
        if (myDoor == null)
        {
            Debug.LogWarning("HingeDoor could not find an Animator containing the door-open animation.", this);
            return false;
        }

        myDoor.Play(doorOpen, 0, 0.0f);
        myDoor.speed = 1f;
        isOpen = true;
        if (holdOpenRoutine != null) StopCoroutine(holdOpenRoutine);
        holdOpenRoutine = StartCoroutine(HoldOpenPose());
        return true;
    }

    IEnumerator HoldOpenPose()
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, openAnimationDuration));
        if (myDoor != null && isOpen)
        {
            myDoor.Play(doorOpen, 0, 1f);
            myDoor.Update(0f);
            myDoor.speed = 0f;
        }
        holdOpenRoutine = null;
    }

    public override string GetPromptMessage()
    {
        return IsLocked ? lockedPrompt : base.GetPromptMessage();
    }

    public override bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        if (replayEvent?.eventKind != "door_opened") return false;
        SetOpen(true, false);
        return true;
    }

    public void LoadData(GameData data)
    {
        LoadDoorState(data, StateId);
    }

    public void SaveData(ref GameData data)
    {
        SaveDoorState(ref data, StateId);
    }

    public void SaveSnapshot(ref GameData data)
    {
        SaveDoorState(ref data, StateId);
    }

    public void LoadSnapshot(GameData data)
    {
        LoadDoorState(data, StateId);
    }

    void LoadDoorState(GameData data, string stateId)
    {
        if (string.IsNullOrEmpty(stateId) || data.doorStates == null)
        {
            return;
        }

        DoorSaveData doorData = data.doorStates.Find(doorState => doorState.id == stateId);
        if (doorData == null)
        {
            return;
        }

        isOpen = doorData.isOpen;
        ApplyDoorVisualState();
    }

    void SaveDoorState(ref GameData data, string stateId)
    {
        if (string.IsNullOrEmpty(stateId))
        {
            return;
        }

        if (data.doorStates == null)
        {
            data.doorStates = new List<DoorSaveData>();
        }

        DoorSaveData doorData = data.doorStates.Find(doorState => doorState.id == stateId);
        if (doorData == null)
        {
            doorData = new DoorSaveData();
            doorData.id = stateId;
            data.doorStates.Add(doorData);
        }

        doorData.isOpen = isOpen;
    }

    void ApplyDoorVisualState()
    {
        ResolveDoorAnimator();
        if (!isOpen || myDoor == null)
        {
            return;
        }

        myDoor.Play(doorOpen, 0, 1f);
        myDoor.Update(0f);
        myDoor.speed = 0f;
    }

    void ResolveDoorAnimator()
    {
        if (myDoor != null)
        {
            return;
        }

        Animator fallback = null;
        foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
        {
            if (candidate == null)
            {
                continue;
            }

            fallback ??= candidate;
            RuntimeAnimatorController controller = candidate.runtimeAnimatorController;
            if (controller == null)
            {
                continue;
            }

            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip != null && clip.name == doorOpen)
                {
                    myDoor = candidate;
                    return;
                }
            }
        }

        myDoor = fallback;
    }
}
