using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CaesarCipherRuntimeSmokeTests
{
    const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<GameObject> createdObjects = new List<GameObject>();

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
    }

    [UnityTest]
    public IEnumerator ReplayOpenAndClose_AreIdempotentAndRestoreLayersAndColliders()
    {
        CreateMainCamera();
        Component cipher = CreateCipher(out Collider worldCollider, out Collider innerCollider, out Collider outerCollider);
        yield return null;

        object openEvent = CreateReplayEvent("caesar_inspection_opened");
        Assert.That(ApplyReplayEvent(cipher, openEvent), Is.True);
        Assert.That(ApplyReplayEvent(cipher, openEvent), Is.True, "A matching checkpoint/event pair must be idempotent.");
        Assert.That(GetProperty<bool>(cipher, "IsInspecting"), Is.True);
        Assert.That(worldCollider.enabled, Is.False);
        Assert.That(innerCollider.enabled, Is.True);
        Assert.That(outerCollider.enabled, Is.False, "Only the inner ring is turnable.");
        Assert.That(innerCollider.gameObject.layer, Is.EqualTo(8));
        Assert.That(outerCollider.gameObject.layer, Is.EqualTo(8));
        Camera overlayCamera = GetField<Camera>(cipher, "inspectionCamera");
        Assert.That(overlayCamera, Is.Not.Null);
        Assert.That(overlayCamera.orthographic, Is.True);
        Assert.That(overlayCamera.clearFlags, Is.EqualTo(CameraClearFlags.Depth));
        Assert.That(overlayCamera.cullingMask, Is.EqualTo(1 << 8));

        object closeEvent = CreateReplayEvent("caesar_inspection_closed");
        Assert.That(ApplyReplayEvent(cipher, closeEvent), Is.True);
        Assert.That(ApplyReplayEvent(cipher, closeEvent), Is.True);
        Assert.That(GetProperty<bool>(cipher, "IsInspecting"), Is.False);
        Assert.That(worldCollider.enabled, Is.True);
        Assert.That(innerCollider.enabled, Is.False);
        Assert.That(outerCollider.enabled, Is.False);
        Assert.That(innerCollider.gameObject.layer, Is.EqualTo(0));
        Assert.That(outerCollider.gameObject.layer, Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator ReplayRotation_OnlyTurnsInnerRingAndBothRingsBeginAtA()
    {
        Component cipher = CreateCipher(out _, out _, out _);
        yield return null;

        Assert.That(GetProperty<char>(cipher, "OuterTopSymbol"), Is.EqualTo('A'));
        Assert.That(GetProperty<char>(cipher, "InnerTopSymbol"), Is.EqualTo('A'));

        object rotateOuter = CreateReplayEvent("caesar_ring_rotated", "outer", 26f);
        object selectOuter = CreateReplayEvent("caesar_ring_selected", "outer");
        object rotateInner = CreateReplayEvent("caesar_ring_rotated", "inner", -1f);
        Assert.That(ApplyReplayEvent(cipher, rotateOuter), Is.True);
        Assert.That(ApplyReplayEvent(cipher, selectOuter), Is.True);
        Assert.That(ApplyReplayEvent(cipher, rotateInner), Is.True);

        Assert.That(GetProperty<int>(cipher, "OuterIndex"), Is.EqualTo(14));
        Assert.That(GetProperty<int>(cipher, "InnerIndex"), Is.EqualTo(26));
        Assert.That(GetProperty(cipher, "SelectedRing").ToString(), Is.EqualTo("Inner"));
        Assert.That(GetProperty<char>(cipher, "OuterTopSymbol"), Is.EqualTo('A'));
        Assert.That(GetProperty<char>(cipher, "InnerTopSymbol"), Is.EqualTo('N'));
    }

    [UnityTest]
    public IEnumerator NormalPersistence_RestoresWheelButNeverModalAndLeavesDoorsUntouched()
    {
        Component cipher = CreateCipher(out _, out _, out _);
        yield return null;

        object gameData = Activator.CreateInstance(RuntimeType("GameData"));
        object cipherState = Activator.CreateInstance(RuntimeType("CaesarCipherSaveData"));
        SetField(cipherState, "id", "test-cipher");
        SetField(cipherState, "innerIndex", -1);
        SetField(cipherState, "outerIndex", 27);
        SetField(cipherState, "selectedRing", 0);
        SetField(cipherState, "isInspecting", true);
        GetList(gameData, "caesarCipherStates").Add(cipherState);

        object doorState = Activator.CreateInstance(RuntimeType("DoorSaveData"));
        SetField(doorState, "id", "untouched-door");
        SetField(doorState, "isOpen", true);
        GetList(gameData, "doorStates").Add(doorState);

        Invoke(cipher, "LoadData", gameData);
        Assert.That(GetProperty<int>(cipher, "InnerIndex"), Is.EqualTo(26));
        Assert.That(GetProperty<int>(cipher, "OuterIndex"), Is.EqualTo(14));
        Assert.That(GetProperty<bool>(cipher, "IsInspecting"), Is.False);

        object[] saveArguments = { gameData };
        cipher.GetType().GetMethod("SaveData").Invoke(cipher, saveArguments);
        gameData = saveArguments[0];
        Assert.That(GetList(gameData, "doorStates").Count, Is.EqualTo(1));
        Assert.That(GetField<bool>(GetList(gameData, "doorStates")[0], "isOpen"), Is.True);
        Assert.That(GetField<bool>(GetList(gameData, "caesarCipherStates")[0], "isInspecting"), Is.False);
    }

    [UnityTest]
    public IEnumerator NoteDiscovery_SurvivesNormalSaveWithoutReopeningTheModal()
    {
        GameObject noteObject = Track(new GameObject("Test Note"));
        GameObject noteCanvas = new GameObject("Note Canvas");
        noteCanvas.transform.SetParent(noteObject.transform, false);
        Component note = noteObject.AddComponent(RuntimeType("NoteInteractable"));
        SetField(note, "id", "test-note");
        SetField(note, "noteCanvas", noteCanvas);

        GameObject interactor = Track(new GameObject("Test Interactor"));
        note.GetType().GetMethod("BaseInteract").Invoke(note, new object[] { interactor });
        yield return null;
        Assert.That(GetProperty<bool>(note, "HasBeenRead"), Is.True);
        Assert.That(noteCanvas.activeSelf, Is.True);

        object gameData = Activator.CreateInstance(RuntimeType("GameData"));
        object[] saveArguments = { gameData };
        note.GetType().GetMethod("SaveData").Invoke(note, saveArguments);
        gameData = saveArguments[0];
        Assert.That(GetField<bool>(GetList(gameData, "noteStates")[0], "hasBeenRead"), Is.True);
        Assert.That(GetField<bool>(GetList(gameData, "noteStates")[0], "isOpen"), Is.False);

        Invoke(note, "LoadData", gameData);
        Assert.That(GetProperty<bool>(note, "HasBeenRead"), Is.True);
        Assert.That(noteCanvas.activeSelf, Is.False, "Normal saves preserve discovery but do not restore modal UI.");
    }

    Component CreateCipher(out Collider worldCollider, out Collider innerCollider, out Collider outerCollider)
    {
        GameObject root = Track(new GameObject("Test Caesar Cipher"));
        root.SetActive(false);
        root.layer = 6;

        Transform wheelCenter = CreateChild("Wheel Center", root.transform).transform;
        Transform innerRing = CreateChild("Inner Ring", root.transform).transform;
        Transform outerRing = CreateChild("Outer Ring", root.transform).transform;
        worldCollider = root.AddComponent<BoxCollider>();
        innerCollider = innerRing.gameObject.AddComponent<BoxCollider>();
        outerCollider = outerRing.gameObject.AddComponent<BoxCollider>();
        innerCollider.enabled = false;
        outerCollider.enabled = false;

        Component cipher = root.AddComponent(RuntimeType("CaesarCipherInteractable"));
        SetField(cipher, "id", "test-cipher");
        SetField(cipher, "wheelCenter", wheelCenter);
        SetField(cipher, "innerRing", innerRing);
        SetField(cipher, "outerRing", outerRing);
        SetField(cipher, "worldInteractionCollider", worldCollider);
        SetField(cipher, "innerRingCollider", innerCollider);
        SetField(cipher, "outerRingCollider", outerCollider);
        SetField(cipher, "inspectionRenderers", Array.Empty<Renderer>());
        root.SetActive(true);
        return cipher;
    }

    void CreateMainCamera()
    {
        GameObject cameraObject = Track(new GameObject("Test Main Camera"));
        cameraObject.tag = "MainCamera";
        cameraObject.AddComponent<Camera>();
    }

    GameObject CreateChild(string name, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    GameObject Track(GameObject gameObject)
    {
        createdObjects.Add(gameObject);
        return gameObject;
    }

    static object CreateReplayEvent(string eventKind, string textValue = "", float numberValue = 0f)
    {
        object replayEvent = Activator.CreateInstance(RuntimeType("ReplayEventData"));
        SetField(replayEvent, "eventKind", eventKind);
        SetField(replayEvent, "textValue", textValue);
        SetField(replayEvent, "numberValue", numberValue);
        return replayEvent;
    }

    static bool ApplyReplayEvent(Component target, object replayEvent)
    {
        return (bool)target.GetType().GetMethod("ApplyReplayEvent").Invoke(target, new[] { replayEvent });
    }

    static void Invoke(object target, string methodName, object argument)
    {
        target.GetType().GetMethod(methodName).Invoke(target, new[] { argument });
    }

    static IList GetList(object target, string fieldName)
    {
        return (IList)target.GetType().GetField(fieldName, InstanceFields).GetValue(target);
    }

    static T GetField<T>(object target, string fieldName)
    {
        return (T)target.GetType().GetField(fieldName, InstanceFields).GetValue(target);
    }

    static object GetProperty(object target, string propertyName)
    {
        return target.GetType().GetProperty(propertyName, InstanceFields).GetValue(target);
    }

    static T GetProperty<T>(object target, string propertyName)
    {
        return (T)GetProperty(target, propertyName);
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
