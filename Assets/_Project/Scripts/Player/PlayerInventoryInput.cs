using UnityEngine;

public class PlayerInventoryInput : MonoBehaviour
{
    [SerializeField]
    InputManager inputManager;
    [SerializeField]
    Inventory inventory;

    void Awake()
    {
        if (inputManager == null)
        {
            inputManager = GetComponent<InputManager>();
        }

        if (inventory == null)
        {
            inventory = GetComponent<Inventory>();
        }
    }

    void OnEnable()
    {
        if (inputManager == null)
        {
            return;
        }

        inputManager.InventoryNavigationPressed += OnInventoryNavigationPressed;
        inputManager.InventorySlotPressed += OnInventorySlotPressed;
    }

    void OnDisable()
    {
        if (inputManager == null)
        {
            return;
        }

        inputManager.InventoryNavigationPressed -= OnInventoryNavigationPressed;
        inputManager.InventorySlotPressed -= OnInventorySlotPressed;
    }

    void Update()
    {
        if (ReplayManager.IsPlaybackActive())
        {
            return;
        }

        if (inputManager == null || inventory == null)
        {
            return;
        }

        if (!inputManager.PlayerControlLocked && inputManager.OnFoot.Drop.triggered)
        {
            inventory.DropSelectedItem();
        }
    }

    void OnInventoryNavigationPressed(int direction)
    {
        if (inventory == null || inputManager == null || inputManager.PlayerControlLocked || ReplayManager.IsPlaybackActive())
        {
            return;
        }

        if (direction > 0)
        {
            inventory.SelectNextSlot();
        }
        else if (direction < 0)
        {
            inventory.SelectPreviousSlot();
        }
    }

    void OnInventorySlotPressed(int slotIndex)
    {
        if (inventory == null || inputManager == null || inputManager.PlayerControlLocked || ReplayManager.IsPlaybackActive())
        {
            return;
        }

        inventory.SelectSlot(slotIndex);
    }
}
