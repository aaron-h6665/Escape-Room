using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour, IReplayObject, IReplayTimeline
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
    [SerializeField]
    TMP_Text inventoryFullText;

    [Header("Messages")]
    [SerializeField]
    float inventoryFullMessageDuration = 1f;

    readonly List<ItemUI> slotUIs = new List<ItemUI>();
    float inventoryFeedbackRemaining;
    public bool IsVisible => gameObject.activeSelf;

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    void Awake()
    {
        ResolveInventoryFullText();
        HideInventoryFullMessage();
    }

    public void Initialize(Inventory inventory)
    {
        this.inventory = inventory;

        ResolveInventoryFullText();
        HideInventoryFullMessage();

        BuildSlots();
        RefreshSlots();
    }

    public void RefreshSlots()
    {
        if (inventory == null)
        {
            return;
        }

        if (slotUIs.Count != Inventory.SlotCount)
        {
            BuildSlots();
        }

        int selectedSlotIndex = inventory.SelectedSlotIndex;
        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (slotUIs[i] == null)
            {
                continue;
            }

            inventory.TryGetItemAt(i, out Item item);
            slotUIs[i].SetSlot(item, i == selectedSlotIndex);
        }
    }

    public void ShowInventoryFullMessage()
    {
        if (inventoryFullText == null || inventory == null || !inventory.IsFull)
        {
            return;
        }

        inventoryFeedbackRemaining = inventoryFullMessageDuration;
        RenderFeedback();
    }
    void Update()
    {
        if (!ReplayManager.IsPlaybackActive() && !(ReplayManager.instance?.IsHandoffFrame ?? false)) AdvanceReplayPresentation(Time.deltaTime);
    }
    public void AdvanceReplayPresentation(float seconds)
    {
        inventoryFeedbackRemaining = Mathf.Max(0f, inventoryFeedbackRemaining - seconds);
        RenderFeedback();
    }
    void RenderFeedback()
    {
        if (inventoryFullText == null) return;
        inventoryFullText.text = "Inventory is full.";
        inventoryFullText.gameObject.SetActive(inventoryFeedbackRemaining > 0f);
    }
    void HideInventoryFullMessage() { inventoryFeedbackRemaining = 0f; RenderFeedback(); }
    public void SaveSnapshot(ref GameData data) => data.inventoryFeedbackRemaining = inventoryFeedbackRemaining;
    public void LoadSnapshot(GameData data) { inventoryFeedbackRemaining = data.inventoryFeedbackRemaining; RenderFeedback(); }

    void ResolveInventoryFullText()
    {
        if (inventoryFullText != null)
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        foreach (TMP_Text candidate in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            string normalizedName = candidate.gameObject.name
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();

            if (normalizedName.Contains("inventoryfull"))
            {
                inventoryFullText = candidate;
                return;
            }
        }
    }

    void BuildSlots()
    {
        Transform parent = ResolveInventoryParent();
        if (uiItemPrefab == null || parent == null || inventory == null)
        {
            return;
        }

        slotUIs.Clear();
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            ItemUI existingItemUI = parent.GetChild(i).GetComponent<ItemUI>();
            if (existingItemUI != null)
            {
                Destroy(existingItemUI.gameObject);
            }
        }

        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            GameObject slotObject = Instantiate(uiItemPrefab, parent, false);
            ItemUI itemUI = slotObject.GetComponent<ItemUI>();
            if (itemUI == null)
            {
                Debug.LogError("The inventory UI item prefab must contain an ItemUI component.", slotObject);
                Destroy(slotObject);
                continue;
            }

            itemUI.Initialize(i, slotIndex => { inventory.SelectSlot(slotIndex); });
            slotUIs.Add(itemUI);
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

    void OnDisable()
    {
        HideInventoryFullMessage();
    }
}
