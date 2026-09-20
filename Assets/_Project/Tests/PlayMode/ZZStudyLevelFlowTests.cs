using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class ZZStudyLevelFlowTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    string directory;
    static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    static object Get(object o, string field) => o.GetType().GetField(field, Flags).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    static object Prop(object o, string name) => o.GetType().GetProperty(name).GetValue(o);
    static Component Find(string name) => (Component)UnityEngine.Object.FindAnyObjectByType(T(name));

    [UnityTest]
    public IEnumerator ActualLevel_CompleteRecordReplayAndTakeoverHaveMatchingWorldState()
    {
        directory = Path.Combine(Path.GetTempPath(), "escape-level-test-" + Guid.NewGuid().ToString("N"));
        yield return SceneManager.LoadSceneAsync("Level"); yield return null;
        var manager = Find("ReplayManager"); Set(manager, "storageRootOverride", directory);
        Call(manager, "StartRecording");
        var handoffs = new List<float>();
        var simon = Find("SimonSaysController");
        Call(simon, "StartPuzzle");
        yield return null; handoffs.Add((float)Prop(manager, "CurrentRecordingTime"));
        // Drive the real puzzle's public actions and resumable timer through all authored rounds.
        int rounds = ((Array)Get(simon, "fixedPattern")).Length;
        for (int round = 1; round <= rounds; round++)
        {
            int guard = 0;
            while (Prop(simon, "CurrentPhase").ToString() != "AwaitingInput" && guard++ < 100)
                Call(simon, "AdvanceTimeline", 0.1f);
            Assert.That(guard, Is.LessThan(100), "round=" + round + " phase=" + Prop(simon, "CurrentPhase") + " stage=" + Get(simon, "stage") + " input=" + Get(simon, "playerInputIndex"));
            var sequence = (IList)Get(simon, "activeSequence");
            for (int i = 0; i < sequence.Count; i++)
            {
                var button = Call(simon, "FindButton", sequence[i]);
                Call(simon, "HandleButtonInteraction", button);
                if (i < sequence.Count - 1) Call(simon, "AdvanceTimeline", (float)Get(simon, "buttonCueDuration") + 0.01f);
            }
            if (round < rounds) Call(simon, "AdvanceTimeline", 2.5f);
        }
        Call(simon, "AdvanceTimeline", 4f);
        Assert.That(Prop(simon, "IsSolved"), Is.True);
        yield return null;
        var player = Find("PlayerMotor");
        Call(player, "ToggleCrouch"); Call(player, "Jump");
        yield return null; handoffs.Add((float)Prop(manager, "CurrentRecordingTime"));
        var note = GameObject.Find("Note").GetComponent(T("NoteInteractable"));
        Call(note, "OpenNote", player.gameObject);
        yield return null; handoffs.Add((float)Prop(manager, "CurrentRecordingTime"));
        Call(note, "CloseNote", true);
        var decoder = Find("CaesarCipherInteractable"); Call(decoder, "OpenInspection", true);
        yield return null; handoffs.Add((float)Prop(manager, "CurrentRecordingTime"));
        Call(decoder, "CloseInspection", true);
        var terminal = Find("CaesarAnswerTerminal"); Call(terminal, "Open", player.gameObject, true);
        Call(terminal, "PressKey", "B"); Call(terminal, "PressKey", "L");
        yield return null; handoffs.Add((float)Prop(manager, "CurrentRecordingTime"));
        Call(terminal, "Close", true);
        var choice = Find("ColorKeyChoicePuzzle");
        Assert.That(Call(choice, "VerifyDecodedAnswer", "SILENT ORBIT"), Is.True);
        Assert.That(Prop(choice, "IsSolved"), Is.True);
        var keypad = Find("NumericKeypadPuzzle"); Call(keypad, "Press", "4"); Call(keypad, "Press", "2");
        yield return null;
        handoffs.Add((float)Prop(manager, "CurrentRecordingTime"));
        Call(manager, "CaptureRenderedFrame");
        yield return null;
        Call(keypad, "Press", "7"); Call(keypad, "Press", "1");
        Assert.That(Prop(keypad, "IsSolved"), Is.True);
        yield return null;
        var exit = Find("EscapeRoomExit"); Call(exit, "Complete", true);
        object original = Prop(manager, "ActiveRecording");
        string sourceId = (string)Get(original, "recordingId");
        Assert.That(File.Exists(Path.Combine(directory, "recordings", "normal", sourceId + ".json")), Is.True);
        string sourceBytes = File.ReadAllText(Path.Combine(directory, "recordings", "normal", sourceId + ".json"));
        Assert.That(T("ReplayManager").GetMethod("IsCompatibleRecording").Invoke(null, new[] { original }), Is.True);
        foreach (float handoff in handoffs)
        {
        // A fresh Level checks authored IDs, static objects, and persistent-manager scene rebinding.
        yield return SceneManager.LoadSceneAsync("Level"); yield return null;
        manager = Find("ReplayManager"); Set(manager, "playbackSelection", Enum.Parse(T("ReplayManager+ReplaySelectionMode"), "Manual"));
        Set(manager, "manualRecordingFileName", sourceId);
        Call(manager, "StartPlayback");
        Assert.That(Prop(manager, "CurrentState").ToString(), Is.EqualTo("Playback"));
        Set(manager, "recordingTime", handoff); Call(manager, "ApplyPlaybackFrame", handoff);
        object before = Activator.CreateInstance(T("GameData"));
        foreach (object obj in (IList)Get(manager, "replayObjects")) Call(obj, "SaveSnapshot", before);
        Call(manager, "TakeOver");
        object continuation = Prop(manager, "ActiveRecording");
        var initial = ((IList)Get(continuation, "worldCheckpoints"))[0];
        object inherited = Get(initial, "gameData");
        // Timing/room metadata are checked independently of object state.
        Set(before, "roomId", Get(inherited, "roomId"));
        Assert.That(JsonUtility.ToJson(inherited), Is.EqualTo(JsonUtility.ToJson(before)));
        if (handoff == handoffs[handoffs.Count - 1]) Assert.That(Prop(Find("NumericKeypadPuzzle"), "EnteredCode"), Is.EqualTo("42"));
        Assert.That(File.ReadAllText(Path.Combine(directory, "recordings", "normal", sourceId + ".json")), Is.EqualTo(sourceBytes));
        Call(manager, "Stop");
        }
        // Watching to the end retains an observation outcome even without a takeover.
        yield return SceneManager.LoadSceneAsync("Level"); yield return null;
        manager = Find("ReplayManager"); Call(manager, "StartPlayback");
        Set(manager, "recordingTime", (float)Get(original, "duration")); Call(manager, "PlaybackUpdate");
        Assert.That(Directory.GetFiles(Path.Combine(directory, "recordings", "observation"), "*.json").Length, Is.EqualTo(handoffs.Count + 1));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var name in new[] { "ReplayManager", "StudyMenuPanel" })
        {
            var obj = Find(name); if (obj != null) UnityEngine.Object.Destroy(obj.gameObject);
        }
        yield return SceneManager.LoadSceneAsync("MenuScene"); yield return null;
        Time.timeScale = 1f;
        if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
