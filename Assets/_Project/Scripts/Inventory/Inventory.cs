using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class Inventory : MonoBehaviour
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

    [Header("Audio Clips")]
    [SerializeField]
    AudioClip pickUpItemAudio;
    [SerializeField]
    AudioClip dropItemAudio;

    [Header("State")]
    [SerializeField]
    string selectedInventoryId;

    readonly Dictionary<string, Item> inventory = new();

    public bool HasSelectedItem => !string.IsNullOrEmpty(selectedInventoryId) && inventory.ContainsKey(selectedInventoryId);

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
        if (item == null)
        {
            Debug.LogWarning("Tried to add a null item to the inventory.", this);
            return false;
        }

        var inventoryId = Guid.NewGuid().ToString();
        inventory.Add(inventoryId, item);
        ui?.AddUIItem(inventoryId, item);

        if (string.IsNullOrEmpty(selectedInventoryId))
        {
            SelectItem(inventoryId);
        }

        PlayOneShot(pickUpItemAudio);
        return true;
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

        GameObject pickupPrefab = item.prefab != null ? item.prefab : droppedItemPrefab;
        if (pickupPrefab == null)
        {
            Debug.LogWarning($"Cannot drop {item.name} because it has no pickup prefab.", this);
            return false;
        }

        Vector3 dropPosition = dropOrigin.position + dropOrigin.forward * dropDistance;
        GameObject droppedObject = Instantiate(pickupPrefab, dropPosition, Quaternion.identity);

        ItemPickupInteractable pickup = droppedObject.GetComponentInChildren<ItemPickupInteractable>();
        if (pickup != null)
        {
            pickup.Initialize(item);
        }

        inventory.Remove(inventoryId);
        ui?.RemoveUIItem(inventoryId);

        if (selectedInventoryId == inventoryId)
        {
            selectedInventoryId = null;
            SelectFirstAvailableItem();
        }

        PlayOneShot(dropItemAudio);
        return true;
    }

    void SelectFirstAvailableItem()
    {
        foreach (string inventoryId in inventory.Keys)
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
