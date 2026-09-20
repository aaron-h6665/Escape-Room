using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EscapeRoomPrototypeRuntimeTests
{
    const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

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
    public IEnumerator CaesarPhrase_RejectsWrongAnswerAndUnlocksDoorDirectly()
    {
        Component door = CreateDoor("Room 2 Door");
        GameObject puzzleObject = Track(new GameObject("Caesar Phrase"));
        Component puzzle = puzzleObject.AddComponent(RuntimeType("ColorKeyChoicePuzzle"));
        SetField(puzzle, "id", "test-caesar-phrase");
        SetField(puzzle, "exitDoor", door);
        Component simon = Track(new GameObject("Clue Simon")).AddComponent(RuntimeType("SimonSaysController"));
        Component note = Track(new GameObject("Clue Note")).AddComponent(RuntimeType("NoteInteractable"));
        SetField(puzzle, "clueSimon", simon);
        SetField(puzzle, "clueNote", note);
        yield return null;

        bool wrongPhrase = (bool)puzzle.GetType().GetMethod("VerifyDecodedAnswer").Invoke(puzzle, new object[] { "OPEN THE DOOR" });
        Assert.That(wrongPhrase, Is.False);
        Assert.That(GetProperty<bool>(puzzle, "AnswerVerified"), Is.False);
        Assert.That(GetProperty<bool>(puzzle, "IsSolved"), Is.False);
        Assert.That(GetProperty<bool>(door, "IsOpen"), Is.False);

        bool correctPhrase = (bool)puzzle.GetType().GetMethod("VerifyDecodedAnswer").Invoke(puzzle, new object[] { "silent orbit" });
        Assert.That(correctPhrase, Is.True);
        Assert.That(GetProperty<bool>(puzzle, "AnswerVerified"), Is.True);
        Assert.That(GetProperty<bool>(puzzle, "IsSolved"), Is.True);
        Assert.That(GetProperty<bool>(door, "IsOpen"), Is.True);

        bool repeatedPhrase = (bool)puzzle.GetType().GetMethod("VerifyDecodedAnswer").Invoke(puzzle, new object[] { "SILENT ORBIT" });
        Assert.That(repeatedPhrase, Is.True, "A repeated correct phrase must remain idempotently solved.");
    }

    [UnityTest]
    public IEnumerator Keypad_RequiresExactCodeAndPersistsSolvedState()
    {
        Component door = CreateDoor("Final Door");
        GameObject keypadObject = Track(new GameObject("Numeric Keypad"));
        Component keypad = keypadObject.AddComponent(RuntimeType("NumericKeypadPuzzle"));
        SetField(keypad, "id", "test-keypad");
        SetField(keypad, "correctCode", "4271");
        SetField(keypad, "codeLength", 4);
        SetField(keypad, "finalDoor", door);
        yield return null;

        foreach (string value in new[] { "4", "2", "7", "0", "enter" })
        {
            Invoke(keypad, "Press", value);
        }
        Assert.That(GetProperty<bool>(keypad, "IsSolved"), Is.False);
        Assert.That(GetProperty<string>(keypad, "EnteredCode"), Is.Empty);
        Assert.That(GetField<int>(keypad, "failedAttempts"), Is.EqualTo(1));
        Assert.That(GetProperty<bool>(door, "IsOpen"), Is.False);

        foreach (string value in new[] { "4", "2", "7", "1", "enter" })
        {
            Invoke(keypad, "Press", value);
        }
        Assert.That(GetProperty<bool>(keypad, "IsSolved"), Is.True);
        Assert.That(GetProperty<bool>(door, "IsOpen"), Is.True);

        object gameData = Activator.CreateInstance(RuntimeType("GameData"));
        object[] saveArguments = { gameData };
        keypad.GetType().GetMethod("SaveSnapshot").Invoke(keypad, saveArguments);
        gameData = saveArguments[0];
        IList keypadStates = GetList(gameData, "keypadStates");
        Assert.That(keypadStates.Count, Is.EqualTo(1));
        Assert.That(GetField<bool>(keypadStates[0], "isOpen"), Is.True);
        Assert.That(GetField<string>(keypadStates[0], "enteredCode"), Is.EqualTo("4271"));
        Assert.That(GetField<int>(keypadStates[0], "failedAttempts"), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator Keypad_AutoSubmitsFourDigitsAndClearsAnIncorrectCode()
    {
        Component door = CreateDoor("Auto Submit Door");
        GameObject keypadObject = Track(new GameObject("Auto Submit Numeric Keypad"));
        Component keypad = keypadObject.AddComponent(RuntimeType("NumericKeypadPuzzle"));
        SetField(keypad, "id", "test-auto-submit-keypad");
        SetField(keypad, "correctCode", "4271");
        SetField(keypad, "codeLength", 4);
        SetField(keypad, "autoSubmitOnCodeLength", true);
        SetField(keypad, "finalDoor", door);
        yield return null;

        Invoke(keypad, "Press", "4");
        Invoke(keypad, "Press", "2");
        Invoke(keypad, "Press", "delete");
        Assert.That(GetProperty<string>(keypad, "EnteredCode"), Is.EqualTo("4"));
        Invoke(keypad, "Press", "clear");

        foreach (string value in new[] { "4", "2", "7", "0" }) Invoke(keypad, "Press", value);
        Assert.That(GetProperty<bool>(keypad, "IsSolved"), Is.False);
        Assert.That(GetProperty<string>(keypad, "EnteredCode"), Is.Empty);
        Assert.That(GetField<int>(keypad, "failedAttempts"), Is.EqualTo(1));
        Assert.That(GetProperty<bool>(door, "IsOpen"), Is.False);

        foreach (string value in new[] { "4", "2", "7", "1" }) Invoke(keypad, "Press", value);
        Assert.That(GetProperty<bool>(keypad, "IsSolved"), Is.True);
        Assert.That(GetProperty<string>(keypad, "EnteredCode"), Is.EqualTo("4271"));
        Assert.That(GetProperty<bool>(door, "IsOpen"), Is.True);
    }

    [Test]
    public void PrototypeStatefulObjects_ImplementPersistenceAndReplayContracts()
    {
        Type persistence = RuntimeType("IDataPersistence");
        Type replay = RuntimeType("IReplayObject");
        foreach (string typeName in new[]
        {
            "SimonSaysController",
            "NoteInteractable",
            "ColorKeyChoicePuzzle",
            "NumericKeypadPuzzle",
            "PrototypeSlidingDoor",
            "EscapeRoomExit"
        })
        {
            Type component = RuntimeType(typeName);
            Assert.That(persistence.IsAssignableFrom(component), Is.True, typeName + " must persist normal game state.");
            Assert.That(replay.IsAssignableFrom(component), Is.True, typeName + " must participate in replay snapshots.");
        }
    }

    Component CreateDoor(string name)
    {
        GameObject root = Track(new GameObject(name));
        root.SetActive(false);
        Transform left = CreateChild("Left", root.transform).transform;
        Transform right = CreateChild("Right", root.transform).transform;
        left.localPosition = new Vector3(-0.75f, 0f, 0f);
        right.localPosition = new Vector3(0.75f, 0f, 0f);
        Component door = root.AddComponent(RuntimeType("PrototypeSlidingDoor"));
        SetField(door, "id", "test-" + name);
        SetField(door, "leftPanel", left);
        SetField(door, "rightPanel", right);
        root.SetActive(true);
        return door;
    }

    GameObject CreateChild(string name, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    T Track<T>(T value) where T : UnityEngine.Object
    {
        createdObjects.Add(value);
        return value;
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

    static T GetProperty<T>(object target, string propertyName)
    {
        return (T)target.GetType().GetProperty(propertyName, InstanceFields).GetValue(target);
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
