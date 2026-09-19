using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ReplayFlowRuntimeTests
{
    const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<GameObject> createdObjects = new List<GameObject>();
    readonly List<string> temporaryDirectories = new List<string>();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null)
            {
                UnityEngine.Object.Destroy(createdObjects[index]);
            }
        }
        createdObjects.Clear();
        yield return null;

        foreach (string directory in temporaryDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        temporaryDirectories.Clear();
    }

    [UnityTest]
    public IEnumerator MissingManualRecording_FallsBackToAValidNormalRecording()
    {
        string storageRoot = CreateTemporaryStorageRoot();
        string normalDirectory = Path.Combine(storageRoot, "recordings", "normal");
        Directory.CreateDirectory(normalDirectory);
        string availablePath = Path.Combine(normalDirectory, "available.json");
        object recording = Activator.CreateInstance(RuntimeType("ReplayRecordingData"));
        SetField(recording, "recordingId", "available");
        File.WriteAllText(availablePath, JsonUtility.ToJson(recording, true));

        Component manager = CreateReplayManager();
        SetField(manager, "storageRootOverride", storageRoot);
        SetField(manager, "playbackSelection", Enum.Parse(RuntimeType("ReplayManager+ReplaySelectionMode"), "Manual"));
        SetField(manager, "manualRecordingFileName", "missing-recording.json");

        string resolvedPath = (string)Invoke(manager, "ResolvePlaybackPath");

        Assert.That(resolvedPath, Is.EqualTo(availablePath));
        yield return null;
    }

    [UnityTest]
    public IEnumerator Takeover_ChangesPlaybackIntoALinkedTakeoverRecording()
    {
        Component manager = CreateReplayManager();
        object sourceRecording = Activator.CreateInstance(RuntimeType("ReplayRecordingData"));
        SetField(sourceRecording, "recordingId", "source-recording");
        SetField(manager, "sourceRecording", sourceRecording);
        SetField(manager, "recordingTime", 12.5f);
        SetField(manager, "currentState", Enum.Parse(RuntimeType("ReplayManager+State"), "Playback"));

        manager.GetType().GetMethod("TakeOver").Invoke(manager, null);

        Assert.That(GetProperty(manager, "CurrentState").ToString(), Is.EqualTo("Takeover"));
        object activeRecording = GetProperty(manager, "ActiveRecording");
        Assert.That(GetField<string>(activeRecording, "recordingKind"), Is.EqualTo("takeover"));
        Assert.That(GetField<string>(activeRecording, "sourceRecordingId"), Is.EqualTo("source-recording"));
        Assert.That(GetField<float>(activeRecording, "takeoverAtRecordingTime"), Is.EqualTo(12.5f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlaybackInteractionPrompt_FollowsTheRecordedCameraWithoutInteracting()
    {
        Component manager = CreateReplayManager();
        SetField(manager, "currentState", Enum.Parse(RuntimeType("ReplayManager+State"), "Playback"));

        GameObject player = Track(new GameObject("Replay UI Player"));
        Component playerLook = player.AddComponent(RuntimeType("PlayerLook"));
        Camera camera = Track(new GameObject("Replay UI Camera")).AddComponent<Camera>();
        camera.transform.SetParent(player.transform, false);
        SetField(playerLook, "cam", camera);
        Component playerUI = player.AddComponent(RuntimeType("PlayerUI"));
        player.AddComponent(RuntimeType("InputManager"));
        Component playerInteract = player.AddComponent(RuntimeType("PlayerInteract"));
        SetField(playerInteract, "mask", (LayerMask)(1 << 6));
        SetField(playerInteract, "blockerMask", (LayerMask)0);

        GameObject noteObject = Track(new GameObject("Replay Prompt Note"));
        noteObject.layer = 6;
        noteObject.transform.position = new Vector3(0f, 0f, 2f);
        noteObject.AddComponent<BoxCollider>();
        Component note = noteObject.AddComponent(RuntimeType("NoteInteractable"));

        Physics.SyncTransforms();
        yield return null;
        yield return null;

        Assert.That(GetProperty(playerUI, "CurrentPromptMessage"), Is.EqualTo("Press E to Read Note."));
        Assert.That(GetProperty(note, "HasBeenRead"), Is.EqualTo(false), "Playback may present the prompt but must not invoke the interaction.");
    }

    [UnityTest]
    public IEnumerator PlaybackPromptSnapshot_IsNotBlanked()
    {
        Component manager = CreateReplayManager();
        SetField(manager, "currentState", Enum.Parse(RuntimeType("ReplayManager+State"), "Playback"));
        Component playerUI = Track(new GameObject("Replay Prompt UI")).AddComponent(RuntimeType("PlayerUI"));
        object gameData = Activator.CreateInstance(RuntimeType("GameData"));
        SetField(gameData, "playerPromptText", "Door locked - solve Simon Says");

        Invoke(playerUI, "LoadSnapshot", gameData);

        Assert.That(GetProperty(playerUI, "CurrentPromptMessage"), Is.EqualTo("Door locked - solve Simon Says"));
        yield return null;
    }

    [UnityTest]
    public IEnumerator DecodedPhraseTerminal_ReplaysItsModalAndTypedText()
    {
        GameObject playerObject = Track(new GameObject("Replay Caesar Player"));
        playerObject.AddComponent(RuntimeType("InputManager"));
        Component crosshair = playerObject.AddComponent(RuntimeType("PlayerCrosshair"));
        GameObject terminalObject = Track(new GameObject("Replay Caesar Terminal"));
        GameObject entryCanvas = new GameObject("Replay Caesar Entry Canvas");
        entryCanvas.transform.SetParent(terminalObject.transform, false);
        entryCanvas.SetActive(false);
        Component terminal = terminalObject.AddComponent(RuntimeType("CaesarAnswerTerminal"));
        SetField(terminal, "entryCanvas", entryCanvas);

        object openEvent = CreateReplayEvent("caesar_answer_entry_opened", true, string.Empty, "Open");
        Assert.That(ApplyReplayEvent(terminal, openEvent), Is.True);
        Assert.That(GetProperty(terminal, "IsOpen"), Is.EqualTo(true));
        Assert.That(entryCanvas.activeSelf, Is.True);
        Assert.That(GetProperty(crosshair, "IsVisible"), Is.EqualTo(false), "The modal should hide the gameplay reticle just as it does in normal play.");

        object textEvent = CreateReplayEvent("caesar_answer_entry_changed", true, "BLUE", "Activated");
        Assert.That(ApplyReplayEvent(terminal, textEvent), Is.True);
        Assert.That(GetProperty(terminal, "EnteredText"), Is.EqualTo("BLUE"));

        object closeEvent = CreateReplayEvent("caesar_answer_entry_closed", true, string.Empty, "Closed");
        Assert.That(ApplyReplayEvent(terminal, closeEvent), Is.True);
        Assert.That(GetProperty(terminal, "IsOpen"), Is.EqualTo(false));
        Assert.That(entryCanvas.activeSelf, Is.False);
        Assert.That(GetProperty(crosshair, "IsVisible"), Is.EqualTo(true));
        yield return null;
    }

    Component CreateReplayManager()
    {
        GameObject managerObject = new GameObject("Test Replay Manager");
        createdObjects.Add(managerObject);
        return managerObject.AddComponent(RuntimeType("ReplayManager"));
    }

    GameObject Track(GameObject gameObject)
    {
        createdObjects.Add(gameObject);
        return gameObject;
    }

    string CreateTemporaryStorageRoot()
    {
        string directory = Path.Combine(Application.temporaryCachePath, "replay-flow-tests", Guid.NewGuid().ToString("N"));
        temporaryDirectories.Add(directory);
        return directory;
    }

    static object Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFields);
        Assert.That(method, Is.Not.Null, $"Missing method {target.GetType().Name}.{methodName}");
        return method.Invoke(target, null);
    }

    static object Invoke(object target, string methodName, object argument)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFields);
        Assert.That(method, Is.Not.Null, $"Missing method {target.GetType().Name}.{methodName}");
        return method.Invoke(target, new[] { argument });
    }

    static object CreateReplayEvent(string eventKind, bool succeeded, string textValue, string state)
    {
        object replayEvent = Activator.CreateInstance(RuntimeType("ReplayEventData"));
        SetField(replayEvent, "eventKind", eventKind);
        SetField(replayEvent, "succeeded", succeeded);
        SetField(replayEvent, "textValue", textValue);
        SetField(replayEvent, "state", Enum.Parse(RuntimeType("ReplayObjectState"), state));
        return replayEvent;
    }

    static bool ApplyReplayEvent(Component target, object replayEvent)
    {
        return (bool)target.GetType().GetMethod("ApplyReplayEvent").Invoke(target, new[] { replayEvent });
    }

    static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFields);
        Assert.That(property, Is.Not.Null, $"Missing property {target.GetType().Name}.{propertyName}");
        return property.GetValue(target);
    }

    static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFields);
        Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{fieldName}");
        return (T)field.GetValue(target);
    }

    static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFields);
        Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{fieldName}");
        field.SetValue(target, value);
    }

    static Type RuntimeType(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Could not find runtime type '{typeName}'.");
        return type;
    }
}
