using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Replay Container")]
public class ReplayContainer : ScriptableObject
{
    [SerializeField]
    private List<SnapshotData> m_snapshots = new List<SnapshotData>();

    public int Count
    {
        get
        {
            EnsureSnapshots();
            return m_snapshots.Count;
        }
    }

    public List<SnapshotData> Snapshots
    {
        get
        {
            EnsureSnapshots();
            return m_snapshots;
        }
    }

    public void Init()
    {
        m_snapshots = new List<SnapshotData>();
    }

    public void AddSnapshot(SnapshotData snapshot)
    {
        EnsureSnapshots();
        m_snapshots.Add(snapshot);
    }

    public void SetSnapshots(List<SnapshotData> snapshots)
    {
        m_snapshots = snapshots ?? new List<SnapshotData>();
    }

    public bool GetSnapshot(int index, out SnapshotData data)
    {
        EnsureSnapshots();

        if (index >= m_snapshots.Count)
        {
            data = new SnapshotData(-1);
            return false;
        }

        if (index < 0) {
            data = new SnapshotData(-1);
            return false;
        }

        data = m_snapshots[index];

        return true;
    }

    void EnsureSnapshots()
    {
        if (m_snapshots == null)
        {
            m_snapshots = new List<SnapshotData>();
        }
    }
}
