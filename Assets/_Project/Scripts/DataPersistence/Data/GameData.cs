using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GameData
{

    public float elapsedTime;
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public List<DoorSaveData> doorStates;
    public List<ItemSaveData> itemStates;
    public InventorySaveData inventory;

    public GameData()
    {
        this.elapsedTime = 0f;
        playerPosition = Vector3.zero;
        playerRotation = Quaternion.identity;
        doorStates = new List<DoorSaveData>();
        itemStates = new List<ItemSaveData>();
        inventory = new InventorySaveData();
    }
}

[System.Serializable]
public class DoorSaveData
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
