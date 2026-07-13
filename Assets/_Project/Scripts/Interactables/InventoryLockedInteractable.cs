using UnityEngine;

public abstract class InventoryLockedInteractable : Interactable
{
    [Header("Inventory Requirement")]
    [SerializeField] protected Inventory inventory;
    [SerializeField] protected Item requiredItem;
    [SerializeField] protected string requiredItemId;

    protected string RequiredItemId => requiredItem != null ? requiredItem.Id : requiredItemId;
    protected bool IsLocked => inventory == null || !inventory.SelectedItemMatches(RequiredItemId);

    protected void ResolveInventory(GameObject interactor = null)
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
