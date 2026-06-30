using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class Inventory : MonoBehaviour, IDataPersistence
{
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
    string selectedInventoryId;

    readonly Dictionary<string, Item> inventory = new();
    readonly Dictionary<string, string> inventorySourcePickupIds = new();
    readonly List<string> inventoryOrder = new();

    public bool HasSelectedItem => !string.IsNullOrEmpty(selectedInventoryId) && inventory.ContainsKey(selectedInventoryId);
    public Item SelectedItem => HasSelectedItem ? inventory[selectedInventoryId] : null;

    void Awake()
    {
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

        if (ui != null)
        {
            ui.Initialize(this);
        }
    }

    public bool AddItem(Item item)
    {
        return AddItem(item, string.Empty);
    }

    public bool AddItem(Item item, string sourcePickupId)
    {
        if (item == null)
        {
            Debug.LogWarning("Tried to add a null item to the inventory.", this);
            return false;
        }

        AddItemToInventory(item, sourcePickupId, true, true);
        return true;
    }

    public void LoadData(GameData data)
    {
        ClearInventory();
        Dictionary<string, Item> itemsById = BuildItemLookup();

        if (data.inventory == null || data.inventory.items == null)
        {
            RestoreLegacyPickedUpItems(data);
            return;
        }

        if (data.inventory.items.Count == 0 && RestoreLegacyPickedUpItems(data))
        {
            return;
        }

        List<InventoryItemSaveData> savedItems = new List<InventoryItemSaveData>(data.inventory.items);
        savedItems.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
        HashSet<string> usedSourcePickupIds = new HashSet<string>();

        foreach (InventoryItemSaveData savedItem in savedItems)
        {
            if (savedItem == null || string.IsNullOrWhiteSpace(savedItem.itemId))
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

            AddItemToInventory(item, sourcePickupId, false, false);
        }

        if (data.inventory.selectedIndex >= 0 && data.inventory.selectedIndex < inventoryOrder.Count)
        {
            SelectItem(inventoryOrder[data.inventory.selectedIndex]);
        }
        else
        {
            SelectFirstAvailableItem();
        }
    }

    public void SaveData(ref GameData data)
    {
        if (data.inventory == null)
        {
            data.inventory = new InventorySaveData();
        }

        if (data.inventory.items == null)
        {
            data.inventory.items = new List<InventoryItemSaveData>();
        }

        data.inventory.items.Clear();
        data.inventory.selectedIndex = inventoryOrder.IndexOf(selectedInventoryId);

        for (int i = 0; i < inventoryOrder.Count; i++)
        {
            string inventoryId = inventoryOrder[i];
            if (!inventory.TryGetValue(inventoryId, out Item item) || item == null)
            {
                continue;
            }

            data.inventory.items.Add(new InventoryItemSaveData
            {
                slotIndex = i,
                itemId = item.Id,
                sourcePickupId = GetSourcePickupId(inventoryId)
            });
        }
    }

    string AddItemToInventory(Item item, string sourcePickupId, bool playAudio, bool autoSelect)
    {
        string inventoryId = Guid.NewGuid().ToString();
        inventory.Add(inventoryId, item);
        inventorySourcePickupIds.Add(inventoryId, sourcePickupId);
        inventoryOrder.Add(inventoryId);
        ui?.AddUIItem(inventoryId, item);

        if (autoSelect && string.IsNullOrEmpty(selectedInventoryId))
        {
            SelectItem(inventoryId);
        }

        if (playAudio)
        {
            PlayOneShot(pickUpItemAudio);
        }

        return inventoryId;
    }

    void ClearInventory()
    {
        foreach (string inventoryId in inventoryOrder)
        {
            ui?.RemoveUIItem(inventoryId);
        }

        inventory.Clear();
        inventorySourcePickupIds.Clear();
        inventoryOrder.Clear();
        selectedInventoryId = null;
        ui?.SetSelectedItem(null);
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

        foreach (Item item in inventory.Values)
        {
            if (item != null && item.Id == itemId)
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

    public void SelectItem(string inventoryId)
    {
        if (string.IsNullOrEmpty(inventoryId) || !inventory.ContainsKey(inventoryId))
        {
            selectedInventoryId = null;
            ui?.SetSelectedItem(null);
            return;
        }

        selectedInventoryId = inventoryId;
        ui?.SetSelectedItem(selectedInventoryId);
    }

    public bool DropSelectedItem()
    {
        if (!HasSelectedItem)
        {
            SelectFirstAvailableItem();
        }

        return HasSelectedItem && DropItem(selectedInventoryId);
    }

    public bool DropItem(string inventoryId)
    {
        if (string.IsNullOrEmpty(inventoryId) || !inventory.TryGetValue(inventoryId, out Item item) || item == null)
        {
            return false;
        }

        Vector3 dropPosition = dropOrigin.position + dropOrigin.forward * dropDistance;
        Quaternion dropRotation = Quaternion.identity;

        if (!TryRestorePersistentPickup(GetSourcePickupId(inventoryId), dropPosition, dropRotation))
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

        inventory.Remove(inventoryId);
        inventorySourcePickupIds.Remove(inventoryId);
        inventoryOrder.Remove(inventoryId);
        ui?.RemoveUIItem(inventoryId);

        if (selectedInventoryId == inventoryId)
        {
            selectedInventoryId = null;
            SelectFirstAvailableItem();
        }

        PlayOneShot(dropItemAudio);
        return true;
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
                AddItemToInventory(item, itemState.id, false, false);
                restoredAnyItem = true;
            }
        }

        SelectFirstAvailableItem();
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

    string GetSourcePickupId(string inventoryId)
    {
        return inventorySourcePickupIds.TryGetValue(inventoryId, out string sourcePickupId) ? sourcePickupId : string.Empty;
    }

    void SelectFirstAvailableItem()
    {
        foreach (string inventoryId in inventoryOrder)
        {
            SelectItem(inventoryId);
            return;
        }

        SelectItem(null);
    }

    void PlayOneShot(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

}
