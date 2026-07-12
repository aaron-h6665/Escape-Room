using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SnapshotData
{
    public float frameTime;

    public GameData gameData;

    public SnapshotData()
    {
        frameTime = 0f;
        gameData = new GameData();
    }

    public SnapshotData(float time)
    {
        frameTime = time;
        gameData = new GameData();
    }
}

[System.Serializable]
public class ReplayFileData
{
    public float snapshotDelta;
    public List<SnapshotData> snapshots;

    public ReplayFileData()
    {
        snapshotDelta = 0f;
        snapshots = new List<SnapshotData>();
    }

    public ReplayFileData(float snapshotDelta, List<SnapshotData> snapshots)
    {
        this.snapshotDelta = snapshotDelta;
        this.snapshots = snapshots ?? new List<SnapshotData>();
    }
}
