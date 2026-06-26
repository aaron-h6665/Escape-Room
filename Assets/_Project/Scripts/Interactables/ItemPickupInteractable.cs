using UnityEngine;

public class ItemPickupInteractable : Interactable
{
    [SerializeField]
    Item item;
    [SerializeField]
    Inventory inventory;
    [SerializeField]
    bool destroyOnPickup = true;

    public void Initialize(Item item)
    {
        this.item = item;
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
        Inventory targetInventory = ResolveInventory(interactor);
        if (targetInventory == null)
        {
            Debug.LogWarning("Item pickup could not find an Inventory on the interacting object.", this);
            return;
        }

        if (!targetInventory.AddItem(item))
        {
            return;
        }

        if (destroyOnPickup)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
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
