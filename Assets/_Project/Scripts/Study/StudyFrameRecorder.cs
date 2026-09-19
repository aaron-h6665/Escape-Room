using UnityEngine;

[DefaultExecutionOrder(20000)]
public sealed class StudyFrameRecorder : MonoBehaviour
{
    void LateUpdate() => GetComponent<ReplayManager>()?.CaptureRenderedFrame();
}
