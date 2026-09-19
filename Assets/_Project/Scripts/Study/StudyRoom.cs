using UnityEngine;

public sealed class StudyRoom : MonoBehaviour, IReplayEventTarget
{
    public string roomId;
    public Bounds worldBounds;
    public string ReplayTargetId => roomId;
    public string ReplayTargetName => gameObject.name;
    public string ReplayTargetCategory => "Room";
    public ReplayObjectState ReplayState => ReplayObjectState.Idle;
    public bool ApplyReplayEvent(ReplayEventData value) => true;
    void LateUpdate()
    {
        var manager = ReplayManager.instance;
        if (manager == null) return;
        var player = FindAnyObjectByType<PlayerMotor>();
        if (player == null || !worldBounds.Contains(player.transform.position) || manager.CurrentRoomId == roomId) return;
        manager.CurrentRoomId = roomId;
        ReplayEventBus.Publish(this, "room_entered", ReplayObjectState.Activated, true, false);
    }
}
