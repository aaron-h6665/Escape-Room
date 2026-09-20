using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public enum SimonSaysPhase
{
    Idle = 0,
    ShowingSequence = 1,
    AwaitingInput = 2,
    RoundSuccess = 3,
    Failed = 4,
    Solved = 5
}

public class SimonSaysController : Interactable, IDataPersistence, IReplayObject, IReplayTimeline
{
    const string StartedEvent = "simon_started";
    const string RoundStartedEvent = "simon_round_started";
    const string RoundSucceededEvent = "simon_round_succeeded";
    const string FailedEvent = "simon_failed";
    const string CompletedEvent = "simon_completed";
    const string RewatchEvent = "simon_rewatch_started";

    [Header("Puzzle")]
    [SerializeField] string id;
    [SerializeField] SimonSaysButton[] buttons;
    [SerializeField] SimonSaysButton[] clockwiseButtons;
    [SerializeField] Collider startInteractionCollider;
    [SerializeField] SimonButtonColor[] fixedPattern =
    {
        SimonButtonColor.Green,
        SimonButtonColor.Red,
        SimonButtonColor.Yellow,
        SimonButtonColor.Blue,
        SimonButtonColor.Green
    };

    [Header("Timing")]
    [SerializeField, Min(0f)] float startDelay = 0.6f;
    [SerializeField, Min(0.01f)] float buttonCueDuration = 0.55f;
    [SerializeField, Min(0f)] float sequenceGap = 0.16f;
    [SerializeField, Min(0f)] float nextRoundDelay = 0.7f;
    [SerializeField, Min(0.01f)] float resultPulseDuration = 0.32f;

    [Header("Result Feedback")]
    [SerializeField] Color roundSuccessColor = Color.white;
    [SerializeField] Color failureColor = new Color(1f, 0.02f, 0.02f, 1f);
    [SerializeField] Color solvedColor = new Color(0.02f, 1f, 0.12f, 1f);
    [SerializeField, Min(0f)] float resultBrightness = 6f;

    [Header("Result Audio")]
    [SerializeField] AudioSource resultAudioSource;
    [SerializeField] AudioClip roundSuccessLowClip;
    [SerializeField] AudioClip roundSuccessHighClip;
    [SerializeField] AudioClip[] failureClips;
    [SerializeField] AudioClip[] completionClips;

    [Header("Future Progression Hooks")]
    [SerializeField] UnityEvent onRoundSucceeded = new UnityEvent();
    [SerializeField] UnityEvent onPuzzleFailed = new UnityEvent();
    [SerializeField] UnityEvent onPuzzleCompleted = new UnityEvent();

    SimonSaysPhase phase = SimonSaysPhase.Idle;
    readonly List<SimonButtonColor> activeSequence = new List<SimonButtonColor>();
    int currentRound;
    int playerInputIndex;
    bool inputCuePlaying;
    Coroutine activeRoutine;
    // Explicit resumable phases replace coroutine-local timing.
    enum CueStage { None, StartDelay, SequenceOn, SequenceGap, InputCue, ResultOn, ResultOff, NextDelay, Completion, RewatchDelay, RewatchOn, RewatchGap }
    CueStage stage;
    float remaining;
    int cueIndex;
    SimonButtonColor pressedColor;
    string renderedCue = "";
    ReplayManager.State previousReplayState = ReplayManager.State.Idle;
    AudioClip generatedRoundLow;
    AudioClip generatedRoundHigh;
    AudioClip[] generatedFailureClips;
    AudioClip[] generatedCompletionClips;

    public string StateId => ReplayIdentity.Resolve(this, id);
    public bool IsSolved => phase == SimonSaysPhase.Solved;
    public SimonSaysPhase CurrentPhase => phase;
    public int FinalGreenCount => fixedPattern.Count(color => color == SimonButtonColor.Green);
    public bool IsRewatching => stage == CueStage.RewatchDelay || stage == CueStage.RewatchOn || stage == CueStage.RewatchGap;
    public UnityEvent OnRoundSucceeded => onRoundSucceeded;
    public UnityEvent OnPuzzleFailed => onPuzzleFailed;
    public UnityEvent OnPuzzleCompleted => onPuzzleCompleted;

    protected override string ReplayIdentityValue => StateId;
    protected override string ReplayCategoryValue => "Puzzle";
    protected override string ReplayInteractionKind => "simon_interacted";
    protected override string ReplayStateChangeKind => StartedEvent;

