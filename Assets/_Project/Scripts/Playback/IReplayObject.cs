using UnityEngine;

public interface IReplayObject
{
    void SaveSnapshot(ref GameData data);
    void LoadSnapshot(GameData data);
}
