using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class StudyReliabilityTests
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<GameObject> objects = new List<GameObject>();
    readonly List<string> folders = new List<string>();
    static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    static object New(string name) => Activator.CreateInstance(T(name));
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    static object Get(object target, string field) => target.GetType().GetField(field, Fields).GetValue(target);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Fields).Invoke(target, args);
    static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    Component Component(string name)
    {
        var obj = new GameObject(name); objects.Add(obj);
        return obj.AddComponent(T(name));
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var obj in objects) if (obj != null) UnityEngine.Object.Destroy(obj);
        objects.Clear(); Time.timeScale = 1f;
        yield return null;
        foreach (string folder in folders) if (Directory.Exists(folder)) Directory.Delete(folder, true);
        folders.Clear();
    }
    [Test]
    public void Simon_HandoffPreservesCueIndexAndRemainingTime()
    {
        var simon = Component("SimonSaysController");
        Call(simon, "StartPuzzle"); Call(simon, "AdvanceTimeline", 0.8f);
        object before = New("GameData"); Call(simon, "SaveSnapshot", before);
        string expected = JsonUtility.ToJson(before);
        Call(simon, "AdvanceTimeline", 0.2f); Call(simon, "LoadSnapshot", before);
        object after = New("GameData"); Call(simon, "SaveSnapshot", after);
        Assert.That(JsonUtility.ToJson(after), Is.EqualTo(expected));
        Call(simon, "AdvanceTimeline", 0.1f);
        Assert.That((float)Get(simon, "remaining"), Is.LessThan((float)((IList)Get(before, "simonSaysStates"))[0].GetType().GetField("stageRemaining").GetValue(((IList)Get(before, "simonSaysStates"))[0])));
    }
    [Test]
    public void SlidingDoor_RestoresMidAnimationAndDoesNotSnapOnRepeatedOpen()
    {
        var door = Component("PrototypeSlidingDoor");
        Call(door, "Open"); Call(door, "AdvanceReplayPresentation", 0.2f);
        float initial = (float)Get(door, "progress"); Assert.That(initial, Is.InRange(0.01f, 0.99f));
        Call(door, "SetOpen", true, false); Assert.That(Get(door, "progress"), Is.EqualTo(initial));
        object state = New("GameData"); Call(door, "SaveSnapshot", state);
        Call(door, "AdvanceReplayPresentation", 1f); Call(door, "LoadSnapshot", state);
        Assert.That(Get(door, "progress"), Is.EqualTo(initial));
    }
    [Test]
    public void Player_RestoresVelocityCrouchAndCameraHeight()
    {
        var obj = new GameObject("Player", typeof(CharacterController)); objects.Add(obj);
        var camera = new GameObject("Camera", typeof(Camera)); camera.transform.SetParent(obj.transform);
        var motor = obj.AddComponent(T("PlayerMotor"));
        Set(motor, "horizontalVelocity", new Vector3(2, 0, 1)); Set(motor, "playerVelocity", new Vector3(0, 3, 0));
        Call(motor, "ToggleCrouch"); camera.transform.localPosition = new Vector3(0, 1.1f, 0);
        object state = New("GameData"); Call(motor, "SaveSnapshot", state);
        Set(motor, "horizontalVelocity", Vector3.zero); Call(motor, "LoadSnapshot", state);
        Assert.That(Get(motor, "horizontalVelocity"), Is.EqualTo(new Vector3(2, 0, 1)));
        Assert.That(Get(motor, "playerVelocity"), Is.EqualTo(new Vector3(0, 3, 0)));
        Assert.That(Property(motor, "IsCrouching"), Is.True);
        Assert.That(camera.transform.localPosition.y, Is.EqualTo(1.1f).Within(0.00001f));
    }
    [Test]
    public void Keypad_RestoresDraftFailuresAndFeedbackTimer()
    {
        var keypad = Component("NumericKeypadPuzzle");
        Call(keypad, "Press", "2"); Call(keypad, "Press", "enter");
        object state = New("GameData"); Call(keypad, "SaveSnapshot", state);
        Call(keypad, "AdvanceReplayPresentation", 2f); Call(keypad, "LoadSnapshot", state);
        Assert.That(Get(keypad, "feedbackRemaining"), Is.EqualTo(0.65f));
        Assert.That(Get(keypad, "failedAttempts"), Is.EqualTo(1));
        Call(keypad, "Press", "4"); Call(keypad, "Press", "2");
        state = New("GameData"); Call(keypad, "SaveSnapshot", state);
        Call(keypad, "Press", "clear"); Call(keypad, "LoadSnapshot", state);
        Assert.That(Property(keypad, "EnteredCode"), Is.EqualTo("42"));
    }
    [Test]
    public void ModalControlOwners_ReleaseIndependently()
    {
        var input = Component("InputManager"); object modal = new object(), pause = new object();
        Call(input, "AcquireControl", modal); Call(input, "AcquireControl", pause); Call(input, "ReleaseControl", pause);
        Assert.That(Property(input, "PlayerControlLocked"), Is.True);
        Call(input, "ReleaseControl", modal); Assert.That(Property(input, "PlayerControlLocked"), Is.False);
    }
    [Test]
    public void Csv_ProtectsFormulasQuotesAndNewlines()
    {
        var method = T("StudyExports").GetMethod("Cell");
        Assert.That(method.Invoke(null, new object[] { "=SUM(A1)" }), Is.EqualTo("\"'=SUM(A1)\""));
        Assert.That(method.Invoke(null, new object[] { "a,\"b\"\nc" }), Is.EqualTo("\"a,\"\"b\"\"\nc\""));
    }
    [Test]
    public void Exports_MatchMilestonesAfterSequenceBoundaryAndUseSeconds()
    {
        string root = Path.Combine(Path.GetTempPath(), "study-tests-" + Guid.NewGuid().ToString("N")); folders.Add(root);
        object source = New("ReplayRecordingData"), takeover = New("ReplayRecordingData");
        Set(source, "recordingId", "source"); Set(source, "status", "completed"); Set(source, "duration", 20f);
        Set(takeover, "recordingKind", "takeover"); Set(takeover, "takeoverAtRecordingTime", 10f); Set(takeover, "takeoverAfterSequence", 1L); Set(takeover, "duration", 8f); Set(takeover, "status", "completed");
        AddEvent(source, 1, 10, "already_done"); AddEvent(source, 2, 16, "keypad_completed"); AddEvent(takeover, 1, 4, "keypad_completed");
        T("StudyExports").GetMethod("Write").Invoke(null, new[] { root, takeover, source });
        string folder = Path.Combine(root, "exports", "takeover", (string)Get(takeover, "attemptId"));
        string csv = File.ReadAllText(Path.Combine(folder, "comparison.csv"));
        Assert.That(csv, Does.Not.Contain("already_done"));
        Assert.That(csv, Does.Contain("\"6\",\"4\""));
        // Numeric columns must remain numeric, including negative differences.
        Assert.That(csv, Does.Contain("\"-2\""));
        Assert.That(File.ReadAllText(Path.Combine(folder, "session.csv")), Does.Contain("RemainingCompletionSeconds"));
    }
    static void AddEvent(object recording, long sequence, float seconds, string milestone)
    {
        object e = New("ReplayEventData"); Set(e, "sequence", sequence); Set(e, "recordingTime", seconds); Set(e, "milestoneId", milestone);
        Set(e, "eventKind", "keypad_solved"); Set(e, "objectCategory", "Keypad"); Set(e, "objectId", "keypad"); Set(e, "succeeded", true);
        ((IList)Get(recording, "events")).Add(e);
    }
    [Test]
    public void RecoveryFailure_RetriesWithoutDiscardingTheSession()
    {
        string root = Path.Combine(Path.GetTempPath(), "study-retry-" + Guid.NewGuid().ToString("N")); folders.Add(root);
        Directory.CreateDirectory(root);
        string blocker = Path.Combine(root, "blocked"); File.WriteAllText(blocker, "test fixture");
        var manager = Component("ReplayManager"); Set(manager, "storageRootOverride", blocker);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
        Call(manager, "StartRecording");
        Assert.That(Property(manager, "SavePending"), Is.True);
        object original = Property(manager, "ActiveRecording");
        Set(manager, "storageRootOverride", root);
        Assert.That(Call(manager, "RetrySave"), Is.True);
        Assert.That(Property(manager, "SavePending"), Is.False);
        Assert.That(Property(manager, "ActiveRecording"), Is.SameAs(original));
        Call(manager, "Stop");
        Assert.That(Get(original, "status"), Is.EqualTo("incomplete"));
        Assert.That(File.Exists(Path.Combine(root, "recordings", "normal", (string)Get(original, "recordingId") + ".json")), Is.True);
    }
    [Test]
    public void Compatibility_RejectsIncompleteAndDifferentLevel()
    {
        object data = New("ReplayRecordingData");
        Assert.That(T("ReplayManager").GetMethod("IsCompatibleRecording").Invoke(null, new[] { data }), Is.False);
        Set(data, "status", "completed"); Set(data, "levelVersion", "other-variant");
        Assert.That(T("ReplayManager").GetMethod("IsCompatibleRecording").Invoke(null, new[] { data }), Is.False);
    }
}
