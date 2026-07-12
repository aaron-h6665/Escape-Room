using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GameData
{

    public float elapsedTime;
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public Quaternion playerCameraRotation;
    public float playerCameraPitch;
    public List<DoorSaveData> doorStates;
    public List<SafeSaveData> safeStates;
    public List<KeypadSaveData> keypadStates;
    public List<ItemSaveData> itemStates;
    public List<NoteSaveData> noteStates;
    public InventorySaveData inventory;
    public string playerPromptText;

    public GameData()
    {
        this.elapsedTime = 0f;
        playerPosition = Vector3.zero;
        playerRotation = Quaternion.identity;
        playerCameraRotation = Quaternion.identity;
        playerCameraPitch = 0f;
        doorStates = new List<DoorSaveData>();
        safeStates = new List<SafeSaveData>();
        keypadStates = new List<KeypadSaveData>();
        itemStates = new List<ItemSaveData>();
        noteStates = new List<NoteSaveData>();
        inventory = new InventorySaveData();
        playerPromptText = string.Empty;
    }
}

[System.Serializable]
public class DoorSaveData
{
    public string id;
    public bool isOpen;
}

[System.Serializable]
public class SafeSaveData
{
    public string id;
    public bool isOpen;
}

[System.Serializable]
public class KeypadSaveData
{
    public string id;
    public bool isOpen;
}

[System.Serializable]
public class ItemSaveData
{
    public string id;
    public bool isPickedUp;
    public bool hasWorldTransform;
    public Vector3 position;
    public Quaternion rotation;
}

[System.Serializable]
public class NoteSaveData
{
    public string id;
    public bool isOpen;
}

[System.Serializable]
public class InventorySaveData
{
    public int selectedIndex;
    public List<InventoryItemSaveData> items;

    public InventorySaveData()
    {
        selectedIndex = -1;
        items = new List<InventoryItemSaveData>();
    }
}

[System.Serializable]
public class InventoryItemSaveData
{
    public int slotIndex;
    public string itemId;
    public string sourcePickupId;
}
