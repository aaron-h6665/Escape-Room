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

        if (inputManager.OnFoot.Drop.triggered)
        {
            inventory.DropSelectedItem();
        }
    }
}
