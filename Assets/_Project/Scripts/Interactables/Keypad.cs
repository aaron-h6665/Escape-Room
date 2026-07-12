using UnityEngine;
using System.Collections.Generic;

public class Keypad : Interactable, IDataPersistence, IReplayObject
{
    [SerializeField]
    private GameObject door;
    private bool doorOpen;

    [Header("Save Data")]
    [SerializeField] private string id;

    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

    string StateId => ReplayIdentity.Resolve(this, id);

    void Awake()
    {
        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    protected override void Interact(GameObject interactor)
    {
        doorOpen = !doorOpen;
        ApplyKeypadVisualState();
    }

    public void LoadData(GameData data)
    {
        LoadKeypadState(data, StateId);
    }

    public void SaveData(ref GameData data)
    {
        SaveKeypadState(ref data, StateId);
    }

    public void SaveSnapshot(ref GameData data)
    {
        SaveKeypadState(ref data, StateId);
    }

    public void LoadSnapshot(GameData data)
    {
        LoadKeypadState(data, StateId);
    }

    void LoadKeypadState(GameData data, string stateId)
    {
        if (string.IsNullOrEmpty(stateId) || data.keypadStates == null)
        {
            return;
        }

        KeypadSaveData keypadData = data.keypadStates.Find(keypadState => keypadState.id == stateId);
        if (keypadData == null)
        {
            return;
        }

        doorOpen = keypadData.isOpen;
        ApplyKeypadVisualState();
    }

    void SaveKeypadState(ref GameData data, string stateId)
    {
        if (string.IsNullOrEmpty(stateId))
        {
            return;
        }

        if (data.keypadStates == null)
        {
            data.keypadStates = new List<KeypadSaveData>();
        }

        KeypadSaveData keypadData = data.keypadStates.Find(keypadState => keypadState.id == stateId);
        if (keypadData == null)
        {
            keypadData = new KeypadSaveData();
            keypadData.id = stateId;
            data.keypadStates.Add(keypadData);
        }

        keypadData.isOpen = doorOpen;
    }

    void ApplyKeypadVisualState()
    {
        if (door == null)
        {
            return;
        }

        Animator animator = door.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetBool("isOpen", doorOpen);
        }
    }
}
