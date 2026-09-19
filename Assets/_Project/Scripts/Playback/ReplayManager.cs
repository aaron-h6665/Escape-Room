using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
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
    bool snapshotDirty;
    int takeoverFrame = -1;
    long lastAppliedSequence;
    float gameTimeOrigin, nextRecoveryAt;
    bool hadFocus = true;
    float focusTimeScale = 1f;
    bool legacyPreviewAllowed;
    StudyConfiguration configuration;
    ReplayRecordingData observation;
    readonly List<ReplayRecordingData> pendingObservations = new List<ReplayRecordingData>();
    public string LastError { get; private set; } = "";
    bool activeSavePending, recoverySavePending;
    public bool SavePending { get => activeSavePending || recoverySavePending || pendingObservations.Count > 0; private set => activeSavePending = value; }
    public bool IsRestoring { get; private set; }
    public bool IsHandoffFrame => takeoverFrame == Time.frameCount;
    public bool AuthoritativePlayback => IsPlaybackActive() && sourceRecording?.formatVersion == 3;
    public string CurrentRoomId { get; set; } = "";
    public event Action TakenOver;
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
            enabled = false;
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureReplayContainer();
        ResolveSceneReferences();
        gameObject.AddComponent<StudyFrameRecorder>();
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
        if (currentState == State.Playback) FinishObservation("application_quit");
        FinalizeActiveRecording("application_quit");
    }

    void OnApplicationFocus(bool focus)
    {
        if (hadFocus == focus) return;
        hadFocus = focus;
        RecordSessionEvent(focus ? "focus_restored" : "focus_lost");
        if (!focus) { focusTimeScale = Time.timeScale; Time.timeScale = 0f; AudioListener.pause = true; }
        else { Time.timeScale = PauseManager.Instance != null && PauseManager.Instance.IsPaused ? 0f : focusTimeScale; AudioListener.pause = false; }
    }

    void Update()
    {
        if (Time.frameCount == takeoverFrame || !hadFocus) return;
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
        if (SavePending && !RetrySave()) return;
        LastError = "";
        if (!ReadConfiguration()) return;
        CurrentLaunchMode = LaunchMode.Record;
        sourceRecording = null;
        timeSpentManager?.ResetTimer();
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
        activeRecording = NewRecording(kind, resolvedDelta);
        gameTimeOrigin = timeSpentManager != null ? timeSpentManager.ElapsedTime : 0f;
        nextRecoveryAt = 5f;
        SavePending = false;
        if (source != null)
        {
            activeRecording.sourceRecordingId = source.recordingId;
            activeRecording.takeoverAtRecordingTime = takeoverAt;
            activeRecording.takeoverAtGameTime = takeoverGameTime;
        }
        currentState = kind == "takeover" ? State.Takeover : State.Record;
        CapturePose();
        CaptureWorldCheckpoint();
        WriteRecovery();
    }

    public void StartPlayback()
    {
        if (SavePending && !RetrySave()) return;
        LastError = "";
        if (!ReadConfiguration()) return;
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
            observation = NewRecording("observation", ResolveSnapshotDelta());
            observation.sourceRecordingId = sourceRecording?.recordingId ?? "legacy";
            observation.sourceSha256 = HashSource();
            lastAppliedSequence = 0;

            if (legacyPlayback)
            {
                ApplyLegacySnapshot(0);
                legacySnapshotIndex = 1;
            }
            else
            {
                ApplyPlaybackFrame(0f);
            }

            Debug.Log($"Playback started from '{loadedReplayPath}'. Poses={(sourceRecording?.poses?.Count ?? 0)}, world checkpoints={(sourceRecording?.worldCheckpoints?.Count ?? replayContainer.Count)}, events={(sourceRecording?.events?.Count ?? 0)}.", this);
            return;
        }

        currentState = State.Idle;
    }

    public void TakeOver()
    {
        if (currentState != State.Playback || Time.timeScale == 0f || !hadFocus || sourceRecording?.formatVersion != 3)
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
        ReplayRecordingData watching = observation;
        if (watching != null)
        {
            watching.status = "taken_over";
            FinishObservation("takeover");
        }
        takeoverFrame = Time.frameCount;
        inputManager?.ConsumeTakeoverInput();
        // Live ownership is acquired before the first checkpoint is written.
        currentState = State.Takeover;
        GetComponent<TakeoverOverlay>()?.HideForTakeover();
        foreach (IReplayHandoff target in replayObjects.OfType<IReplayHandoff>()) target.OnTakeover();
        TakenOver?.Invoke();
        BeginRecording("takeover", source, takeoverAt, takeoverGameTime);
        activeRecording.sourceAttemptId = watching?.attemptId ?? "";
        activeRecording.takeoverAfterSequence = lastAppliedSequence;
        activeRecording.takeoverRoomId = CurrentRoomId;
        activeRecording.sourceSha256 = HashSource();
        RecordSessionEvent("takeover");
        WriteRecovery();
        Debug.Log($"Takeover began at replay {takeoverAt:0.###}s (game time {takeoverGameTime:0.###}s). Takeover ID={activeRecording.recordingId}, source ID={source?.recordingId}.", this);
    }

    public void RecordEvent(ReplayEventData replayEvent)
    {
        if (!IsRecordingActive() || activeRecording == null || replayEvent == null)
        {
            return;
        }

        replayEvent.utc = DateTime.UtcNow.ToString("O");
        replayEvent.sequence = activeRecording.events.Count + 1;
        replayEvent.roomId = CurrentRoomId;
        replayEvent.milestoneId = AuthoredMilestone(replayEvent);
        replayEvent.recordingTime = recordingTime;
        replayEvent.gameTime = timeSpentManager != null ? timeSpentManager.ElapsedTime : recordingTime;
        activeRecording.events.Add(replayEvent);
        snapshotDirty = true;
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
        recordingTime += Time.deltaTime;
        sampleAccumulator += Time.deltaTime;
        timeSpentManager?.ApplyReplayElapsedTime(gameTimeOrigin + recordingTime);
        if (activeRecording != null) activeRecording.duration = recordingTime;
    }

    // Called after every gameplay LateUpdate, including camera look. Never invents past samples.
    public void CaptureRenderedFrame()
    {
        if (!IsRecordingActive() || activeRecording == null || Time.timeScale == 0f) return;
        if (snapshotDirty || sampleAccumulator >= ResolveSnapshotDelta())
        {
            CapturePose();
            CaptureWorldCheckpoint();
            snapshotDirty = false;
            sampleAccumulator = 0f;
        }
        if (recordingTime >= nextRecoveryAt)
        {
            WriteRecovery();
            nextRecoveryAt = recordingTime + 5f;
        }
    }

    void PlaybackUpdate()
    {
        if (!legacyPlayback && sourceRecording == null) return;
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

        ApplyPlaybackFrame(Mathf.Min(recordingTime, sourceRecording.duration));
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
        SnapshotData snapshot = new SnapshotData(recordingTime) { lastEventSequence = activeRecording.events.Count };
        snapshot.gameData.roomId = CurrentRoomId;
        foreach (IReplayObject replayObject in replayObjects)
        {
            replayObject.SaveSnapshot(ref snapshot.gameData);
        }
        activeRecording.worldCheckpoints.Add(snapshot);
    }

    void ApplyPlaybackFrame(float time)
    {
        if (sourceRecording.formatVersion < 3)
        {
            ApplyDueWorldCheckpoints(); ApplyDueEvents(); ApplyPoseAtTime(time); return;
        }
        var frames = sourceRecording.worldCheckpoints;
        while (checkpointIndex + 1 < frames.Count && frames[checkpointIndex + 1].frameTime <= time) checkpointIndex++;
        if (frames.Count > 0)
        {
            SnapshotData frame = frames[checkpointIndex];
            ApplySnapshot(frame);
            lastAppliedSequence = frame.lastEventSequence;
            foreach (IReplayTimeline target in replayObjects.OfType<IReplayTimeline>())
                target.AdvanceReplayPresentation(Mathf.Max(0f, time - frame.frameTime));
        }
        ApplyPoseAtTime(time);
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
        CurrentRoomId = snapshot.gameData.roomId ?? "";
        IsRestoring = true;
        try
        {
            // Inventory and puzzle state first; door presentation and modals afterwards.
            foreach (IReplayObject replayObject in replayObjects.OrderBy(o => o is Inventory ? 0 : o is IReplayTimeline ? 2 : 1))
                replayObject.LoadSnapshot(snapshot.gameData);
        }
        finally { IsRestoring = false; }
    }

    void StopPlayback()
    {
        FinishObservation("playback_completed");
        currentState = State.Idle;
        StudyMenuPanel.ShowCompletion();
        Debug.Log("Playback reached the end of the selected recording.", this);
    }

    public void Stop()
    {
        if (currentState == State.Playback)
        {
            FinishObservation("stopped");
            currentState = State.Idle;
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
        if (finalized || activeRecording == null || !IsRecordingActive()) return;
        CapturePose(); CaptureWorldCheckpoint();
        activeRecording.duration = recordingTime;
        activeRecording.status = reason == "game_completed" ? "completed" : "incomplete";
        activeRecording.terminationReason = reason;
        activeRecording.endedUtc = DateTime.UtcNow.ToString("O");
        currentState = State.Idle;
        finalized = true;
        SavePending = saveReplayOnStop;
        RetrySave();
    }

    public bool RetrySave()
    {
        if (!SavePending) return true;
        if (recoverySavePending) { WriteRecovery(); if (recoverySavePending) return false; }
        try
        {
            foreach (var pending in pendingObservations.ToArray())
            {
                StudyExports.AtomicWrite(Path.Combine(StorageRoot, "recordings", "observation", pending.recordingId + ".json"), JsonUtility.ToJson(pending, true));
                StudyExports.Write(StorageRoot, pending, null);
                pendingObservations.Remove(pending);
            }
            if (!activeSavePending || activeRecording == null) { LastError = ""; return true; }
            string directory = activeRecording.recordingKind == "takeover" ? TakeoverDirectory : NormalDirectory;
            StudyExports.AtomicWrite(Path.Combine(directory, activeRecording.recordingId + ".json"), JsonUtility.ToJson(activeRecording, true));
            StudyExports.Write(StorageRoot, activeRecording, sourceRecording);
            SavePending = false;
            LastError = "";
            return true;
        }
        catch (Exception e) { LastError = "Could not save. Please retry."; Debug.LogError(e, this); return false; }
    }

    public void SaveReplay() => WriteRecovery();

    void WriteRecovery()
    {
        if (!saveReplayOnStop || activeRecording == null) return;
        try { StudyExports.AtomicWrite(Path.Combine(StorageRoot, "recovery", activeRecording.recordingId + ".json"), JsonUtility.ToJson(activeRecording)); recoverySavePending = false; if (!SavePending) LastError = ""; }
        catch (Exception e) { recoverySavePending = true; LastError = "Could not save. Please retry."; Debug.LogError(e, this); }
    }

    void FinishObservation(string reason)
    {
        if (observation == null) return;
        observation.duration = Mathf.Min(recordingTime, sourceRecording?.duration ?? recordingTime);
        observation.status = reason == "takeover" ? "taken_over" : reason == "playback_completed" ? "completed_without_takeover" : "incomplete";
        observation.terminationReason = reason;
        observation.endedUtc = DateTime.UtcNow.ToString("O");
        pendingObservations.Add(observation);
        observation = null;
        RetrySave();
    }

    public void RecordSessionEvent(string kind, string value = "")
    {
        var e = new ReplayEventData { eventKind = kind, objectId = "session", objectName = "Session", objectCategory = "Session", textValue = value, succeeded = true };
        if (IsRecordingActive()) RecordEvent(e);
        else if (observation != null)
        {
            e.utc = DateTime.UtcNow.ToString("O"); e.sequence = observation.events.Count + 1; e.recordingTime = recordingTime; e.gameTime = recordingTime;
            observation.events.Add(e);
        }
    }

    ReplayRecordingData NewRecording(string kind, float delta)
    {
        return new ReplayRecordingData(kind, delta) { participantCode = configuration?.participantCode ?? "", inputDevice = StudyOptions.DeviceName,
            settingsJson = JsonUtility.ToJson(StudyOptions.Current),
            targetIds = Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(b => b != null && b.gameObject.scene.IsValid()).OfType<IReplayEventTarget>().Select(t => t.ReplayTargetId).OrderBy(x => x, StringComparer.Ordinal).ToList() };
    }

    bool ReadConfiguration()
    {
        try
        {
            configuration = StudyConfiguration.Read(StorageRoot);
            if (configuration != null)
            {
                playbackSelection = configuration.playbackSelection == "manual" ? ReplaySelectionMode.Manual : ReplaySelectionMode.Random;
                manualRecordingFileName = configuration.recordingFile;
                legacyPreviewAllowed = configuration.allowLegacyPreview;
            }
            return true;
        }
        catch (Exception e) { LastError = "Unable to start. Please contact the operator."; Debug.LogError(e, this); return false; }
    }

    string HashSource()
    {
        if (!File.Exists(loadedReplayPath)) return "";
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(loadedReplayPath))).Replace("-", "").ToLowerInvariant();
    }

    static string AuthoredMilestone(ReplayEventData e)
    {
        switch (e.eventKind)
        {
            case "simon_completed": return "simon_completed";
            case "key_choice_submitted": return e.succeeded ? "caesar_completed" : "";
            case "keypad_solved": return "keypad_completed";
            case "escape_room_completed": return "escape_completed";
            default: return "";
        }
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
                if (!IsCompatibleRecording(data) && !(legacyPreviewAllowed && data.formatVersion < 3))
                    throw new InvalidDataException("Recording is not compatible with this study build.");
                EnsureRecordingLists(data);
                sourceRecording = data;
                snapshotDelta = ResolveLoadedSnapshotDelta(data.snapshotDelta, path);
                legacyPlayback = false;
                ValidateEventTargets(data);
                return true;
            }

            if (!legacyPreviewAllowed) throw new InvalidDataException("Legacy preview is disabled.");
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
            string name = manualRecordingFileName ?? "";
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name += ".json";
            string path = Path.IsPathRooted(name) ? name : Path.Combine(NormalDirectory, name);
            if (IsReadableRecording(path)) return path;
            LastError = "Unable to load. Please contact the operator.";
            Debug.LogWarning("Selected recording is missing, incomplete, or incompatible. No random substitution was made.", this);
            return "";
        }
        string[] files = Directory.Exists(NormalDirectory) ? Directory.GetFiles(NormalDirectory, "*.json").Where(IsReadableRecording).ToArray() : Array.Empty<string>();
        if (files.Length > 0) return files[UnityEngine.Random.Range(0, files.Length)];
        LastError = "Unable to load. Please contact the operator.";
        Debug.LogWarning("No compatible completed normal recordings are available.", this);
        return "";
    }

    bool IsReadableRecording(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            if (legacyPreviewAllowed && playbackSelection == ReplaySelectionMode.Manual && !json.Contains("\"formatVersion\""))
                return JsonUtility.FromJson<ReplayFileData>(json)?.snapshots?.Count > 0;
            var data = JsonUtility.FromJson<ReplayRecordingData>(json);
            if (legacyPreviewAllowed && playbackSelection == ReplaySelectionMode.Manual && data?.formatVersion < 3) return true;
            return IsCompatibleRecording(data);
        }
        catch { return false; }
    }

    public static bool IsCompatibleRecording(ReplayRecordingData data)
    {
        if (data == null || data.formatVersion != 3 || string.IsNullOrWhiteSpace(data.recordingId)
            || data.levelVersion != StudyConfiguration.LevelVersion || data.configurationId != StudyConfiguration.ConfigurationId
            || data.recordingKind != "normal" || data.status != "completed" || data.worldCheckpoints == null || data.worldCheckpoints.Count == 0
            || data.poses == null || data.poses.Count == 0 || data.events == null || float.IsNaN(data.duration) || float.IsInfinity(data.duration) || data.duration <= 0) return false;
        long seq = 0; float time = -1;
        foreach (var e in data.events)
        {
            if (e == null || e.sequence != ++seq || !FiniteTime(e.recordingTime) || e.recordingTime < time || e.recordingTime > data.duration) return false;
            time = e.recordingTime;
        }
        time = -1; long boundary = 0;
        foreach (var frame in data.worldCheckpoints)
        {
            if (frame?.gameData == null || !FiniteTime(frame.frameTime) || frame.frameTime < time || frame.frameTime > data.duration
                || frame.lastEventSequence < boundary || frame.lastEventSequence > seq) return false;
            time = frame.frameTime; boundary = frame.lastEventSequence;
        }
        time = -1;
        foreach (var pose in data.poses)
        {
            if (pose == null || !FiniteTime(pose.recordingTime) || pose.recordingTime < time || pose.recordingTime > data.duration) return false;
            time = pose.recordingTime;
        }
        return data.worldCheckpoints[0].frameTime == 0f;
    }
    static bool FiniteTime(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;

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
        if (data.formatVersion == 3)
        {
            string[] currentIds = eventTargets.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (data.targetIds == null || !currentIds.SequenceEqual(data.targetIds.OrderBy(x => x, StringComparer.Ordinal)))
                throw new InvalidDataException("Recording object manifest does not match this scene.");
        }
        HashSet<string> sceneIds = new HashSet<string>();
        foreach (IReplayEventTarget target in Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(b => b != null && b.gameObject.scene.IsValid()).OfType<IReplayEventTarget>())
        {
            if (string.IsNullOrWhiteSpace(target.ReplayTargetId))
            {
                Debug.LogWarning($"Replay target '{target.ReplayTargetName}' has no stable ID; scene-path fallback will be used.", target as UnityEngine.Object);
            }
            else if (!sceneIds.Add(target.ReplayTargetId))
                throw new InvalidDataException("Duplicate replay target ID: " + target.ReplayTargetId);
        }
        foreach (ReplayEventData replayEvent in data.events.Where(e => e != null && !string.IsNullOrWhiteSpace(e.objectId)))
        {
            if (replayEvent.objectCategory == "Session") continue;
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
        data.events = data.events.OrderBy(e => e.recordingTime).ThenBy(e => e.sequence).ToList();
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
