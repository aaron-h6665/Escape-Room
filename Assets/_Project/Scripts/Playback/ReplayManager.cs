using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ReplayManager : MonoBehaviour
{
    public const string DefaultReplayFileName = "replay.json";
    public const float DefaultSnapshotDelta = 0.033333335f;
    public static ReplayManager instance;

    public enum State { Idle, Record, Playback, Takeover }
    public enum LaunchMode { None, Record, Playback }
    public enum ReplaySelectionMode { Random, Manual }

    public static LaunchMode QueuedLaunchMode { get; private set; }
    public static LaunchMode CurrentLaunchMode { get; private set; }

    [Header("Replay Storage")]
    public ReplayContainer replayContainer;
    [SerializeField] string replayFileName = DefaultReplayFileName;
    [SerializeField] bool saveReplayOnStop = true;
    [SerializeField] bool loadReplayFileOnPlayback = true;
    [SerializeField] ReplaySelectionMode playbackSelection = ReplaySelectionMode.Random;
    [SerializeField] string manualRecordingFileName = string.Empty;

    [Header("State")]
    [SerializeField] State currentState;

    [Tooltip("Seconds between recorded player pose samples. 0.033333335 is approximately 30 FPS; 0.016666667 is approximately 60 FPS.")]
    public float snapshotDelta = DefaultSnapshotDelta;

    [NonSerialized] public List<IReplayObject> replayObjects = new List<IReplayObject>();
    public State CurrentState => currentState;
    public float CurrentRecordingTime => recordingTime;
    public ReplayRecordingData ActiveRecording => activeRecording;
    public ReplayRecordingData SourceRecording => sourceRecording;

    ReplayRecordingData activeRecording;
    ReplayRecordingData sourceRecording;
    bool legacyPlayback;
    bool finalized;
    float recordingTime;
    float sampleAccumulator;
    int poseIndex;
    int checkpointIndex;
    int eventIndex;
    int legacySnapshotIndex;
    string loadedReplayPath = string.Empty;
    InputManager inputManager;
    PlayerMotor playerMotor;
    PlayerLook playerLook;
    TimeSpentManager timeSpentManager;
    readonly Dictionary<string, IReplayEventTarget> eventTargets = new Dictionary<string, IReplayEventTarget>();
    readonly HashSet<string> unsupportedEventWarnings = new HashSet<string>();
    [NonSerialized] string storageRootOverride;

    string StorageRoot => string.IsNullOrWhiteSpace(storageRootOverride) ? Application.persistentDataPath : storageRootOverride;
    string NormalDirectory => Path.Combine(StorageRoot, "recordings", "normal");
    string TakeoverDirectory => Path.Combine(StorageRoot, "recordings", "takeover");
    string SummaryDirectory => Path.Combine(StorageRoot, "summaries");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        instance = null;
        QueuedLaunchMode = LaunchMode.None;
        CurrentLaunchMode = LaunchMode.None;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("Found more than one ReplayManager in the scene.");
            enabled = false;
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureReplayContainer();
        ResolveSceneReferences();
        if (GetComponent<TakeoverOverlay>() == null)
        {
            gameObject.AddComponent<TakeoverOverlay>();
        }
    }

    void Start()
    {
        RefreshReplayObjects();
        ResolveSceneReferences();
        StartQueuedLaunchMode();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (inputManager != null)
        {
            inputManager.TakeoverPressed -= TakeOver;
        }
        if (instance == this)
        {
            instance = null;
        }
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (scene.name == SceneTransitionService.LoadingSceneName)
        {
            return;
        }

        RefreshReplayObjects();
        ResolveSceneReferences();
        RefreshEventTargets();

        if (currentState == State.Idle && QueuedLaunchMode != LaunchMode.None)
        {
            StartQueuedLaunchMode();
        }
    }

    void OnApplicationQuit()
    {
        FinalizeActiveRecording("application_quit");
    }

    void Update()
    {
        if (currentState == State.Record || currentState == State.Takeover)
        {
            RecordingUpdate();
        }
        else if (currentState == State.Playback)
        {
            PlaybackUpdate();
        }
    }

    public void StartRecording()
    {
        CurrentLaunchMode = LaunchMode.Record;
        BeginRecording("normal", null, -1f, -1f);
        Debug.Log($"Normal recording started. ID={activeRecording.recordingId}, sample interval={activeRecording.snapshotDelta:0.########} seconds.", this);
    }

    void BeginRecording(string kind, ReplayRecordingData source, float takeoverAt, float takeoverGameTime)
    {
        float resolvedDelta = ResolveSnapshotDelta();
        RefreshReplayObjects();
        ResolveSceneReferences();
        finalized = false;
        recordingTime = 0f;
        sampleAccumulator = 0f;
        activeRecording = new ReplayRecordingData(kind, resolvedDelta);
        if (source != null)
        {
            activeRecording.sourceRecordingId = source.recordingId;
            activeRecording.takeoverAtRecordingTime = takeoverAt;
            activeRecording.takeoverAtGameTime = takeoverGameTime;
        }
        currentState = kind == "takeover" ? State.Takeover : State.Record;
        CapturePose();
        CaptureWorldCheckpoint();
    }

    public void StartPlayback()
    {
        CurrentLaunchMode = LaunchMode.Playback;
        finalized = false;
        bool loaded = loadReplayFileOnPlayback ? LoadReplay() : replayContainer.Count > 0;
        if (!loadReplayFileOnPlayback && loaded)
        {
            legacyPlayback = true;
            loadedReplayPath = "ReplayContainer";
        }
        if (loaded)
        {
            if (legacyPlayback)
            {
                if (replayContainer.Count == 0)
                {
                    Debug.LogWarning("Playback was requested, but the legacy replay contains no snapshots.", this);
                    currentState = State.Idle;
                    return;
                }
            }
            else if (sourceRecording == null || (sourceRecording.poses.Count == 0 && sourceRecording.worldCheckpoints.Count == 0))
            {
                Debug.LogWarning("Playback was requested, but no pose samples or world checkpoints are available.", this);
                currentState = State.Idle;
                return;
            }

            RefreshReplayObjects();
            ResolveSceneReferences();
            recordingTime = 0f;
            poseIndex = 0;
            checkpointIndex = 0;
            eventIndex = 0;
            legacySnapshotIndex = 0;
            currentState = State.Playback;

            if (legacyPlayback)
            {
                ApplyLegacySnapshot(0);
                legacySnapshotIndex = 1;
            }
            else
            {
                ApplyDueWorldCheckpoints();
                ApplyDueEvents();
                ApplyPoseAtTime(0f);
            }

            Debug.Log($"Playback started from '{loadedReplayPath}'. Poses={(sourceRecording?.poses?.Count ?? 0)}, world checkpoints={(sourceRecording?.worldCheckpoints?.Count ?? replayContainer.Count)}, events={(sourceRecording?.events?.Count ?? 0)}.", this);
            return;
        }

        currentState = State.Idle;
    }

    public void TakeOver()
    {
        if (currentState != State.Playback)
        {
            return;
        }

        if (legacyPlayback)
        {
            sourceRecording = ConvertLegacyRecording(replayContainer.Snapshots, snapshotDelta, loadedReplayPath);
        }

        float takeoverAt = recordingTime;
        float takeoverGameTime = timeSpentManager != null ? timeSpentManager.ElapsedTime : takeoverAt;
        ReplayRecordingData source = sourceRecording;
        BeginRecording("takeover", source, takeoverAt, takeoverGameTime);
        Debug.Log($"Takeover began at replay {takeoverAt:0.###}s (game time {takeoverGameTime:0.###}s). Takeover ID={activeRecording.recordingId}, source ID={source?.recordingId}.", this);
    }

    public void RecordEvent(ReplayEventData replayEvent)
    {
        if (!IsRecordingActive() || activeRecording == null || replayEvent == null)
        {
            return;
        }

        replayEvent.recordingTime = recordingTime;
        replayEvent.gameTime = timeSpentManager != null ? timeSpentManager.ElapsedTime : recordingTime;
        activeRecording.events.Add(replayEvent);
        if (replayEvent.stateChanged)
        {
            CaptureWorldCheckpoint();
        }
    }

    public void Register(IReplayObject replayObject)
    {
        if (replayObject != null && !replayObjects.Contains(replayObject))
        {
            replayObjects.Add(replayObject);
        }
    }

    void RecordingUpdate()
    {
        float delta = Time.deltaTime;
        recordingTime += delta;
        sampleAccumulator += delta;
        float interval = activeRecording != null ? activeRecording.snapshotDelta : ResolveSnapshotDelta();
        while (sampleAccumulator >= interval)
        {
            float sampleTime = recordingTime - sampleAccumulator + interval;
            CapturePose(sampleTime);
            sampleAccumulator -= interval;
        }
        if (activeRecording != null)
        {
            activeRecording.duration = recordingTime;
        }
    }

    void PlaybackUpdate()
    {
        recordingTime += Time.deltaTime;
        if (legacyPlayback)
        {
            while (legacySnapshotIndex < replayContainer.Count && replayContainer.GetSnapshot(legacySnapshotIndex, out SnapshotData snapshot) && snapshot.frameTime <= recordingTime)
            {
                ApplySnapshot(snapshot);
                legacySnapshotIndex++;
            }
            if (legacySnapshotIndex >= replayContainer.Count)
            {
                StopPlayback();
            }
            return;
        }

        ApplyDueWorldCheckpoints();
        ApplyDueEvents();
        ApplyPoseAtTime(recordingTime);
        if (sourceRecording != null && recordingTime >= sourceRecording.duration)
        {
            ApplyPoseAtTime(sourceRecording.duration);
            StopPlayback();
        }
    }

    void CapturePose(float? requestedTime = null)
    {
        if (activeRecording == null)
        {
            return;
        }
        ResolveSceneReferences();
        Transform player = playerMotor != null ? playerMotor.transform : (playerLook != null ? playerLook.transform : null);
        if (player == null)
        {
            return;
        }
        float poseTime = requestedTime ?? recordingTime;
        activeRecording.poses.Add(new ReplayPoseSample
        {
            recordingTime = poseTime,
            gameTime = timeSpentManager != null ? timeSpentManager.ElapsedTime : recordingTime,
            position = player.position,
            rotation = player.rotation,
            cameraPitch = playerLook != null ? playerLook.CameraPitch : 0f
        });
    }

    void CaptureWorldCheckpoint()
    {
        if (activeRecording == null)
        {
            return;
        }
        RefreshReplayObjects();
        SnapshotData snapshot = new SnapshotData(recordingTime);
        foreach (IReplayObject replayObject in replayObjects)
        {
            replayObject.SaveSnapshot(ref snapshot.gameData);
        }
        activeRecording.worldCheckpoints.Add(snapshot);
    }

    void ApplyDueWorldCheckpoints()
    {
        if (sourceRecording?.worldCheckpoints == null)
        {
            return;
        }
        while (checkpointIndex < sourceRecording.worldCheckpoints.Count && sourceRecording.worldCheckpoints[checkpointIndex].frameTime <= recordingTime)
        {
            ApplySnapshot(sourceRecording.worldCheckpoints[checkpointIndex]);
            checkpointIndex++;
        }
    }

    void ApplyDueEvents()
    {
        if (sourceRecording?.events == null)
        {
            return;
        }
        RefreshEventTargets();
        while (eventIndex < sourceRecording.events.Count && sourceRecording.events[eventIndex].recordingTime <= recordingTime)
        {
            ReplayEventData replayEvent = sourceRecording.events[eventIndex++];
            if (replayEvent == null || string.IsNullOrWhiteSpace(replayEvent.objectId))
            {
                continue;
            }
            if (!eventTargets.TryGetValue(replayEvent.objectId, out IReplayEventTarget target))
            {
                WarnUnsupportedOnce(replayEvent, $"No replay target exists for event '{replayEvent.eventKind}' on ID '{replayEvent.objectId}'. The event was preserved but skipped.");
                continue;
            }
            if (!target.ApplyReplayEvent(replayEvent) && !(target is IReplayObject))
            {
                WarnUnsupportedOnce(replayEvent, $"Replay target '{target.ReplayTargetName}' does not support event '{replayEvent.eventKind}'. The event was preserved but skipped.");
            }
        }
    }

    void WarnUnsupportedOnce(ReplayEventData replayEvent, string message)
    {
        string key = (replayEvent.objectId ?? string.Empty) + "|" + (replayEvent.eventKind ?? string.Empty);
        if (unsupportedEventWarnings.Add(key))
        {
            Debug.LogWarning(message, this);
        }
    }

    void ApplyPoseAtTime(float time)
    {
        List<ReplayPoseSample> poses = sourceRecording?.poses;
        if (poses == null || poses.Count == 0)
        {
            return;
        }
        while (poseIndex + 1 < poses.Count && poses[poseIndex + 1].recordingTime <= time)
        {
            poseIndex++;
        }
        ReplayPoseSample from = poses[Mathf.Clamp(poseIndex, 0, poses.Count - 1)];
        ReplayPoseSample to = poses[Mathf.Min(poseIndex + 1, poses.Count - 1)];
        float denominator = to.recordingTime - from.recordingTime;
        float t = denominator > 0.00001f ? Mathf.Clamp01((time - from.recordingTime) / denominator) : 0f;
        Vector3 position = Vector3.Lerp(from.position, to.position, t);
        Quaternion rotation = Quaternion.Slerp(from.rotation, to.rotation, t);
        float pitch = Mathf.LerpAngle(from.cameraPitch, to.cameraPitch, t);
        if (playerMotor != null)
        {
            playerMotor.ApplyReplayPosition(position);
        }
        if (playerLook != null)
        {
            playerLook.ApplyReplayLook(rotation, pitch);
        }
        if (timeSpentManager != null)
        {
            timeSpentManager.ApplyReplayElapsedTime(Mathf.Lerp(from.gameTime, to.gameTime, t));
        }
    }

    void ApplyLegacySnapshot(int index)
    {
        if (replayContainer.GetSnapshot(index, out SnapshotData snapshot))
        {
            ApplySnapshot(snapshot);
        }
    }

    void ApplySnapshot(SnapshotData snapshot)
    {
        if (snapshot == null || snapshot.gameData == null)
        {
            return;
        }
        RefreshReplayObjects();
        foreach (IReplayObject replayObject in replayObjects)
        {
            replayObject.LoadSnapshot(snapshot.gameData);
        }
    }

    void StopPlayback()
    {
        currentState = State.Idle;
        Debug.Log("Playback reached the end of the selected recording.", this);
    }

    public void Stop()
    {
        if (currentState == State.Playback)
        {
            StopPlayback();
            return;
        }
        FinalizeActiveRecording("stop");
    }

    public void StopRecording()
    {
        if (IsRecordingActive())
        {
            FinalizeActiveRecording("stop_recording");
        }
    }

    public void CompleteGame()
    {
        FinalizeActiveRecording("game_completed");
    }

    public void FinalizeActiveRecording(string reason)
    {
        if (finalized || activeRecording == null || (currentState != State.Record && currentState != State.Takeover))
        {
            return;
        }
        finalized = true;
        State previousState = currentState;
        activeRecording.duration = recordingTime;
        CapturePose();
        CaptureWorldCheckpoint();
        currentState = State.Idle;
        if (!saveReplayOnStop)
        {
            Debug.Log($"Recording {activeRecording.recordingId} finalized without saving because saveReplayOnStop is disabled.", this);
            return;
        }

        string summaryId = previousState == State.Takeover && sourceRecording != null ? Guid.NewGuid().ToString("N") : string.Empty;
        activeRecording.summaryId = summaryId;
        string directory = previousState == State.Takeover ? TakeoverDirectory : NormalDirectory;
        string recordingPath = Path.Combine(directory, activeRecording.recordingId + ".json");
        if (!TryWriteRecording(activeRecording, recordingPath))
        {
            return;
        }
        Debug.Log($"Recording finalized ({reason}). Saved to '{recordingPath}'. Poses={activeRecording.poses.Count}, checkpoints={activeRecording.worldCheckpoints.Count}, events={activeRecording.events.Count}.", this);

        if (previousState == State.Takeover && sourceRecording != null)
        {
            string summaryPath = Path.Combine(SummaryDirectory, summaryId + ".csv");
            try
            {
                int rows = TakeoverComparison.WriteCsv(sourceRecording, activeRecording, summaryId, summaryPath);
                Debug.Log($"Takeover comparison saved to '{summaryPath}' with {rows} readable rows.", this);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not save takeover comparison to '{summaryPath}'.\n{exception}", this);
            }
        }
    }

    public void SaveReplay()
    {
        if (activeRecording == null)
        {
            Debug.LogWarning("SaveReplay was requested, but there is no active recording.", this);
            return;
        }
        string directory = activeRecording.recordingKind == "takeover" ? TakeoverDirectory : NormalDirectory;
        TryWriteRecording(activeRecording, Path.Combine(directory, activeRecording.recordingId + ".json"));
    }

    bool TryWriteRecording(ReplayRecordingData recording, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(recording, true));
            ReplaceTemporaryFile(temporaryPath, path);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Replay save error for '{path}'.\n{exception}", this);
            return false;
        }
    }

    static void ReplaceTemporaryFile(string temporaryPath, string path)
    {
        if (File.Exists(path))
        {
            File.Replace(temporaryPath, path, null);
        }
        else
        {
            File.Move(temporaryPath, path);
        }
    }

    public bool LoadReplay()
    {
        string path = ResolvePlaybackPath();
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }
        return LoadReplayFromPath(path);
    }

    bool LoadReplayFromPath(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Replay file was not found at '{path}'.", this);
            return false;
        }
        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError($"Replay file is empty: '{path}'.", this);
                return false;
            }
            loadedReplayPath = path;
            if (json.Contains("\"formatVersion\""))
            {
                ReplayRecordingData data = JsonUtility.FromJson<ReplayRecordingData>(json);
                if (data == null || string.IsNullOrWhiteSpace(data.recordingId))
                {
                    Debug.LogError($"Recording metadata is invalid in '{path}'.", this);
                    return false;
                }
                EnsureRecordingLists(data);
                sourceRecording = data;
                snapshotDelta = ResolveLoadedSnapshotDelta(data.snapshotDelta, path);
                legacyPlayback = false;
                ValidateEventTargets(data);
                return true;
            }

            ReplayFileData legacy = JsonUtility.FromJson<ReplayFileData>(json);
            if (legacy?.snapshots == null || legacy.snapshots.Count == 0)
            {
                Debug.LogError($"Legacy replay contains no readable snapshots: '{path}'.", this);
                return false;
            }
            replayContainer.SetSnapshots(legacy.snapshots);
            snapshotDelta = ResolveLoadedSnapshotDelta(legacy.snapshotDelta, path);
            sourceRecording = null;
            legacyPlayback = true;
            Debug.LogWarning($"Loaded legacy replay '{path}'. Interaction timings will be inferred from state changes when takeover occurs.", this);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Replay load error for '{path}'.\n{exception}", this);
            return false;
        }
    }

    string ResolvePlaybackPath()
    {
        if (playbackSelection == ReplaySelectionMode.Manual)
        {
            if (string.IsNullOrWhiteSpace(manualRecordingFileName))
            {
                Debug.LogWarning("Manual replay selection has no filename or UUID. Falling back to a random normal recording.", this);
            }
            else
            {
                string name = manualRecordingFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? manualRecordingFileName : manualRecordingFileName + ".json";
                string manualPath = Path.IsPathRooted(name) ? name : Path.Combine(NormalDirectory, name);
                if (!File.Exists(manualPath) && string.Equals(name, DefaultReplayFileName, StringComparison.OrdinalIgnoreCase))
                {
                    manualPath = SaveFileUtility.GetPath(DefaultReplayFileName, StorageRoot);
                }
                if (IsReadableRecording(manualPath))
                {
                    Debug.Log($"Manual replay selection chose '{manualPath}'.", this);
                    return manualPath;
                }

                Debug.LogWarning($"Manual replay '{manualPath}' is missing or invalid. Falling back to a random normal recording.", this);
            }
        }

        if (Directory.Exists(NormalDirectory))
        {
            string[] recordings = Directory.GetFiles(NormalDirectory, "*.json").Where(IsReadableRecording).ToArray();
            if (recordings.Length > 0)
            {
                string selected = recordings[UnityEngine.Random.Range(0, recordings.Length)];
                Debug.Log($"Random replay selection chose '{selected}' from {recordings.Length} valid normal recordings.", this);
                return selected;
            }
            Debug.LogWarning($"No valid normal recordings exist in '{NormalDirectory}'. Checking the legacy replay location.", this);
        }
        else
        {
            Debug.LogWarning($"Normal recordings folder does not exist yet: '{NormalDirectory}'. Checking the legacy replay location.", this);
        }

        string legacyPath = SaveFileUtility.GetPath(ResolveReplayFileName(), StorageRoot);
        if (File.Exists(legacyPath))
        {
            Debug.Log($"Using legacy replay fallback '{legacyPath}'.", this);
            return legacyPath;
        }
        Debug.LogError($"No recordings exist. Add a normal recording under '{NormalDirectory}' or provide '{legacyPath}'.", this);
        return string.Empty;
    }

    bool IsReadableRecording(string path)
    {
        try
        {
            if (new FileInfo(path).Length <= 2) return false;
            string json = File.ReadAllText(path);
            if (json.Contains("\"formatVersion\""))
            {
                ReplayRecordingData recording = JsonUtility.FromJson<ReplayRecordingData>(json);
                return recording != null && !string.IsNullOrWhiteSpace(recording.recordingId);
            }
            ReplayFileData legacy = JsonUtility.FromJson<ReplayFileData>(json);
            return legacy?.snapshots != null && legacy.snapshots.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    ReplayRecordingData ConvertLegacyRecording(List<SnapshotData> snapshots, float delta, string sourcePath)
    {
        ReplayRecordingData converted = new ReplayRecordingData("normal", ResolveLoadedSnapshotDelta(delta, sourcePath));
        converted.recordingId = "legacy-" + Path.GetFileNameWithoutExtension(sourcePath);
        converted.worldCheckpoints = snapshots ?? new List<SnapshotData>();
        converted.duration = converted.worldCheckpoints.Count > 0 ? converted.worldCheckpoints[converted.worldCheckpoints.Count - 1].frameTime : 0f;
        foreach (SnapshotData snapshot in converted.worldCheckpoints)
        {
            if (snapshot?.gameData == null) continue;
            converted.poses.Add(new ReplayPoseSample
            {
                recordingTime = snapshot.frameTime,
                gameTime = snapshot.gameData.elapsedTime,
                position = snapshot.gameData.playerPosition,
                rotation = snapshot.gameData.playerRotation,
                cameraPitch = snapshot.gameData.playerCameraPitch
            });
        }
        InferLegacyEvents(converted);
        return converted;
    }

    void InferLegacyEvents(ReplayRecordingData recording)
    {
        for (int i = 1; i < recording.worldCheckpoints.Count; i++)
        {
            GameData before = recording.worldCheckpoints[i - 1].gameData;
            GameData after = recording.worldCheckpoints[i].gameData;
            if (before == null || after == null) continue;
            InferBoolChanges(recording, before.doorStates, after.doorStates, recording.worldCheckpoints[i].frameTime, "door_changed", "Door", d => d.id, d => d.isOpen);
            InferBoolChanges(recording, before.safeStates, after.safeStates, recording.worldCheckpoints[i].frameTime, "safe_changed", "Safe", d => d.id, d => d.isOpen);
            InferBoolChanges(recording, before.keypadStates, after.keypadStates, recording.worldCheckpoints[i].frameTime, "room_changed", "Room", d => d.id, d => d.isOpen);
            InferBoolChanges(recording, before.noteStates, after.noteStates, recording.worldCheckpoints[i].frameTime, "note_changed", "Note", d => d.id, d => d.isOpen);
            InferBoolChanges(recording, before.itemStates, after.itemStates, recording.worldCheckpoints[i].frameTime, "item_changed", "Item", d => d.id, d => d.isPickedUp, ReplayObjectState.PickedUp, ReplayObjectState.Dropped);
        }
    }

    void InferBoolChanges<T>(ReplayRecordingData recording, List<T> before, List<T> after, float time, string kind, string category, Func<T, string> id, Func<T, bool> value, ReplayObjectState trueState = ReplayObjectState.Open, ReplayObjectState falseState = ReplayObjectState.Closed) where T : class
    {
        if (before == null || after == null) return;
        Dictionary<string, bool> oldValues = before.Where(x => x != null).GroupBy(id).ToDictionary(g => g.Key, g => value(g.Last()));
        foreach (T current in after.Where(x => x != null))
        {
            string targetId = id(current);
            bool currentValue = value(current);
            if (oldValues.TryGetValue(targetId, out bool oldValue) && oldValue == currentValue) continue;
            recording.events.Add(new ReplayEventData
            {
                recordingTime = time,
                gameTime = time,
                eventKind = kind,
                objectId = targetId,
                objectName = targetId,
                objectCategory = category,
                state = currentValue ? trueState : falseState,
                succeeded = true,
                stateChanged = true,
                inferred = true
            });
        }
    }

    void ValidateEventTargets(ReplayRecordingData data)
    {
        RefreshEventTargets();
        HashSet<string> sceneIds = new HashSet<string>();
        foreach (IReplayEventTarget target in Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(b => b != null && b.gameObject.scene.IsValid()).OfType<IReplayEventTarget>())
        {
            if (string.IsNullOrWhiteSpace(target.ReplayTargetId))
            {
                Debug.LogWarning($"Replay target '{target.ReplayTargetName}' has no stable ID; scene-path fallback will be used.", target as UnityEngine.Object);
            }
            else if (!sceneIds.Add(target.ReplayTargetId))
            {
                Debug.LogError($"Duplicate replay target ID '{target.ReplayTargetId}' detected. Playback comparisons may be ambiguous.", target as UnityEngine.Object);
            }
        }
        foreach (ReplayEventData replayEvent in data.events.Where(e => e != null && !string.IsNullOrWhiteSpace(e.objectId)))
        {
            if (!eventTargets.ContainsKey(replayEvent.objectId))
            {
                WarnUnsupportedOnce(replayEvent, $"Recording references missing replay target ID '{replayEvent.objectId}' for event '{replayEvent.eventKind}'.");
            }
        }
    }

    void RefreshEventTargets()
    {
        eventTargets.Clear();
        foreach (IReplayEventTarget target in Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(b => b != null && b.gameObject.scene.IsValid()).OfType<IReplayEventTarget>())
        {
            if (!string.IsNullOrWhiteSpace(target.ReplayTargetId) && !eventTargets.ContainsKey(target.ReplayTargetId))
            {
                eventTargets.Add(target.ReplayTargetId, target);
            }
        }
    }

    float ResolveSnapshotDelta()
    {
        if (snapshotDelta > 0f && !float.IsNaN(snapshotDelta) && !float.IsInfinity(snapshotDelta)) return snapshotDelta;
        Debug.LogError($"snapshotDelta must be a finite positive number, but was {snapshotDelta}. Falling back to {DefaultSnapshotDelta} seconds.", this);
        return DefaultSnapshotDelta;
    }

    float ResolveLoadedSnapshotDelta(float value, string path)
    {
        if (value > 0f && !float.IsNaN(value) && !float.IsInfinity(value)) return value;
        Debug.LogError($"Recording '{path}' has invalid snapshotDelta {value}. Falling back to {DefaultSnapshotDelta} seconds.", this);
        return DefaultSnapshotDelta;
    }

    void EnsureRecordingLists(ReplayRecordingData data)
    {
        data.poses = data.poses ?? new List<ReplayPoseSample>();
        data.worldCheckpoints = data.worldCheckpoints ?? new List<SnapshotData>();
        data.events = data.events ?? new List<ReplayEventData>();
        data.poses.Sort((a, b) => (a?.recordingTime ?? 0f).CompareTo(b?.recordingTime ?? 0f));
        data.worldCheckpoints.Sort((a, b) => (a?.frameTime ?? 0f).CompareTo(b?.frameTime ?? 0f));
        data.events.Sort((a, b) => (a?.recordingTime ?? 0f).CompareTo(b?.recordingTime ?? 0f));
        data.duration = data.duration > 0f ? data.duration : Mathf.Max(data.poses.LastOrDefault()?.recordingTime ?? 0f, data.worldCheckpoints.LastOrDefault()?.frameTime ?? 0f);
    }

    void RefreshReplayObjects()
    {
        replayObjects = replayObjects ?? new List<IReplayObject>();
        replayObjects.RemoveAll(replayObject => replayObject == null || (replayObject as UnityEngine.Object) == null);
        foreach (IReplayObject replayObject in Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(b => b != null && b.gameObject.scene.IsValid()).OfType<IReplayObject>())
        {
            Register(replayObject);
        }
    }

    void ResolveSceneReferences()
    {
        BindInputManager();
        if (playerMotor == null) playerMotor = FindAnyObjectByType<PlayerMotor>();
        if (playerLook == null) playerLook = FindAnyObjectByType<PlayerLook>();
        if (timeSpentManager == null) timeSpentManager = FindAnyObjectByType<TimeSpentManager>();
    }

    void BindInputManager()
    {
        InputManager resolvedInputManager = FindAnyObjectByType<InputManager>();
        if (inputManager == resolvedInputManager)
        {
            return;
        }

        if (inputManager != null)
        {
            inputManager.TakeoverPressed -= TakeOver;
        }

        inputManager = resolvedInputManager;
        if (inputManager != null)
        {
            inputManager.TakeoverPressed += TakeOver;
        }
    }

    void EnsureReplayContainer()
    {
        if (replayContainer == null) replayContainer = ScriptableObject.CreateInstance<ReplayContainer>();
    }

    void StartQueuedLaunchMode()
    {
        LaunchMode mode = QueuedLaunchMode;
        QueuedLaunchMode = LaunchMode.None;
        if (mode == LaunchMode.Record) StartRecording();
        else if (mode == LaunchMode.Playback) StartPlayback();
    }

    string ResolveReplayFileName() => string.IsNullOrWhiteSpace(replayFileName) ? DefaultReplayFileName : replayFileName;

    public static void QueueRecordingOnNextScene() => QueuedLaunchMode = LaunchMode.Record;
    public static void QueuePlaybackOnNextScene() => QueuedLaunchMode = LaunchMode.Playback;
    public static bool IsPlaybackActive() => instance != null && instance.CurrentState == State.Playback;
    public static bool IsRecordingActive() => instance != null && (instance.CurrentState == State.Record || instance.CurrentState == State.Takeover);
    public static bool IsPlaybackLaunch() => QueuedLaunchMode == LaunchMode.Playback || CurrentLaunchMode == LaunchMode.Playback;
    public static bool IsRecordingLaunch() => QueuedLaunchMode == LaunchMode.Record || CurrentLaunchMode == LaunchMode.Record;
}
