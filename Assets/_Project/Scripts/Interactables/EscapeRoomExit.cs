using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class EscapeRoomExit : MonoBehaviour, IDataPersistence, IReplayObject, IReplayEventTarget
{
    [SerializeField] string id;
    [SerializeField] GameObject victoryScreen;
    [SerializeField] TMP_Text victoryText;
    [SerializeField] bool hasWon;

    public string ReplayTargetId => ReplayIdentity.Resolve(this, id);
    public string ReplayTargetName => gameObject.name;
    public string ReplayTargetCategory => "Victory";
    public ReplayObjectState ReplayState => hasWon ? ReplayObjectState.Completed : ReplayObjectState.Idle;
    public bool HasWon => hasWon;

    void Awake()
    {
        ApplyState();
        ReplayManager.instance?.Register(this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (ReplayManager.IsPlaybackActive() || hasWon || other.GetComponentInParent<PlayerMotor>() == null) return;
        Complete(!ReplayManager.IsPlaybackActive());
    }

    void Complete(bool record)
    {
        hasWon = true;
        ApplyState();
        if (record)
        {
            ReplayEventBus.Publish(this, "escape_room_completed", ReplayObjectState.Completed, true, true);
            ReplayManager.instance?.CompleteGame();
            StudyMenuPanel.ShowCompletion();
            DataPersistenceManager.instance?.SaveGame();
        }
    }

    void ApplyState()
    {
        if (victoryScreen != null) victoryScreen.SetActive(hasWon);
        if (victoryText != null) victoryText.text = "ESCAPE ROOM COMPLETE\n\n<size=55%><color=#C2CAD8>You have completed the escape room.\n</color></size>";
        InputManager input = FindAnyObjectByType<InputManager>();
        if (!hasWon) { input?.ReleaseControl(this); return; }
        input?.AcquireControl(this);
        FindAnyObjectByType<InventoryUI>()?.SetVisible(false);
        FindAnyObjectByType<PlayerUI>()?.SetPromptVisible(false);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        if (replayEvent?.eventKind != "escape_room_completed") return false;
        Complete(false);
        return true;
    }

    public void LoadData(GameData data) => LoadState(data);
    public void LoadSnapshot(GameData data) => LoadState(data);
    public void SaveData(ref GameData data) => SaveState(ref data);
    public void SaveSnapshot(ref GameData data) => SaveState(ref data);

    void LoadState(GameData data)
    {
        VictorySaveData saved = data?.victoryStates?.Find(value => value.id == ReplayTargetId);
        hasWon = saved != null && saved.hasWon;
        ApplyState();
    }

    void SaveState(ref GameData data)
    {
        data ??= new GameData();
        data.victoryStates ??= new List<VictorySaveData>();
        VictorySaveData saved = data.victoryStates.Find(value => value.id == ReplayTargetId);
        if (saved == null)
        {
            saved = new VictorySaveData { id = ReplayTargetId };
            data.victoryStates.Add(saved);
        }
        saved.hasWon = hasWon;
    }
}
