using System;
using System.Collections.Generic;
using UnityEngine;

public enum ReplayObjectState
{
    Idle = 0,
    Attempted = 1,
    Open = 2,
    Closed = 3,
    PickedUp = 4,
    Dropped = 5,
    Activated = 6,
    Deactivated = 7,
    Completed = 8
}

[Serializable]
public class ReplayEventData
{
    public int schemaVersion = 1;
    public float recordingTime;
    public float gameTime;
    public string eventKind = string.Empty;
    public string objectId = string.Empty;
    public string objectName = string.Empty;
    public string objectCategory = string.Empty;
    public ReplayObjectState state;
    public bool succeeded;
    public bool stateChanged;
    public bool inferred;
    public string itemId = string.Empty;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public float numberValue;
    public string textValue = string.Empty;
    public string customPayload = string.Empty;

    public string ComparisonKey => string.Join("|", eventKind ?? string.Empty, objectId ?? string.Empty, ((int)state).ToString());
}

[Serializable]
public class ReplayPoseSample
{
    public float recordingTime;
    public float gameTime;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public float cameraPitch;
}

[Serializable]
public class ReplayRecordingData
{
    public int formatVersion = 2;
    public string recordingId = string.Empty;
    public string recordingKind = "normal";
    public string sourceRecordingId = string.Empty;
    public string summaryId = string.Empty;
    public float takeoverAtRecordingTime = -1f;
    public float takeoverAtGameTime = -1f;
    public string createdUtc = string.Empty;
    public string sceneName = string.Empty;
    public float snapshotDelta = 0.033333335f;
    public float duration;
    public List<ReplayPoseSample> poses = new List<ReplayPoseSample>();
    public List<SnapshotData> worldCheckpoints = new List<SnapshotData>();
    public List<ReplayEventData> events = new List<ReplayEventData>();

    public ReplayRecordingData()
    {
    }

    public ReplayRecordingData(string kind, float sampleDelta)
    {
        recordingId = Guid.NewGuid().ToString("N");
        recordingKind = kind;
        snapshotDelta = sampleDelta;
        createdUtc = DateTime.UtcNow.ToString("O");
        sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    }
}

public interface IReplayEventTarget
{
    string ReplayTargetId { get; }
    string ReplayTargetName { get; }
    string ReplayTargetCategory { get; }
    ReplayObjectState ReplayState { get; }
    bool ApplyReplayEvent(ReplayEventData replayEvent);
}

public static class ReplayEventBus
{
    public static void Publish(ReplayEventData replayEvent)
    {
        if (ReplayManager.IsRecordingActive())
        {
            ReplayManager.instance.RecordEvent(replayEvent);
        }
    }

    public static void Publish(IReplayEventTarget target, string eventKind, ReplayObjectState state, bool succeeded, bool stateChanged, string itemId = "", Vector3? position = null, Quaternion? rotation = null, string textValue = "", string customPayload = "", float numberValue = 0f)
    {
        ReplayManager manager = ReplayManager.instance;
        if (manager == null || target == null || !ReplayManager.IsRecordingActive())
        {
            return;
        }

        Publish(new ReplayEventData
        {
            eventKind = eventKind ?? string.Empty,
            objectId = target.ReplayTargetId ?? string.Empty,
            objectName = target.ReplayTargetName ?? string.Empty,
            objectCategory = target.ReplayTargetCategory ?? string.Empty,
            state = state,
            succeeded = succeeded,
            stateChanged = stateChanged,
            itemId = itemId ?? string.Empty,
            position = position ?? Vector3.zero,
            rotation = rotation ?? Quaternion.identity,
            numberValue = numberValue,
            textValue = textValue ?? string.Empty,
            customPayload = customPayload ?? string.Empty
        });
    }
}
