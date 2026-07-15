using System.Collections.Generic;
using UnityEngine;

public class ItemPickupInteractable : Interactable, IDataPersistence, IReplayObject
{
    [SerializeField]
    Item item;
    [SerializeField]
    Inventory inventory;
    [SerializeField]
    bool destroyOnPickup = true;

    [Header("Save Data")]
    [SerializeField] private string id;
    [SerializeField] bool isPickedUp;

    public Item Item => item;
    public string SaveId => id;
    string ReplayId => ReplayIdentity.Resolve(this, id);
    protected override string ReplayIdentityValue => ReplayId;
    protected override string ReplayCategoryValue => "Item";
    protected override string ReplayItemIdValue => item != null ? item.Id : string.Empty;
    protected override string ReplayInteractionKind => "item_pickup_attempted";
    protected override string ReplayStateChangeKind => "item_picked_up";
    public override string ReplayTargetName => item != null ? item.name : gameObject.name;
    public override ReplayObjectState ReplayState => isPickedUp ? ReplayObjectState.PickedUp : ReplayObjectState.Idle;

    void Awake()
    {
        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

    bool HasSaveId => !string.IsNullOrEmpty(id);

    public void Initialize(Item item)
    {
        this.item = item;
        id = string.Empty;
        isPickedUp = false;
        gameObject.SetActive(true);
    }

    public void LoadData(GameData data)
    {
        if (!HasSaveId)
        {
            return;
        }

        LoadItemState(data, id);
    }

    public void SaveData(ref GameData data)
    {
        if (!HasSaveId)
        {
            return;
        }

        SaveItemState(ref data, id);
    }

    public void SaveSnapshot(ref GameData data)
    {
        SaveItemState(ref data, ReplayId);
    }

    public void LoadSnapshot(GameData data)
    {
        LoadItemState(data, ReplayId);
    }

    void LoadItemState(GameData data, string stateId)
    {
        if (string.IsNullOrEmpty(stateId) || data.itemStates == null)
        {
            return;
        }

        ItemSaveData itemSaveData = data.itemStates.Find(itemState => itemState.id == stateId);
        if (itemSaveData == null)
        {
            return;
        }

        isPickedUp = itemSaveData.isPickedUp;
        if (!isPickedUp && itemSaveData.hasWorldTransform)
        {
            transform.SetPositionAndRotation(itemSaveData.position, itemSaveData.rotation);
        }

        ApplyItemVisualState();
    }

    void SaveItemState(ref GameData data, string stateId)
    {
        if (string.IsNullOrEmpty(stateId))
        {
            return;
        }

        if (data.itemStates == null)
        {
            data.itemStates = new List<ItemSaveData>();
        }

        ItemSaveData itemSaveData = data.itemStates.Find(itemState => itemState.id == stateId);
        if (itemSaveData == null)
        {
            itemSaveData = new ItemSaveData();
            itemSaveData.id = stateId;
            data.itemStates.Add(itemSaveData);
        }

        itemSaveData.isPickedUp = isPickedUp;
        itemSaveData.hasWorldTransform = !isPickedUp;
        itemSaveData.position = transform.position;
        itemSaveData.rotation = transform.rotation;
    }

    public override string GetPromptMessage()
    {
        if (!string.IsNullOrWhiteSpace(promptMessage))
        {
            return promptMessage;
        }

        string itemName = item != null ? item.name.ToLowerInvariant() : "item";
        return $"Press E to pick up {itemName}";
    }

    protected override void Interact(GameObject interactor)
    {
        if (isPickedUp)
        {
            return;
        }

        Inventory targetInventory = ResolveInventory(interactor);
        if (targetInventory == null)
        {
            Debug.LogWarning("Item pickup could not find an Inventory on the interacting object.", this);
            return;
        }

        if (!targetInventory.AddItem(item, SaveId))
        {
            return;
        }

        isPickedUp = true;

        if (HasSaveId || !destroyOnPickup)
        {
            ApplyItemVisualState();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void ApplyItemVisualState()
    {
        gameObject.SetActive(!isPickedUp);
    }

    public void RestoreToWorld(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        isPickedUp = false;
        ApplyItemVisualState();
    }

    Inventory ResolveInventory(GameObject interactor)
    {
        if (inventory != null)
        {
            return inventory;
        }

        if (interactor != null)
        {
            inventory = interactor.GetComponentInParent<Inventory>();
            if (inventory != null)
            {
                return inventory;
            }
        }

#if UNITY_2023_1_OR_NEWER
        inventory = FindFirstObjectByType<Inventory>();
#else
        inventory = FindObjectOfType<Inventory>();
#endif

        return inventory;
    }
}
