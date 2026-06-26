using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;


public class InventoryUI : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField]
    GameObject uiItemPrefab;

    [Header("References")]
    [SerializeField]
    Inventory inventory;
    [SerializeField]
    ScrollRect inventoryScrollView;
    [SerializeField]
    Transform uiInventoryParent;

    [Header("State")]
    readonly Dictionary<string, ItemUI> inventoryUI = new();

    public void Initialize(Inventory inventory)
    {
        this.inventory = inventory;
    }

    public void AddUIItem(string inventoryId, Item item)
    {
        Transform parent = ResolveInventoryParent();
        if (uiItemPrefab == null || parent == null || inventory == null)
        {
            return;
        }

        var itemUI = Instantiate(uiItemPrefab).GetComponent<ItemUI>();
        itemUI.transform.SetParent(parent, false);
        inventoryUI.Add(inventoryId, itemUI);
        itemUI.Initialize(inventoryId, item, inventory.SelectItem);
    }

    public void RemoveUIItem(string inventoryId)
    {
        if (!inventoryUI.TryGetValue(inventoryId, out ItemUI itemUI))
        {
            return;
        }

        inventoryUI.Remove(inventoryId);
        Destroy(itemUI.gameObject);
    }

    public void SetSelectedItem(string inventoryId)
    {
        foreach (var pair in inventoryUI)
        {
            pair.Value.SetSelected(pair.Key == inventoryId);
        }
    }

    Transform ResolveInventoryParent()
    {
        if (inventoryScrollView != null && inventoryScrollView.content != null)
        {
            uiInventoryParent = inventoryScrollView.content;
            return uiInventoryParent;
        }

        ScrollRect scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null && scrollRect.content != null)
        {
            inventoryScrollView = scrollRect;
            uiInventoryParent = scrollRect.content;
            return uiInventoryParent;
        }

        if (uiInventoryParent != null && uiInventoryParent.GetComponent<Canvas>() == null)
        {
            return uiInventoryParent;
        }

        return null;
    }
}
