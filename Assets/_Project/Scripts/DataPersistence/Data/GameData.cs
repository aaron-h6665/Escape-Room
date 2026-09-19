using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GameData
{

    public float elapsedTime;
    public string roomId = "";
    public bool hasMotorState;
    public float caesarSuccessRemaining, inventoryFeedbackRemaining;
    public Vector3 horizontalVelocity, verticalVelocity, cameraLocalPosition, controllerCenter;
    public float controllerHeight;
    public bool crouchRequested, isCrouching, grounded;
    public int deathCount;
    public List<UiSnapshot> uiStates = new List<UiSnapshot>();
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public Quaternion playerCameraRotation;
    public float playerCameraPitch;
    public List<DoorSaveData> doorStates;
    public List<SafeSaveData> safeStates;
    public List<KeypadSaveData> keypadStates;
    public List<ItemSaveData> itemStates;
    public List<NoteSaveData> noteStates;
    public List<CaesarCipherSaveData> caesarCipherStates;
    public List<SimonSaysSaveData> simonSaysStates;
    public List<KeyChoiceSaveData> keyChoiceStates;
    public List<VictorySaveData> victoryStates;
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
        caesarCipherStates = new List<CaesarCipherSaveData>();
        simonSaysStates = new List<SimonSaysSaveData>();
        keyChoiceStates = new List<KeyChoiceSaveData>();
        victoryStates = new List<VictorySaveData>();
        inventory = new InventorySaveData();
        playerPromptText = string.Empty;
    }
}

[System.Serializable]
public class DoorSaveData
{
    public bool hasAnimationState;
    public float animationProgress;
    public Vector3 leftPosition, rightPosition;
    public string id;
    public bool isOpen;
}

[System.Serializable]
public class SafeSaveData
{
    public bool hasAnimationState;
    public float animationProgress;
    public float interactionRemaining;
    public string id;
    public bool isOpen;
}

[System.Serializable]
public class KeypadSaveData
{
    public string id;
    public bool isOpen;
    public string enteredCode;
    public float feedbackRemaining;
    public int failedAttempts;
}

[System.Serializable]
public class KeyChoiceSaveData
{
    public string id;
    public bool answerVerified;
    public bool isSolved;
    public int failedAttempts;
    public int answerAttempts;
}

[System.Serializable]
public class VictorySaveData
{
    public string id;
    public bool hasWon;
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
    public bool hasBeenRead;
}

[System.Serializable]
public class CaesarCipherSaveData
{
    public bool hasPresentation;
    public Quaternion innerRotation, snapStart, snapTarget;
    public float snapElapsed, displayedInnerIndex;
    public bool snapping;
    public string id;
    public int innerIndex;
    public int outerIndex;
    public int selectedRing;
    public bool isInspecting;
}

[System.Serializable]
public class SimonSaysSaveData
{
    public string id;
    public bool isSolved;
    public int phase;
    public int currentRound;
    public int playerInputIndex;
    public bool hasTimeline;
    public int timelineStage, cueIndex, pressedColor;
    public float stageRemaining;
    public List<int> sequence;

    public SimonSaysSaveData()
    {
        id = string.Empty;
        sequence = new List<int>();
    }
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

[System.Serializable]
public class UiSnapshot
{
    public string id, text, feedback, selected;
    public bool isOpen;
    public UnityEngine.Color color;
}
