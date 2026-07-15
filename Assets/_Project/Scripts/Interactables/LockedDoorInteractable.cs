using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LockedDoorInteractable : InventoryLockedInteractable, IDataPersistence, IReplayObject
{
    [Header("Animation Names")]
    [SerializeField] Animator doorAnimator;
    [SerializeField] string openAnimationName = "HingeDoorOpen";
    [SerializeField] string closeAnimationName = "HingeDoorClose";

    [Header("State")]
    [SerializeField] int waitTimer = 1;
    [SerializeField] bool pauseInteraction;
    [SerializeField] bool doorOpen;
    [Header("Other")]
    [SerializeField] private string id;
    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

    string ReplayId => ReplayIdentity.Resolve(this, id);
    protected override string ReplayIdentityValue => ReplayId;
    protected override string ReplayCategoryValue => "Door";
    protected override string ReplayInteractionKind => "door_interacted";
    protected override string ReplayStateChangeKind => doorOpen ? "door_opened" : "door_closed";
    public override ReplayObjectState ReplayState => doorOpen ? ReplayObjectState.Open : ReplayObjectState.Closed;

    void Awake()
    {
        if (doorAnimator == null)
        {
            doorAnimator = GetComponent<Animator>();
        }

        ResolveInventory();

        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    public void LoadData(GameData data)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("LockedDoorInteractable is missing an id. Door state will not be loaded.", this);
            return;
        }

        LoadDoorState(data, id);
    }

    public void SaveData(ref GameData data)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("LockedDoorInteractable is missing an id. Door state will not be saved.", this);
            return;
        }

        SaveDoorState(ref data, id);
    }

    public void SaveSnapshot(ref GameData data)
    {
        SaveDoorState(ref data, ReplayId);
    }

    public void LoadSnapshot(GameData data)
    {
        LoadDoorState(data, ReplayId);
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

        doorOpen = doorData.isOpen;
        pauseInteraction = false;
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

        doorData.isOpen = doorOpen;
    }

    public override string GetPromptMessage()
    {
        if (IsLocked)
        {
            return "Door Locked";
        }

        return doorOpen ? "Press E to close door" : "Press E to open door";
    }

    protected override void Interact(GameObject interactor)
    {
        if (inventory == null)
        {
            ResolveInventory(interactor);
        }

        if (IsLocked)
        {
            return;
        }

        if (pauseInteraction)
        {
            return;
        }

        if (doorAnimator == null)
        {
            Debug.LogWarning("LockedDoorInteractable is missing an Animator.", this);
            return;
        }

        doorAnimator.speed = 1f;
        doorAnimator.Play(doorOpen ? closeAnimationName : openAnimationName, 0, 0.0f);
        doorOpen = !doorOpen;
        StartCoroutine(PauseDoorInteraction());
    }

    void ApplyDoorVisualState()
    {
        if (doorAnimator == null)
        {
            return;
        }

        doorAnimator.Play(doorOpen ? openAnimationName : closeAnimationName, 0, 1f);
        doorAnimator.Update(0f);
    }

    IEnumerator PauseDoorInteraction()
    {
        pauseInteraction = true;
        yield return new WaitForSeconds(waitTimer);
        pauseInteraction = false;
    }

}
