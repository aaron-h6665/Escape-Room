using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LockedDoorInteractable : Interactable, IDataPersistence, IReplayObject
{
    [Header("Inventory Requirement")]
    [SerializeField] Inventory inventory;
    [SerializeField] Item requiredItem;
    [SerializeField] string requiredItemId = "hinge_door_key";

    [Header("Animation Names")]
    [SerializeField] Animator doorAnimator;
    [SerializeField] string openAnimationName = "HingeDoorOpen";
    [SerializeField] string closeAnimationName = "HingeDoorClose";

    [Header("UI")]
    [SerializeField] int timeToShowUI = 1;
    [SerializeField] GameObject showDoorLockedUI;
    [SerializeField] bool showLockedUIOnInteract;

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

    Coroutine doorLockedCoroutine;

    bool IsLocked => inventory == null || !inventory.HasItem(RequiredItemId);
    string RequiredItemId => requiredItem != null ? requiredItem.Id : requiredItemId;
    string ReplayId => ReplayIdentity.Resolve(this, id);

    void Awake()
    {
        if (doorAnimator == null)
        {
            doorAnimator = GetComponent<Animator>();
        }

        ResolveInventory();

        if (showDoorLockedUI != null)
        {
            showDoorLockedUI.SetActive(false);
        }

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
            if (showLockedUIOnInteract)
            {
                ShowDoorLockedMessage();
            }

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

    void ShowDoorLockedMessage()
    {
        if (showDoorLockedUI == null)
        {
            return;
        }

        if (doorLockedCoroutine != null)
        {
            StopCoroutine(doorLockedCoroutine);
        }

        doorLockedCoroutine = StartCoroutine(ShowDoorLocked());
    }

    IEnumerator ShowDoorLocked()
    {
        showDoorLockedUI.SetActive(true);
        yield return new WaitForSeconds(timeToShowUI);
        showDoorLockedUI.SetActive(false);
        doorLockedCoroutine = null;
    }

    void ResolveInventory(GameObject interactor = null)
    {
        if (inventory != null)
        {
            return;
        }

        if (interactor != null)
        {
            inventory = interactor.GetComponentInParent<Inventory>();
            if (inventory != null)
            {
                return;
            }
        }

#if UNITY_2023_1_OR_NEWER
        inventory = FindFirstObjectByType<Inventory>();
#else
        inventory = FindObjectOfType<Inventory>();
#endif
    }
}
