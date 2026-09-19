using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class Inventory : MonoBehaviour, IDataPersistence, IReplayObject, IReplayEventTarget
{
    public const int SlotCount = 5;

    [Header("References")]
    [SerializeField]
    InventoryUI ui;
    [SerializeField]
    AudioSource audioSource;
    [SerializeField]
    Transform dropOrigin;

    [Header("Prefabs")]
    [SerializeField]
    GameObject droppedItemPrefab;
    [SerializeField]
    float dropDistance = 1.5f;

    [Header("Catalog")]
    [SerializeField]
    Item[] itemCatalog;

    [Header("Audio Clips")]
    [SerializeField]
    AudioClip pickUpItemAudio;
    [SerializeField]
    AudioClip dropItemAudio;

    [Header("State")]
    [SerializeField]
    Item[] slots = new Item[SlotCount];
    [SerializeField]
    string[] sourcePickupIds = new string[SlotCount];
    [SerializeField]
    int selectedSlotIndex = -1;

    public bool HasSelectedItem
    {
        get
        {
            EnsureSlotArrays();
            return IsValidSlot(selectedSlotIndex) && slots[selectedSlotIndex] != null;
        }
    }

    public Item SelectedItem => HasSelectedItem ? slots[selectedSlotIndex] : null;
    public bool IsFull => OccupiedSlotCount >= SlotCount;
    public string ReplayTargetId => ReplayIdentity.Resolve(this, string.Empty);
    public string ReplayTargetName => "Player Inventory";
    public string ReplayTargetCategory => "Inventory";
    public ReplayObjectState ReplayState => ReplayObjectState.Idle;
    public int SelectedSlotIndex
    {
        get
        {
            EnsureSlotArrays();
            return IsValidSlot(selectedSlotIndex) ? selectedSlotIndex : -1;
        }
    }

    public int OccupiedSlotCount
    {
        get
        {
            EnsureSlotArrays();
            int count = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    void Awake()
    {
        EnsureSlotArrays();

        if (ui == null)
        {
#if UNITY_2023_1_OR_NEWER
            ui = FindFirstObjectByType<InventoryUI>();
#else
            ui = FindObjectOfType<InventoryUI>();
#endif
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (dropOrigin == null)
        {
            Camera camera = GetComponentInChildren<Camera>();
            dropOrigin = camera != null ? camera.transform : transform;
        }

        ui?.Initialize(this);

        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    public bool AddItem(Item item)
    {
        return AddItem(item, string.Empty);
    }

    public bool AddItem(Item item, string sourcePickupId)
    {
        EnsureSlotArrays();

        if (item == null)
        {
            Debug.LogWarning("Tried to add a null item to the inventory.", this);
            return false;
        }

        int slotIndex = FindFirstEmptySlot();
        if (slotIndex < 0)
        {
            ui?.ShowInventoryFullMessage();
            return false;
        }

        AddItemToSlot(slotIndex, item, sourcePickupId, true, true);
        return true;
    }

    public void LoadData(GameData data)
    {
        EnsureSlotArrays();
        ClearInventory();

        if (data == null)
        {
            return;
        }

        Dictionary<string, Item> itemsById = BuildItemLookup();

        if (data.inventory == null || data.inventory.items == null)
        {
            RestoreLegacyPickedUpItems(data);
            return;
        }

        if (!data.hasMotorState && data.inventory.items.Count == 0 && RestoreLegacyPickedUpItems(data))
        {
            return;
        }

        List<InventoryItemSaveData> savedItems = new List<InventoryItemSaveData>(data.inventory.items);
        savedItems.Sort((a, b) =>
        {
            if (a == null)
            {
                return b == null ? 0 : 1;
            }

            if (b == null)
            {
                return -1;
            }

            return a.slotIndex.CompareTo(b.slotIndex);
        });
        HashSet<int> usedSlots = new HashSet<int>();
        HashSet<string> usedSourcePickupIds = new HashSet<string>();

        foreach (InventoryItemSaveData savedItem in savedItems)
        {
            if (savedItem == null || string.IsNullOrWhiteSpace(savedItem.itemId))
            {
                continue;
            }

            if (!IsValidSlot(savedItem.slotIndex) || !usedSlots.Add(savedItem.slotIndex))
            {
                continue;
            }

            if (!itemsById.TryGetValue(savedItem.itemId, out Item item))
            {
                Debug.LogWarning($"Could not restore inventory item '{savedItem.itemId}' because no matching Item asset is loaded.", this);
                continue;
            }

            string sourcePickupId = ResolveSavedSourcePickupId(savedItem.sourcePickupId, item, data, usedSourcePickupIds);
            if (!string.IsNullOrEmpty(sourcePickupId))
            {
                usedSourcePickupIds.Add(sourcePickupId);
            }

            AddItemToSlot(savedItem.slotIndex, item, sourcePickupId, false, false);
        }

        if (!SelectSlot(data.inventory.selectedIndex))
        {
            SelectFirstAvailableSlot();
        }
    }

    public void SaveData(ref GameData data)
    {
        EnsureSlotArrays();

        if (data == null)
        {
            data = new GameData();
        }

        if (data.inventory == null)
        {
            data.inventory = new InventorySaveData();
        }

        if (data.inventory.items == null)
        {
            data.inventory.items = new List<InventoryItemSaveData>();
        }

        data.inventory.items.Clear();
        data.inventory.selectedIndex = SelectedSlotIndex;

        for (int i = 0; i < SlotCount; i++)
        {
            Item item = slots[i];
            if (item == null)
            {
                continue;
            }

            data.inventory.items.Add(new InventoryItemSaveData
            {
                slotIndex = i,
                itemId = item.Id,
                sourcePickupId = sourcePickupIds[i] ?? string.Empty
            });
        }
    }

    public void SaveSnapshot(ref GameData data)
    {
        SaveData(ref data);
    }

    public void LoadSnapshot(GameData data)
    {
        LoadData(data);
    }

    public bool TryGetItemAt(int slotIndex, out Item item)
    {
        EnsureSlotArrays();
        item = IsValidSlot(slotIndex) ? slots[slotIndex] : null;
        return item != null;
    }

    public bool SelectSlot(int slotIndex)
    {
        EnsureSlotArrays();

        if (!IsValidSlot(slotIndex))
        {
            return false;
        }

        SetSelectedSlot(slotIndex);
        return true;
    }

    public bool SelectNextOccupiedSlot()
    {
        return SelectOccupiedSlotFrom(HasSelectedItem ? selectedSlotIndex + 1 : 0, 1);
    }

    public bool SelectPreviousOccupiedSlot()
    {
        return SelectOccupiedSlotFrom(HasSelectedItem ? selectedSlotIndex - 1 : SlotCount - 1, -1);
    }

    public bool SelectNextSlot()
    {
        return SelectRelativeSlot(1);
    }

    public bool SelectPreviousSlot()
    {
        return SelectRelativeSlot(-1);
    }

    public bool HasItem(Item item)
    {
        return item != null && HasItem(item.Id);
    }

    public bool HasItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        EnsureSlotArrays();
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] != null && slots[i].Id == itemId)
            {
                return true;
            }
        }

        return false;
    }

    public bool SelectedItemMatches(Item item)
    {
        return item != null && SelectedItemMatches(item.Id);
    }

    public bool SelectedItemMatches(string itemId)
    {
        return SelectedItem != null && !string.IsNullOrWhiteSpace(itemId) && SelectedItem.Id == itemId;
    }

    public bool DropSelectedItem()
    {
        return HasSelectedItem && DropItemAt(selectedSlotIndex);
    }

    public bool DropItemAt(int slotIndex)
    {
        EnsureSlotArrays();

        if (!IsValidSlot(slotIndex) || slotIndex != selectedSlotIndex || slots[slotIndex] == null)
        {
            return false;
        }

        Item item = slots[slotIndex];
        string sourcePickupId = sourcePickupIds[slotIndex];
        Vector3 dropPosition = (dropOrigin != null ? dropOrigin : transform).position
            + (dropOrigin != null ? dropOrigin : transform).forward * dropDistance;
        // Keep required items on the accessible side of walls and above the floor.
        Vector3 origin = (dropOrigin != null ? dropOrigin : transform).position;
        Vector3 direction = (dropPosition - origin).normalized;
        if (Physics.SphereCast(origin, 0.12f, direction, out RaycastHit wall, dropDistance, 1 << 7, QueryTriggerInteraction.Ignore))
            dropPosition = origin + direction * Mathf.Max(0.2f, wall.distance - 0.2f);
        if (Physics.Raycast(dropPosition, Vector3.down, out RaycastHit floor, 10f, 1 << 7, QueryTriggerInteraction.Ignore))
            dropPosition = floor.point + Vector3.up * 0.15f;
        Quaternion dropRotation = Quaternion.identity;

        if (!TryRestorePersistentPickup(sourcePickupId, dropPosition, dropRotation))
        {
            GameObject pickupPrefab = ResolvePickupPrefab(item);
            if (pickupPrefab == null)
            {
                Debug.LogWarning($"Cannot drop {item.name} because no pickup prefab contains an ItemPickupInteractable.", this);
                return false;
            }

            GameObject droppedObject = Instantiate(pickupPrefab, dropPosition, dropRotation);
            ItemPickupInteractable pickup = droppedObject.GetComponentInChildren<ItemPickupInteractable>();
            if (pickup != null)
            {
                pickup.Initialize(item);
            }
        }

        slots[slotIndex] = null;
        sourcePickupIds[slotIndex] = string.Empty;

        int nextSelectedSlot = FindOccupiedSlotFrom(slotIndex + 1, 1);
        if (nextSelectedSlot < 0)
        {
            nextSelectedSlot = FindOccupiedSlotFrom(slotIndex - 1, -1);
        }

        SetSelectedSlot(nextSelectedSlot);
        PlayOneShot(dropItemAudio);
        ReplayEventBus.Publish(this, "item_dropped", ReplayObjectState.Dropped, true, true, item.Id, dropPosition, dropRotation, sourcePickupId);
        return true;
    }

    public bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        return false;
    }

    void AddItemToSlot(int slotIndex, Item item, string sourcePickupId, bool playAudio, bool autoSelect)
    {
        if (!IsValidSlot(slotIndex) || item == null)
        {
            return;
        }

        slots[slotIndex] = item;
        sourcePickupIds[slotIndex] = sourcePickupId ?? string.Empty;

        if (autoSelect && SelectedSlotIndex < 0)
        {
            selectedSlotIndex = slotIndex;
        }

        ui?.RefreshSlots();

        if (playAudio)
        {
            PlayOneShot(pickUpItemAudio);
        }
    }

    void ClearInventory()
    {
        EnsureSlotArrays();
        Array.Clear(slots, 0, slots.Length);
        Array.Clear(sourcePickupIds, 0, sourcePickupIds.Length);
        selectedSlotIndex = -1;
        ui?.RefreshSlots();
    }

    void SetSelectedSlot(int slotIndex)
    {
        int previous = selectedSlotIndex;
        selectedSlotIndex = IsValidSlot(slotIndex) ? slotIndex : -1;
        ui?.RefreshSlots();
        if (previous != selectedSlotIndex && !(ReplayManager.instance?.IsRestoring ?? false))
            ReplayEventBus.Publish(this, "inventory_slot_selected", ReplayObjectState.Activated, true, false, numberValue: selectedSlotIndex);
    }

    bool SelectRelativeSlot(int step)
    {
        EnsureSlotArrays();

        int startIndex;
        if (SelectedSlotIndex >= 0)
        {
            startIndex = selectedSlotIndex + step;
        }
        else
        {
            startIndex = step > 0 ? 0 : SlotCount - 1;
        }

        SetSelectedSlot(WrapSlotIndex(startIndex));
        return true;
    }

    bool SelectOccupiedSlotFrom(int startIndex, int step)
    {
        int slotIndex = FindOccupiedSlotFrom(startIndex, step);
        if (slotIndex < 0)
        {
            return false;
        }

        SetSelectedSlot(slotIndex);
        return true;
    }

    int FindOccupiedSlotFrom(int startIndex, int step)
    {
        EnsureSlotArrays();

        for (int offset = 0; offset < SlotCount; offset++)
        {
            int slotIndex = WrapSlotIndex(startIndex + offset * step);
            if (slots[slotIndex] != null)
            {
                return slotIndex;
            }
        }

        return -1;
    }

    void SelectFirstAvailableSlot()
    {
        int slotIndex = FindFirstOccupiedSlot();
        if (slotIndex >= 0 && slots[slotIndex] != null)
        {
            SetSelectedSlot(slotIndex);
            return;
        }

        SetSelectedSlot(-1);
    }

    int FindFirstOccupiedSlot()
    {
        EnsureSlotArrays();
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] != null)
            {
                return i;
            }
        }

        return -1;
    }

    int FindFirstEmptySlot()
    {
        EnsureSlotArrays();
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    int WrapSlotIndex(int index)
    {
        int wrapped = index % SlotCount;
        return wrapped < 0 ? wrapped + SlotCount : wrapped;
    }

    bool IsValidSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < SlotCount;
    }

    void EnsureSlotArrays()
    {
        if (slots == null)
        {
            slots = new Item[SlotCount];
        }
        else if (slots.Length != SlotCount)
        {
            Array.Resize(ref slots, SlotCount);
        }

        if (sourcePickupIds == null)
        {
            sourcePickupIds = new string[SlotCount];
        }
        else if (sourcePickupIds.Length != SlotCount)
        {
            Array.Resize(ref sourcePickupIds, SlotCount);
        }

        if (!IsValidSlot(selectedSlotIndex))
        {
            selectedSlotIndex = -1;
        }
    }

    GameObject ResolvePickupPrefab(Item item)
    {
        if (item.prefab != null && HasPickupInteractable(item.prefab))
        {
            return item.prefab;
        }

        if (item.prefab != null)
        {
            Debug.LogWarning($"{item.name}'s prefab does not contain an ItemPickupInteractable. Falling back to the default dropped item prefab.", this);
        }

        if (droppedItemPrefab != null && HasPickupInteractable(droppedItemPrefab))
        {
            return droppedItemPrefab;
        }

        return null;
    }

    bool HasPickupInteractable(GameObject prefab)
    {
        return prefab != null && prefab.GetComponentInChildren<ItemPickupInteractable>(true) != null;
    }

    bool TryRestorePersistentPickup(string sourcePickupId, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrEmpty(sourcePickupId))
        {
            return false;
        }

        ItemPickupInteractable pickup = FindPickupBySaveId(sourcePickupId);
        if (pickup == null)
        {
            return false;
        }

        pickup.RestoreToWorld(position, rotation);
        return true;
    }

    ItemPickupInteractable FindPickupBySaveId(string sourcePickupId)
    {
        foreach (ItemPickupInteractable pickup in Resources.FindObjectsOfTypeAll<ItemPickupInteractable>())
        {
            if (pickup == null || !pickup.gameObject.scene.IsValid())
            {
                continue;
            }

            if (pickup.SaveId == sourcePickupId)
            {
                return pickup;
            }
        }

        return null;
    }

    Dictionary<string, Item> BuildItemLookup()
    {
        Dictionary<string, Item> itemsById = new Dictionary<string, Item>();

        if (itemCatalog != null)
        {
            foreach (Item item in itemCatalog)
            {
                AddItemToLookup(itemsById, item);
            }
        }

        foreach (ItemPickupInteractable pickup in Resources.FindObjectsOfTypeAll<ItemPickupInteractable>())
        {
            AddItemToLookup(itemsById, pickup.Item);
        }

        foreach (Item item in Resources.FindObjectsOfTypeAll<Item>())
        {
            AddItemToLookup(itemsById, item);
        }

        return itemsById;
    }

    Dictionary<string, Item> BuildPickupItemLookup()
    {
        Dictionary<string, Item> itemsByPickupId = new Dictionary<string, Item>();

        foreach (ItemPickupInteractable pickup in Resources.FindObjectsOfTypeAll<ItemPickupInteractable>())
        {
            if (pickup == null || !pickup.gameObject.scene.IsValid() || string.IsNullOrEmpty(pickup.SaveId) || pickup.Item == null || itemsByPickupId.ContainsKey(pickup.SaveId))
            {
                continue;
            }

            itemsByPickupId.Add(pickup.SaveId, pickup.Item);
        }

        return itemsByPickupId;
    }

    string ResolveSavedSourcePickupId(string savedSourcePickupId, Item item, GameData data, HashSet<string> usedSourcePickupIds)
    {
        if (!string.IsNullOrEmpty(savedSourcePickupId))
        {
            return savedSourcePickupId;
        }

        if (item == null || data.itemStates == null)
        {
            return string.Empty;
        }

        Dictionary<string, Item> itemsByPickupId = BuildPickupItemLookup();

        foreach (ItemSaveData itemState in data.itemStates)
        {
            if (itemState == null || !itemState.isPickedUp || string.IsNullOrEmpty(itemState.id) || usedSourcePickupIds.Contains(itemState.id))
            {
                continue;
            }

            if (itemsByPickupId.TryGetValue(itemState.id, out Item pickupItem) && pickupItem != null && pickupItem.Id == item.Id)
            {
                return itemState.id;
            }
        }

        return string.Empty;
    }

    bool RestoreLegacyPickedUpItems(GameData data)
    {
        if (data.itemStates == null)
        {
            SelectFirstAvailableSlot();
            return false;
        }

        bool restoredAnyItem = false;
        Dictionary<string, Item> itemsByPickupId = BuildPickupItemLookup();

        foreach (ItemSaveData itemState in data.itemStates)
        {
            if (itemState == null || !itemState.isPickedUp || string.IsNullOrEmpty(itemState.id))
            {
                continue;
            }

            if (itemsByPickupId.TryGetValue(itemState.id, out Item item))
            {
                int slotIndex = FindFirstEmptySlot();
                if (slotIndex < 0)
                {
                    break;
                }

                AddItemToSlot(slotIndex, item, itemState.id, false, false);
                restoredAnyItem = true;
            }
        }

        SelectFirstAvailableSlot();
        return restoredAnyItem;
    }

    void AddItemToLookup(Dictionary<string, Item> itemsById, Item item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id) || itemsById.ContainsKey(item.Id))
        {
            return;
        }

        itemsById.Add(item.Id, item);
    }

    void PlayOneShot(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
