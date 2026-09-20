using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class SimonCipherRuntimeTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    static object Get(object o, string f) => o.GetType().GetField(f, Flags).GetValue(o);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, Flags).SetValue(o,v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o,args);
    static object Prop(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
    static Component Find(string name) => (Component)UnityEngine.Object.FindAnyObjectByType(T(name));

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var name in new[]{"ReplayManager", "StudyMenuPanel"})
        {
            var obj=Find(name); if(obj != null) UnityEngine.Object.Destroy(obj.gameObject);
        }
        yield return SceneManager.LoadSceneAsync("MenuScene"); yield return null;
        Time.timeScale=1f;
    }

    [UnityTest]
    public IEnumerator ClockwiseKeyboardControllerAndMouseAgreeOnTwoNotches()
    {
        yield return SceneManager.LoadSceneAsync("Level"); yield return null;
        var cipher = Find("CaesarCipherInteractable");
        Call(cipher,"OpenInspection",false);
        var innerEnum = Enum.Parse(T("CaesarCipherInteractable+RingSelection"),"Inner");
        var previousBackground = InputSystem.settings.backgroundBehavior;
        var previousEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var gamepad = InputSystem.AddDevice<Gamepad>();
        try
        {
            foreach (var key in new[]{Key.D,Key.RightArrow})
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key)); InputSystem.Update();
                Call(cipher,"ProcessStepInput");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update();
            }
            Assert.That(Prop(cipher,"InnerIndex"),Is.EqualTo(2));
            Assert.That(Prop(cipher,"InnerTopSymbol"),Is.EqualTo('Z'));
            Call(cipher,"SetRingIndex",innerEnum,0,false,false);
            for(int i=0;i<2;i++)
            {
                InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.DpadRight)); InputSystem.Update();
                Call(cipher,"ProcessStepInput");
                InputSystem.QueueStateEvent(gamepad,new GamepadState()); InputSystem.Update();
            }
            Assert.That(Prop(cipher,"InnerIndex"),Is.EqualTo(2));
            Call(cipher,"SetRingIndex",innerEnum,0,false,false);
            var camera=(Camera)Get(cipher,"inspectionCamera");
            Vector3 center=camera.WorldToScreenPoint(((Transform)Get(cipher,"wheelCenter")).position);
            float radius=0.67f*camera.pixelHeight/(2f*camera.orthographicSize);
            Vector2 screenCenter=new Vector2(center.x,center.y);
            float angle=2f*360f/27f*Mathf.Deg2Rad;
            Physics.SyncTransforms();
            Call(cipher,"BeginPointerDrag",screenCenter+Vector2.up*radius);
            Assert.That(Get(cipher,"pointerDragging"),Is.True,"The visible inner ring must be hit-testable.");
            Vector2 end=screenCenter+new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*radius;
            Call(cipher,"UpdatePointerDrag",end); Call(cipher,"EndPointerDrag",end);
            Assert.That(Prop(cipher,"InnerIndex"),Is.EqualTo(2));
            Assert.That(Prop(cipher,"OuterTopSymbol"),Is.EqualTo('A'));
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditor;
            Call(cipher,"CloseInspection",false);
        }
    }

    [UnityTest]
    public IEnumerator Rewatch_StaysSolvedIgnoresInputAndRestoresMidCue()
    {
        yield return SceneManager.LoadSceneAsync("Level"); yield return null;
        Component simon = Find("SimonSaysController");
        Assert.That(Prop(simon,"FinalGreenCount"), Is.EqualTo(2));
        Call(simon,"StartPuzzle"); Call(simon,"AdvanceTimeline",1.5f);
        Call(simon,"ResetPuzzle");
        Assert.That(Prop(simon,"FinalGreenCount"), Is.EqualTo(2), "Retries must not change the clue.");
        Call(simon,"RestoreSolvedState");
        int completions = 0;
        ((UnityEngine.Events.UnityEvent)Prop(simon,"OnPuzzleCompleted")).AddListener(()=>completions++);
        Call(simon,"WatchFinalSequence"); Call(simon,"AdvanceTimeline",0.7f);
        Assert.That(Prop(simon,"IsRewatching"), Is.True);
        Assert.That(Prop(simon,"IsSolved"), Is.True);
        int index = (int)Get(simon,"cueIndex");
        float remaining = (float)Get(simon,"remaining");
        Call(simon,"WatchFinalSequence");
        var button = ((Array)Get(simon,"buttons")).GetValue(0);
        Call(simon,"HandleButtonInteraction",button);
        Assert.That(Get(simon,"cueIndex"), Is.EqualTo(index));
        Assert.That(Get(simon,"remaining"), Is.EqualTo(remaining));
        object saved = Activator.CreateInstance(T("GameData"));
        Call(simon,"SaveSnapshot",saved);
        Call(simon,"AdvanceTimeline",10f);
        Assert.That(Prop(simon,"IsRewatching"), Is.False);
        Assert.That(Prop(simon,"IsSolved"), Is.True);
        Call(simon,"LoadSnapshot",saved);
        Assert.That(Get(simon,"cueIndex"), Is.EqualTo(index));
        Assert.That(Get(simon,"remaining"), Is.EqualTo(remaining));
        Assert.That(Prop(simon,"IsRewatching"), Is.True);
        object normal = Activator.CreateInstance(T("GameData"));
        Call(simon,"SaveData",normal); Call(simon,"RestoreSolvedState"); Call(simon,"LoadData",normal);
        Assert.That(Prop(simon,"IsRewatching"), Is.True);
        Call(simon,"AdvanceTimeline",10f);
        Call(simon,"WatchFinalSequence"); Call(simon,"AdvanceTimeline",10f);
        Assert.That(completions, Is.Zero);
        Assert.That(((Collider)Get(simon,"startInteractionCollider")).enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator SimonDoor_RemainsOpenAndPlayerCanCrossBothWays()
    {
        yield return SceneManager.LoadSceneAsync("Level"); yield return null;
        var simon = Find("SimonSaysController");
        var door = GameObject.Find("SimonSaysRoom/HingeDoor").GetComponent(T("HingeDoor"));
        Assert.That(Prop(door,"IsLocked"), Is.True);
        Call(door,"Open");
        Assert.That(Prop(door,"IsOpen"), Is.False, "Public open must respect the Simon lock.");
        Call(simon,"RestoreSolvedState"); Call(door,"Open"); Call(door,"AdvanceReplayPresentation",2f);
        yield return null;
        var player = Find("PlayerMotor"); ((Behaviour)player).enabled = false;
        var controller = player.GetComponent<CharacterController>();
        controller.enabled=false; player.transform.position=new Vector3(-2.29f,3.04f,11.3f); controller.enabled=true;
        Physics.SyncTransforms();
        for (int crossing=0;crossing<4;crossing++)
        {
            float direction=crossing%2==0?-1f:1f;
            for (int step=0;step<26;step++) controller.Move(new Vector3(0,0,direction*0.1f));
            Assert.That(player.transform.position.z, crossing%2==0 ? Is.LessThan(9f) : Is.GreaterThan(11f));
            Assert.That(Prop(door,"IsOpen"),Is.True);
            Call(simon,"WatchFinalSequence"); Call(simon,"AdvanceTimeline",10f);
            Call(door,"AdvanceReplayPresentation",10f);
        }
        object saved=Activator.CreateInstance(T("GameData")); Call(door,"SaveSnapshot",saved);
        Call(door,"SetOpen",false,false); Call(door,"LoadSnapshot",saved);
        Assert.That(Prop(door,"IsOpen"),Is.True);
        bool backfaces=Physics.queriesHitBackfaces;
        try
        {
            Physics.queriesHitBackfaces=true; Physics.SyncTransforms();
            foreach (float direction in new[]{-1f,1f})
            {
                Vector3 start=new Vector3(-2.29f,3.4f,10f-direction);
                var hits=Physics.RaycastAll(start,Vector3.forward*direction,2f);
                Assert.That(hits.Where(h=>!h.collider.isTrigger && h.collider!=controller).Select(h=>h.collider.name),Is.Empty);
            }
        }
        finally { Physics.queriesHitBackfaces=backfaces; }
    }

    [UnityTest]
    public IEnumerator AllCaesarNoteSurfacesUseTheSameSimonClue()
    {
        yield return SceneManager.LoadSceneAsync("Level"); yield return null;
        var note=GameObject.Find("CaesarCipherRoom/Note").GetComponent(T("NoteInteractable"));
        string clue=(string)Prop(note,"ClueText");
        Assert.That(clue,Does.Contain("UKNGPV QTDKV"));
        Assert.That(clue,Does.Contain("outer ring and read the aligned inner letter"));
        Assert.That(clue,Does.Contain("clockwise one notch"));
        Assert.That(clue,Does.Not.Contain("CCW"));
        Type textType=Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro",true);
        foreach(var text in note.GetComponentsInChildren(textType,true))
            Assert.That(textType.GetProperty("text").GetValue(text),Is.EqualTo(clue));
        Call(note,"OpenNote",Find("PlayerMotor").gameObject);
        Call(note,"CloseNote",false);
        var cipher=Find("CaesarCipherInteractable"); Call(cipher,"OpenInspection",false);
        Call(cipher,"RefreshPinnedClue");
        var pinned=Get(cipher,"pinnedClueText");
        Assert.That(textType.GetProperty("text").GetValue(pinned),Is.EqualTo(clue));
        Assert.That(Prop(note,"ClueSprite"),Is.Null,"Do not reintroduce a stale image beside the shared text.");
        Canvas.ForceUpdateCanvases(); Call(pinned,"ForceMeshUpdate",true,true);
        Assert.That(Prop(pinned,"isTextOverflowing"),Is.False,"The complete instructions must fit the pinned panel.");
        Call(cipher,"CloseInspection",false);
    }
}
