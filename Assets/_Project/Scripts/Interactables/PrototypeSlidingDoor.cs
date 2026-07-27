using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PrototypeSlidingDoor : MonoBehaviour, IDataPersistence, IReplayObject, IReplayEventTarget
{
    [SerializeField] string id;
    [SerializeField] Transform leftPanel;
    [SerializeField] Transform rightPanel;
    [SerializeField] Vector3 leftOpenOffset = new Vector3(-1.7f, 0f, 0f);
    [SerializeField] Vector3 rightOpenOffset = new Vector3(1.7f, 0f, 0f);
    [SerializeField, Min(0.05f)] float animationDuration = 0.8f;
    [SerializeField] bool isOpen;

    Vector3 leftClosedPosition;
    Vector3 rightClosedPosition;
    bool positionsCaptured;
    Coroutine animationRoutine;

    public string ReplayTargetId => ReplayIdentity.Resolve(this, id);
    public string ReplayTargetName => gameObject.name;
    public string ReplayTargetCategory => "Door";
    public ReplayObjectState ReplayState => isOpen ? ReplayObjectState.Open : ReplayObjectState.Closed;
    public bool IsOpen => isOpen;

    void Awake()
    {
        CapturePositions();
        ApplyImmediate();
        ReplayManager.instance?.Register(this);
    }

    public void Open() => SetOpen(true, true);
    public void Close() => SetOpen(false, true);

    public void SetOpen(bool open, bool recordEvent)
    {
        CapturePositions();
        if (isOpen == open)
        {
            ApplyImmediate();
            return;
        }

        isOpen = open;
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }
        animationRoutine = StartCoroutine(Animate());

        if (recordEvent)
        {
            ReplayEventBus.Publish(this, open ? "door_opened" : "door_closed", ReplayState, true, true);
        }
    }

    IEnumerator Animate()
    {
        Vector3 leftStart = leftPanel != null ? leftPanel.localPosition : Vector3.zero;
        Vector3 rightStart = rightPanel != null ? rightPanel.localPosition : Vector3.zero;
        Vector3 leftTarget = leftClosedPosition + (isOpen ? leftOpenOffset : Vector3.zero);
        Vector3 rightTarget = rightClosedPosition + (isOpen ? rightOpenOffset : Vector3.zero);
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / animationDuration));
            if (leftPanel != null) leftPanel.localPosition = Vector3.Lerp(leftStart, leftTarget, t);
            if (rightPanel != null) rightPanel.localPosition = Vector3.Lerp(rightStart, rightTarget, t);
            yield return null;
        }

        ApplyImmediate();
        animationRoutine = null;
    }

    void CapturePositions()
    {
        if (positionsCaptured) return;
        leftClosedPosition = leftPanel != null ? leftPanel.localPosition : Vector3.zero;
        rightClosedPosition = rightPanel != null ? rightPanel.localPosition : Vector3.zero;
        positionsCaptured = true;
    }

    void ApplyImmediate()
    {
        CapturePositions();
        if (leftPanel != null) leftPanel.localPosition = leftClosedPosition + (isOpen ? leftOpenOffset : Vector3.zero);
        if (rightPanel != null) rightPanel.localPosition = rightClosedPosition + (isOpen ? rightOpenOffset : Vector3.zero);
    }

    public bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        if (replayEvent == null) return false;
        if (replayEvent.eventKind == "door_opened") { SetOpen(true, false); return true; }
        if (replayEvent.eventKind == "door_closed") { SetOpen(false, false); return true; }
        return false;
    }

    public void LoadData(GameData data) => LoadState(data);
    public void LoadSnapshot(GameData data) => LoadState(data);
    public void SaveData(ref GameData data) => SaveState(ref data);
    public void SaveSnapshot(ref GameData data) => SaveState(ref data);

    void LoadState(GameData data)
    {
        DoorSaveData saved = data?.doorStates?.Find(value => value.id == ReplayTargetId);
        if (saved == null) return;
        isOpen = saved.isOpen;
        if (animationRoutine != null) StopCoroutine(animationRoutine);
        animationRoutine = null;
        ApplyImmediate();
    }

    void SaveState(ref GameData data)
    {
        data ??= new GameData();
        data.doorStates ??= new List<DoorSaveData>();
        DoorSaveData saved = data.doorStates.Find(value => value.id == ReplayTargetId);
        if (saved == null)
        {
            saved = new DoorSaveData { id = ReplayTargetId };
            data.doorStates.Add(saved);
        }
        saved.isOpen = isOpen;
    }
}