    public override ReplayObjectState ReplayState
    {
        get
        {
            switch (phase)
            {
                case SimonSaysPhase.ShowingSequence:
                case SimonSaysPhase.AwaitingInput:
                case SimonSaysPhase.RoundSuccess:
                    return ReplayObjectState.Activated;
                case SimonSaysPhase.Failed:
                    return ReplayObjectState.Deactivated;
                case SimonSaysPhase.Solved:
                    return ReplayObjectState.Completed;
                default:
                    return ReplayObjectState.Idle;
            }
        }
    }

    void Awake()
    {
        if (buttons == null || buttons.Length == 0)
        {
            buttons = GetComponentsInChildren<SimonSaysButton>(true);
        }

        if (clockwiseButtons == null || clockwiseButtons.Length == 0)
        {
            clockwiseButtons = buttons;
        }

        if (resultAudioSource == null)
        {
            resultAudioSource = GetComponent<AudioSource>();
        }

        if (resultAudioSource == null)
        {
            resultAudioSource = gameObject.AddComponent<AudioSource>();
            resultAudioSource.playOnAwake = false;
            resultAudioSource.spatialBlend = 1f;
        }

        EnsurePatternIsValid();
        CreateFallbackResultTones();
        ApplyIdleVisuals();
        UpdateStartInteractionSurface();

        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
            previousReplayState = ReplayManager.instance.CurrentState;
        }
    }

    void Update()
    {
        if (!ReplayManager.IsPlaybackActive() && !(ReplayManager.instance?.IsHandoffFrame ?? false)) AdvanceTimeline(Time.deltaTime);
    }

    void AdvanceTimeline(float delta)
    {
        if (stage == CueStage.None || delta <= 0f) return;
        remaining -= delta;
        int guard = 0;
        while (stage != CueStage.None && remaining <= 0f && guard++ < 128)
        {
            float overflow = -remaining;
            switch (stage)
            {
                case CueStage.RewatchDelay: Enter(CueStage.RewatchOn, buttonCueDuration); break;
                case CueStage.RewatchOn: Enter(CueStage.RewatchGap, sequenceGap); break;
                case CueStage.RewatchGap:
                    cueIndex++;
                    Enter(cueIndex < activeSequence.Count ? CueStage.RewatchOn : CueStage.None,
                        cueIndex < activeSequence.Count ? buttonCueDuration : 0f);
                    break;
                case CueStage.StartDelay: case CueStage.NextDelay: BeginRound(); break;
                case CueStage.SequenceOn: Enter(CueStage.SequenceGap, sequenceGap); break;
                case CueStage.SequenceGap:
                    cueIndex++;
                    if (cueIndex < activeSequence.Count) Enter(CueStage.SequenceOn, buttonCueDuration);
                    else { phase = SimonSaysPhase.AwaitingInput; Enter(CueStage.None, 0); }
                    break;
                case CueStage.InputCue:
                    inputCuePlaying = false;
                    if (phase == SimonSaysPhase.AwaitingInput) Enter(CueStage.None, 0);
                    else { cueIndex = 0; Enter(CueStage.ResultOn, resultPulseDuration); }
                    break;
                case CueStage.ResultOn: Enter(CueStage.ResultOff, resultPulseDuration); break;
                case CueStage.ResultOff:
                    cueIndex++;
                    if (cueIndex < (phase == SimonSaysPhase.Failed ? 3 : 2)) Enter(CueStage.ResultOn, resultPulseDuration);
                    else if (phase == SimonSaysPhase.Failed) ResetPuzzle();
                    else if (currentRound >= fixedPattern.Length) CompletePuzzle(false);
                    else Enter(CueStage.NextDelay, nextRoundDelay);
                    break;
                case CueStage.Completion:
                    cueIndex++;
                    if (cueIndex < buttons.Length * 2) Enter(CueStage.Completion, resultPulseDuration);
                    else Enter(CueStage.None, 0);
                    break;
            }
            remaining -= overflow;
        }
    }

    void Enter(CueStage next, float duration)
    {
        stage = next; remaining = Mathf.Max(0f, duration);
        UpdateStartInteractionSurface(); RenderTimeline();
    }

    void RenderTimeline()
    {
        ResetAllButtonVisuals();
        string key = currentRound + ":" + stage + ":" + cueIndex + ":" + pressedColor + ":" + phase;
        bool sound = key != renderedCue;
        renderedCue = key;
        if ((stage == CueStage.SequenceOn || stage == CueStage.RewatchOn) && cueIndex < activeSequence.Count)
        {
            SimonSaysButton button = FindButton(activeSequence[cueIndex]);
            button?.SetFeedback(SimonSaysButton.GetDisplayColor(activeSequence[cueIndex]), resultBrightness, true);
            if (sound) button?.PlayTone();
        }
        else if (stage == CueStage.InputCue)
        {
            SimonSaysButton button = FindButton(pressedColor);
            button?.SetFeedback(SimonSaysButton.GetDisplayColor(pressedColor), resultBrightness, true);
            if (sound) button?.PlayTone();
        }
        else if (stage == CueStage.ResultOn)
        {
            SetAllButtonFeedback(phase == SimonSaysPhase.Failed ? failureColor : roundSuccessColor, true);
            var clips = phase == SimonSaysPhase.Failed ? GetFailureClips() : new[] { GetRoundLowClip(), GetRoundHighClip() };
            if (sound && clips.Length > 0) PlayResultClip(clips[cueIndex % clips.Length]);
        }
        else if (stage == CueStage.Completion && buttons.Length > 0)
        {
            var chase = clockwiseButtons != null && clockwiseButtons.Length > 0 ? clockwiseButtons : buttons;
            var button = chase[cueIndex % chase.Length];
            button?.SetFeedback(SimonSaysButton.GetDisplayColor(button.ButtonColor), resultBrightness, true);
            var clips = GetCompletionClips();
            if (sound && clips.Length > 0) PlayResultClip(clips[cueIndex % clips.Length]);
        }
        else if (IsSolved && !IsRewatching) ApplySolvedVisuals();
    }

    public void AdvanceReplayPresentation(float seconds)
    {
        remaining = Mathf.Max(0f, remaining - seconds);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopActiveRoutine();
        ResetAllButtonVisuals();
    }

    void OnDestroy()
    {
        DestroyGeneratedClip(generatedRoundLow);
        DestroyGeneratedClip(generatedRoundHigh);
        if (generatedFailureClips != null)
        {
            foreach (AudioClip clip in generatedFailureClips)
            {
                DestroyGeneratedClip(clip);
            }
        }
        if (generatedCompletionClips != null)
        {
            foreach (AudioClip clip in generatedCompletionClips)
            {
                DestroyGeneratedClip(clip);
            }
        }
    }

    [ContextMenu("Generate guid for id")]
    void GenerateGuid()
    {
        id = Guid.NewGuid().ToString();
    }

    public override string GetPromptMessage()
    {
        return GetPhasePrompt(false, default);
    }

    public string GetButtonPrompt(SimonButtonColor color)
    {
        return GetPhasePrompt(true, color);
    }

    string GetPhasePrompt(bool isButton, SimonButtonColor color)
    {
        switch (phase)
        {
            case SimonSaysPhase.Idle:
                return "Press E to start Simon Says";
            case SimonSaysPhase.ShowingSequence:
                return "Watch the sequence";
            case SimonSaysPhase.AwaitingInput:
                return isButton
                    ? "Press E to choose " + color
                    : "Choose a colored button";
            case SimonSaysPhase.RoundSuccess:
                return "Round complete";
            case SimonSaysPhase.Failed:
                return "Try again";
            case SimonSaysPhase.Solved:
                return IsRewatching ? "Watch the final sequence"
                    : stage == CueStage.None ? "Press E to watch the final sequence again" : "Simon Says complete";
            default:
                return promptMessage;
        }
    }

    protected override void Interact(GameObject interactor) => StartPuzzle();

    public void StartPuzzle()
    {
        if (ReplayManager.IsPlaybackActive()) return;
        if (IsSolved) { WatchFinalSequence(); return; }
        if (phase != SimonSaysPhase.Idle) return;
        activeSequence.Clear(); currentRound = playerInputIndex = cueIndex = 0;
        phase = SimonSaysPhase.ShowingSequence;
        Enter(CueStage.StartDelay, startDelay);
        ReplayEventBus.Publish(this, StartedEvent, ReplayState, true, true);
    }

    public void WatchFinalSequence()
    {
        if (!IsSolved || stage != CueStage.None || ReplayManager.IsPlaybackActive()) return;
        activeSequence.Clear();
        activeSequence.AddRange(fixedPattern);
        cueIndex = 0;
        inputCuePlaying = false;
        Enter(CueStage.RewatchDelay, startDelay);
        ReplayEventBus.Publish(this, RewatchEvent, ReplayState, true, true,
            customPayload: SerializeSequence(activeSequence));
    }

    public void ResetPuzzle()
    {
        StopActiveRoutine(); activeSequence.Clear(); currentRound = playerInputIndex = cueIndex = 0;
        phase = SimonSaysPhase.Idle; Enter(CueStage.None, 0);
    }

    void BeginRound()
    {
        if (currentRound >= fixedPattern.Length) return;
        activeSequence.Add(fixedPattern[currentRound]); currentRound = activeSequence.Count;
        cueIndex = playerInputIndex = 0; phase = SimonSaysPhase.ShowingSequence;
        Enter(CueStage.SequenceOn, buttonCueDuration);
        ReplayEventBus.Publish(this, RoundStartedEvent, ReplayState, true, true,
            textValue: currentRound.ToString(), customPayload: SerializeSequence(activeSequence));
    }

    public void HandleButtonInteraction(SimonSaysButton button)
    {
        if (ReplayManager.IsPlaybackActive()) return;
        if (phase == SimonSaysPhase.Idle || IsSolved) { StartPuzzle(); return; }
        if (phase != SimonSaysPhase.AwaitingInput || inputCuePlaying || button == null || playerInputIndex >= activeSequence.Count) return;
        button.RecordAcceptedPress(); pressedColor = button.ButtonColor; inputCuePlaying = true;
        if (pressedColor != activeSequence[playerInputIndex])
        {
            phase = SimonSaysPhase.Failed;
            ReplayEventBus.Publish(this, FailedEvent, ReplayState, false, true, textValue: currentRound.ToString());
            onPuzzleFailed?.Invoke();
        }
        else if (++playerInputIndex >= activeSequence.Count)
        {
            phase = SimonSaysPhase.RoundSuccess;
            ReplayEventBus.Publish(this, RoundSucceededEvent, ReplayState, true, true, textValue: currentRound.ToString());
            onRoundSucceeded?.Invoke();
        }
        Enter(CueStage.InputCue, buttonCueDuration);
    }

    public void HandleReplayButtonPress(SimonSaysButton button) { }

    void CompletePuzzle(bool replayDriven)
    {
        phase = SimonSaysPhase.Solved; playerInputIndex = activeSequence.Count; cueIndex = 0;
        Enter(CueStage.Completion, resultPulseDuration);
        if (!replayDriven)
        {
            ReplayEventBus.Publish(this, CompletedEvent, ReplayState, true, true, customPayload: SerializeSequence(activeSequence));
            onPuzzleCompleted?.Invoke();
        }
    }

    public override bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        // v3 playback restores complete snapshots; legacy previews cannot reproduce cue timing.
        return replayEvent != null && replayEvent.eventKind.StartsWith("simon_", StringComparison.Ordinal);
    }

    public void LoadData(GameData data)
    {
        SimonSaysSaveData saved = FindSavedState(data, StateId);
        if (saved != null && saved.isSolved)
        {
            if (saved.hasTimeline && saved.timelineStage >= (int)CueStage.RewatchDelay)
            {
                LoadSnapshot(data);
                return;
            }
            RestoreSolvedState();
        }
        else
        {
            ResetPuzzle();
        }
    }

    public void SaveData(ref GameData data)
    {
        if (IsRewatching) { SaveSnapshot(ref data); return; }
        SimonSaysSaveData saved = GetOrCreateSavedState(
            ref data,
            StateId);
        if (saved == null)
        {
            return;
        }

        saved.hasTimeline = false;
        saved.timelineStage = saved.cueIndex = 0;
        saved.stageRemaining = 0f;
        saved.isSolved = IsSolved;
        saved.phase = (int)(IsSolved
            ? SimonSaysPhase.Solved
            : SimonSaysPhase.Idle);
        saved.currentRound = IsSolved ? fixedPattern.Length : 0;
        saved.playerInputIndex = IsSolved ? fixedPattern.Length : 0;
        saved.sequence = IsSolved
            ? fixedPattern.Select(color => (int)color).ToList()
            : new List<int>();
    }

    public void SaveSnapshot(ref GameData data)
    {
        SimonSaysSaveData saved = GetOrCreateSavedState(
            ref data,
            StateId);
        if (saved == null)
        {
            return;
        }

        saved.isSolved = IsSolved;
        saved.phase = (int)phase;
        saved.hasTimeline = true;
        saved.timelineStage = (int)stage;
        saved.stageRemaining = remaining;
        saved.cueIndex = cueIndex;
        saved.pressedColor = (int)pressedColor;
        saved.currentRound = currentRound;
        saved.playerInputIndex = playerInputIndex;
        saved.sequence = activeSequence
            .Select(color => (int)color)
            .ToList();
    }

    public void LoadSnapshot(GameData data)
    {
        SimonSaysSaveData saved = FindSavedState(data, StateId);
        if (saved == null)
        {
            ResetPuzzle();
            return;
        }

        StopActiveRoutine();
        activeSequence.Clear();
        if (saved.sequence != null)
        {
            foreach (int value in saved.sequence)
            {
                if (Enum.IsDefined(typeof(SimonButtonColor), value))
                {
                    activeSequence.Add((SimonButtonColor)value);
                }
            }
        }

        currentRound = Mathf.Clamp(
            saved.currentRound,
            0,
            fixedPattern.Length);
        playerInputIndex = Mathf.Clamp(
            saved.playerInputIndex,
            0,
            activeSequence.Count);
        phase = saved.isSolved
            ? SimonSaysPhase.Solved
            : SanitizePhase(saved.phase);
        stage = saved.hasTimeline ? (CueStage)saved.timelineStage : CueStage.None;
        remaining = saved.stageRemaining;
        cueIndex = saved.cueIndex;
        pressedColor = (SimonButtonColor)saved.pressedColor;
        inputCuePlaying = stage == CueStage.InputCue;
        UpdateStartInteractionSurface();
        RenderTimeline();
    }

    void RestoreSolvedState()
    {
        StopActiveRoutine();
        activeSequence.Clear();
        activeSequence.AddRange(fixedPattern);
        currentRound = fixedPattern.Length;
        playerInputIndex = fixedPattern.Length;
        phase = SimonSaysPhase.Solved;
        cueIndex = 0;
        Enter(CueStage.None, 0f);
    }

    SimonSaysPhase SanitizePhase(int value)
    {
        if (!Enum.IsDefined(typeof(SimonSaysPhase), value))
        {
            return SimonSaysPhase.Idle;
        }

        return (SimonSaysPhase)value;
    }

    SimonSaysSaveData FindSavedState(GameData data, string stateId)
    {
        if (data == null
            || data.simonSaysStates == null
            || string.IsNullOrWhiteSpace(stateId))
        {
            return null;
        }

        return data.simonSaysStates.Find(
            state => state != null && state.id == stateId);
    }

    SimonSaysSaveData GetOrCreateSavedState(
        ref GameData data,
        string stateId)
    {
        if (data == null || string.IsNullOrWhiteSpace(stateId))
        {
            return null;
        }

        if (data.simonSaysStates == null)
        {
            data.simonSaysStates = new List<SimonSaysSaveData>();
        }

        SimonSaysSaveData saved = data.simonSaysStates.Find(
            state => state != null && state.id == stateId);
        if (saved == null)
        {
            saved = new SimonSaysSaveData { id = stateId };
            data.simonSaysStates.Add(saved);
        }

        return saved;
    }

    void EnsurePatternIsValid()
    {
        if (fixedPattern == null || fixedPattern.Length == 0)
        {
            fixedPattern = new[]
            {
                SimonButtonColor.Green,
                SimonButtonColor.Red,
                SimonButtonColor.Yellow,
                SimonButtonColor.Blue,
                SimonButtonColor.Green
            };
        }
    }

    SimonSaysButton FindButton(SimonButtonColor color)
    {
        return buttons != null
            ? Array.Find(
                buttons,
                button => button != null && button.ButtonColor == color)
            : null;
    }

    void StopActiveRoutine()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
        inputCuePlaying = false;
    }

    void ApplyIdleVisuals()
    {
        ResetAllButtonVisuals();
    }

    void ApplySolvedVisuals()
    {
        SetAllButtonFeedback(solvedColor, false);
    }

    void UpdateStartInteractionSurface()
    {
        if (startInteractionCollider != null)
        {
            startInteractionCollider.enabled = phase == SimonSaysPhase.Idle || (IsSolved && stage == CueStage.None);
        }
    }

    void SetAllButtonFeedback(Color color, bool pressed)
    {
        if (buttons == null)
        {
            return;
        }

        foreach (SimonSaysButton button in buttons)
        {
            button?.SetFeedback(color, resultBrightness, pressed);
        }
    }

    void ResetAllButtonVisuals()
    {
        if (buttons == null)
        {
            return;
        }

        foreach (SimonSaysButton button in buttons)
        {
            button?.ResetVisual();
        }
    }

    void CreateFallbackResultTones()
    {
        if (roundSuccessLowClip == null)
        {
            generatedRoundLow = SimonToneUtility.CreateSineTone(
                "Simon_Round_Low",
                659.25f,
                0.18f,
                0.2f);
        }

        if (roundSuccessHighClip == null)
        {
            generatedRoundHigh = SimonToneUtility.CreateSineTone(
                "Simon_Round_High",
                783.99f,
                0.18f,
                0.2f);
        }

        if (failureClips == null || failureClips.Length < 3)
        {
            generatedFailureClips = new[]
            {
                SimonToneUtility.CreateSineTone(
                    "Simon_Fail_1", 196f, 0.18f, 0.22f),
                SimonToneUtility.CreateSineTone(
                    "Simon_Fail_2", 164.81f, 0.18f, 0.22f),
                SimonToneUtility.CreateSineTone(
                    "Simon_Fail_3", 130.81f, 0.22f, 0.22f)
            };
        }

        if (completionClips == null || completionClips.Length < 4)
        {
            generatedCompletionClips = new[]
            {
                SimonToneUtility.CreateSineTone(
                    "Simon_Complete_1", 523.25f, 0.18f, 0.2f),
                SimonToneUtility.CreateSineTone(
                    "Simon_Complete_2", 659.25f, 0.18f, 0.2f),
                SimonToneUtility.CreateSineTone(
                    "Simon_Complete_3", 783.99f, 0.18f, 0.2f),
                SimonToneUtility.CreateSineTone(
                    "Simon_Complete_4", 1046.5f, 0.22f, 0.2f)
            };
        }
    }

    AudioClip GetRoundLowClip()
    {
        return roundSuccessLowClip != null
            ? roundSuccessLowClip
            : generatedRoundLow;
    }

    AudioClip GetRoundHighClip()
    {
        return roundSuccessHighClip != null
            ? roundSuccessHighClip
            : generatedRoundHigh;
    }

    AudioClip[] GetFailureClips()
    {
        return failureClips != null && failureClips.Length >= 3
            ? failureClips
            : generatedFailureClips;
    }

    AudioClip[] GetCompletionClips()
    {
        return completionClips != null && completionClips.Length >= 4
            ? completionClips
            : generatedCompletionClips;
    }

    void PlayResultClip(AudioClip clip)
    {
        if (resultAudioSource != null && clip != null)
        {
            resultAudioSource.PlayOneShot(clip);
        }
    }

    static void DestroyGeneratedClip(AudioClip clip)
    {
        if (clip != null)
        {
            Destroy(clip);
        }
    }

    static string SerializeSequence(
        IEnumerable<SimonButtonColor> sequence)
    {
        return string.Join(
            ",",
            sequence.Select(color => ((int)color).ToString()));
    }

    static List<SimonButtonColor> ParseSequence(string payload)
    {
        List<SimonButtonColor> result = new List<SimonButtonColor>();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return result;
        }

        foreach (string part in payload.Split(','))
        {
            if (int.TryParse(part, out int value)
                && Enum.IsDefined(typeof(SimonButtonColor), value))
            {
                result.Add((SimonButtonColor)value);
            }
        }

        return result;
    }

    static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, out int parsed) ? parsed : fallback;
    }

    public void Configure(
        string stableId,
        SimonSaysButton[] configuredButtons,
        SimonSaysButton[] configuredClockwiseButtons,
        AudioSource configuredAudioSource,
        Collider configuredStartInteractionCollider)
    {
        id = stableId;
        buttons = configuredButtons;
        clockwiseButtons = configuredClockwiseButtons;
        resultAudioSource = configuredAudioSource;
        startInteractionCollider = configuredStartInteractionCollider;
        promptMessage = "Press E to start Simon Says";
        highlightOnFocus = false;
        highlightRenderers = new Renderer[0];
    }
}
