using UnityEngine;
using System.Collections.Generic;

public class HingeDoor : Interactable, IDataPersistence, IReplayObject
{
    [SerializeField] private Animator myDoor = null;

    [SerializeField] private string doorOpen = "HingeDoorOpen";
    [SerializeField] private string id;
    [SerializeField] private bool isOpen;

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
        if (isOpen)
        {
            return;
        }

        ResolveDoorAnimator();
        if (myDoor == null)
        {
            Debug.LogWarning("HingeDoor could not find an Animator containing the door-open animation.", this);
            return;
        }

        myDoor.Play(doorOpen, 0, 0.0f);
        isOpen = true;
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
