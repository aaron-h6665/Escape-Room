using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ReplayManager : MonoBehaviour
{
    public static ReplayManager instance;
    public const string DefaultReplayFileName = "replay.json";

    public enum State
    {
        Idle,
        Record,
        Playback
    }

    public enum LaunchMode
    {
        None,
        Record,
        Playback
    }

    public static LaunchMode QueuedLaunchMode { get; private set; }
    public static LaunchMode CurrentLaunchMode { get; private set; }

    [Header("Replay Storage")]
    public ReplayContainer replayContainer;
    [SerializeField] string replayFileName = DefaultReplayFileName;
    [SerializeField] bool saveReplayOnStop = true;
    [SerializeField] bool loadReplayFileOnPlayback = true;

    [Header("State")]
    [SerializeField] State currentState;

    [System.NonSerialized]
    public List<IReplayObject> replayObjects = new List<IReplayObject>();

    public State CurrentState => currentState;

    [Tooltip("What should the frame delta be between snapshots?")]
    public float snapshotDelta = 0.1f;

    float m_time;
    private float m_snapshotDeltaTotal;
    private int snapshotIndex = 0;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("Found more than one ReplayManager in the scene.");
            enabled = false;
            return;
        }

        instance = this;
        EnsureReplayContainer();
    }

    private void Start()
    {
        RefreshReplayObjects();
        StartQueuedLaunchMode();
    }

    private void OnApplicationQuit()
    {
        if (currentState == State.Record)
        {
            Stop();
        }
    }

    public void StartRecording()
    {
        CurrentLaunchMode = LaunchMode.Record;
        EnsureReplayContainer();
        replayContainer.Init();
        RefreshReplayObjects();

        m_time = 0f;
        m_snapshotDeltaTotal = 0f;
        snapshotIndex = 0;
        currentState = State.Record;
        TakeSnapshot();
    }

    public void StartPlayback()
    {
        CurrentLaunchMode = LaunchMode.Playback;
        EnsureReplayContainer();
        if (replayContainer.Count == 0 && loadReplayFileOnPlayback)
        {
            LoadReplay();
        }

        if (replayContainer.Count == 0)
        {
            Debug.LogWarning("Playback was requested, but no replay snapshots are available.", this);
            currentState = State.Idle;
            return;
        }

        RefreshReplayObjects();
        m_time = 0f;
        snapshotIndex = 0;
        currentState = State.Playback;
        ApplySnapshotAtIndex(snapshotIndex);
        snapshotIndex++;
    }

    public void Stop()
    {
        State previousState = currentState;
        currentState = State.Idle;
        m_snapshotDeltaTotal = 0f;

        if (previousState == State.Record && saveReplayOnStop)
        {
            SaveReplay();
        }
    }

    public void StopRecording()
    {
        if (currentState == State.Record)
        {
            Stop();
        }
    }

    public void Register(IReplayObject replayObject)
    {
        if (replayObject == null)
        {
            return;
        }

        if (replayObjects == null)
        {
            replayObjects = new List<IReplayObject>();
        }

        if (!replayObjects.Contains(replayObject))
        {
            replayObjects.Add(replayObject);
        }
    }

    public void FixedUpdate()
    {
        if (currentState == State.Record)
        {
            m_snapshotDeltaTotal += Time.fixedDeltaTime;
            m_time += Time.fixedDeltaTime;

            float resolvedSnapshotDelta = Mathf.Max(0.01f, snapshotDelta);
            while (m_snapshotDeltaTotal >= resolvedSnapshotDelta)
            {
                TakeSnapshot();

                m_snapshotDeltaTotal -= resolvedSnapshotDelta;
            }

            return;
        }

        if (currentState == State.Playback)
        {
            PlaybackFixedUpdate();
        }
    }

    private void TakeSnapshot()
    {
        RefreshReplayObjects();
        SnapshotData snapshotData = new SnapshotData(m_time);

        foreach (IReplayObject replayObject in replayObjects)
        {
            replayObject.SaveSnapshot(ref snapshotData.gameData);
        }

        replayContainer.AddSnapshot(snapshotData);

        snapshotIndex++;
    }

    void PlaybackFixedUpdate()
    {
        m_time += Time.fixedDeltaTime;

        while (snapshotIndex < replayContainer.Count)
        {
            if (!replayContainer.GetSnapshot(snapshotIndex, out SnapshotData snapshotData))
            {
                Stop();
                return;
            }

            if (snapshotData.frameTime > m_time)
            {
                return;
            }

            ApplySnapshot(snapshotData);
            snapshotIndex++;
        }

        Stop();
    }

    bool ApplySnapshotAtIndex(int index)
    {
        if (!replayContainer.GetSnapshot(index, out SnapshotData snapshotData))
        {
            return false;
        }

        ApplySnapshot(snapshotData);
        return true;
    }

    void ApplySnapshot(SnapshotData snapshotData)
    {
        RefreshReplayObjects();

        foreach (IReplayObject replayObject in replayObjects)
        {
            replayObject.LoadSnapshot(snapshotData.gameData);
        }
    }

    public void SaveReplay()
    {
        EnsureReplayContainer();

        string fullPath = SaveFileUtility.GetPath(ResolveReplayFileName());
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            ReplayFileData replayFileData = new ReplayFileData(snapshotDelta, replayContainer.Snapshots);
            string dataToStore = JsonUtility.ToJson(replayFileData, true);
            File.WriteAllText(fullPath, dataToStore);
            Debug.Log("Replay saved to " + fullPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Replay save error " + fullPath + "\n" + e);
        }
    }

    public bool LoadReplay()
    {
        EnsureReplayContainer();

        string fullPath = SaveFileUtility.GetPath(ResolveReplayFileName());
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning("Replay file was not found at " + fullPath);
            return false;
        }

        try
        {
            string dataToLoad = File.ReadAllText(fullPath);
            ReplayFileData replayFileData = JsonUtility.FromJson<ReplayFileData>(dataToLoad);
            if (replayFileData == null || replayFileData.snapshots == null)
            {
                Debug.LogWarning("Replay file did not contain readable snapshot data: " + fullPath);
                return false;
            }

            snapshotDelta = replayFileData.snapshotDelta > 0f ? replayFileData.snapshotDelta : snapshotDelta;
            replayContainer.SetSnapshots(replayFileData.snapshots);
            Debug.Log("Replay loaded from " + fullPath);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Replay load error " + fullPath + "\n" + e);
            return false;
        }
    }

    void RefreshReplayObjects()
    {
        if (replayObjects == null)
        {
            replayObjects = new List<IReplayObject>();
        }

        replayObjects.RemoveAll(replayObject => replayObject == null || (replayObject as Object) == null);

        IEnumerable<IReplayObject> sceneReplayObjects = Resources
            .FindObjectsOfTypeAll<MonoBehaviour>()
            .Where(behaviour => behaviour != null && behaviour.gameObject.scene.IsValid())
            .OfType<IReplayObject>();

        foreach (IReplayObject replayObject in sceneReplayObjects)
        {
            Register(replayObject);
        }
    }

    void EnsureReplayContainer()
    {
        if (replayContainer == null)
        {
            replayContainer = ScriptableObject.CreateInstance<ReplayContainer>();
        }
    }

    void StartQueuedLaunchMode()
    {
        if (QueuedLaunchMode == LaunchMode.None)
        {
            return;
        }

        LaunchMode launchMode = QueuedLaunchMode;
        QueuedLaunchMode = LaunchMode.None;

        if (launchMode == LaunchMode.Record)
        {
            StartRecording();
            return;
        }

        if (launchMode == LaunchMode.Playback)
        {
            StartPlayback();
        }
    }

    string ResolveReplayFileName()
    {
        return string.IsNullOrWhiteSpace(replayFileName) ? DefaultReplayFileName : replayFileName;
    }

    public static void QueueRecordingOnNextScene()
    {
        QueuedLaunchMode = LaunchMode.Record;
    }

    public static void QueuePlaybackOnNextScene()
    {
        QueuedLaunchMode = LaunchMode.Playback;
    }

    public static bool IsPlaybackActive()
    {
        return instance != null && instance.CurrentState == State.Playback;
    }

    public static bool IsRecordingActive()
    {
        return instance != null && instance.CurrentState == State.Record;
    }

    public static bool IsPlaybackLaunch()
    {
        return QueuedLaunchMode == LaunchMode.Playback || CurrentLaunchMode == LaunchMode.Playback;
    }

    public static bool IsRecordingLaunch()
    {
        return QueuedLaunchMode == LaunchMode.Record || CurrentLaunchMode == LaunchMode.Record;
    }
}
