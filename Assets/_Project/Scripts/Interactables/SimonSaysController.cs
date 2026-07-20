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

public class SimonSaysController : Interactable, IDataPersistence, IReplayObject
{
    const string StartedEvent = "simon_started";
    const string RoundStartedEvent = "simon_round_started";
    const string RoundSucceededEvent = "simon_round_succeeded";
    const string FailedEvent = "simon_failed";
    const string CompletedEvent = "simon_completed";

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
    [SerializeField, Min(0.01f)] float buttonCueDuration = 0.42f;
    [SerializeField, Min(0f)] float sequenceGap = 0.16f;
    [SerializeField, Min(0f)] float nextRoundDelay = 0.7f;
    [SerializeField, Min(0.01f)] float resultPulseDuration = 0.18f;

    [Header("Result Feedback")]
    [SerializeField] Color roundSuccessColor = Color.white;
    [SerializeField] Color failureColor = new Color(1f, 0.02f, 0.02f, 1f);
    [SerializeField] Color solvedColor = new Color(0.02f, 1f, 0.12f, 1f);
    [SerializeField, Min(0f)] float resultBrightness = 3f;

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
    ReplayManager.State previousReplayState = ReplayManager.State.Idle;
    AudioClip generatedRoundLow;
    AudioClip generatedRoundHigh;
    AudioClip[] generatedFailureClips;
    AudioClip[] generatedCompletionClips;

    public string StateId => ReplayIdentity.Resolve(this, id);
    public bool IsSolved => phase == SimonSaysPhase.Solved;
    public SimonSaysPhase CurrentPhase => phase;
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
        ReplayManager.State replayState = ReplayManager.instance != null
            ? ReplayManager.instance.CurrentState
            : ReplayManager.State.Idle;

        bool takeoverStarted = previousReplayState == ReplayManager.State.Playback
            && replayState == ReplayManager.State.Takeover;
        if (takeoverStarted
            && phase == SimonSaysPhase.ShowingSequence
            && currentRound == 0
            && activeRoutine == null)
        {
            activeRoutine = StartCoroutine(BeginFirstRound());
        }
        else if (takeoverStarted
            && phase == SimonSaysPhase.RoundSuccess
            && activeRoutine == null
            && currentRound < fixedPattern.Length)
        {
            activeRoutine = StartCoroutine(BeginNextRoundAfterDelay());
        }

        previousReplayState = replayState;
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
                return "Simon Says complete";
            default:
                return promptMessage;
        }
    }

    protected override void Interact(GameObject interactor)
    {
        if (phase == SimonSaysPhase.Idle)
        {
            StartPuzzleInternal(false, false);
        }
    }

    public void HandleButtonInteraction(SimonSaysButton button)
    {
        if (ReplayManager.IsPlaybackActive())
        {
            return;
        }

        if (phase == SimonSaysPhase.Idle)
        {
            StartPuzzleInternal(true, false);
            return;
        }

        SubmitButton(button, false);
    }

    public void HandleReplayButtonPress(SimonSaysButton button)
    {
        if (button == null)
        {
            return;
        }

        StartCoroutine(button.PlayCue(buttonCueDuration));
        if (phase == SimonSaysPhase.AwaitingInput)
        {
            SubmitButton(button, true, false);
        }
    }

    public void StartPuzzle()
    {
        if (phase == SimonSaysPhase.Idle)
        {
            StartPuzzleInternal(true, false);
        }
    }

    public void ResetPuzzle()
    {
        StopActiveRoutine();
        activeSequence.Clear();
        currentRound = 0;
        playerInputIndex = 0;
        phase = SimonSaysPhase.Idle;
        UpdateStartInteractionSurface();
        ApplyIdleVisuals();
    }

    void StartPuzzleInternal(bool publishStartedEvent, bool replayDriven)
    {
        if (phase == SimonSaysPhase.Solved)
        {
            return;
        }

        StopActiveRoutine();
        activeSequence.Clear();
        currentRound = 0;
        playerInputIndex = 0;
        phase = SimonSaysPhase.ShowingSequence;
        UpdateStartInteractionSurface();
        ResetAllButtonVisuals();

        if (publishStartedEvent && !replayDriven)
        {
            ReplayEventBus.Publish(
                this,
                StartedEvent,
                ReplayObjectState.Activated,
                true,
                true);
        }

        if (!replayDriven)
        {
            activeRoutine = StartCoroutine(BeginFirstRound());
        }
    }

    IEnumerator BeginFirstRound()
    {
        yield return new WaitForSeconds(startDelay);
        activeRoutine = null;
        BeginRound();
    }

    IEnumerator BeginNextRoundAfterDelay()
    {
        yield return new WaitForSeconds(nextRoundDelay);
        activeRoutine = null;
        BeginRound();
    }

    void BeginRound()
    {
        if (currentRound >= fixedPattern.Length || phase == SimonSaysPhase.Solved)
        {
            return;
        }

        phase = SimonSaysPhase.ShowingSequence;
        playerInputIndex = 0;
        activeSequence.Add(fixedPattern[currentRound]);
        currentRound = activeSequence.Count;

        ReplayEventBus.Publish(
            this,
            RoundStartedEvent,
            ReplayObjectState.Activated,
            true,
            true,
            textValue: currentRound.ToString(),
            customPayload: SerializeSequence(activeSequence));

        activeRoutine = StartCoroutine(ShowCurrentSequence());
    }

    IEnumerator ShowCurrentSequence()
    {
        ResetAllButtonVisuals();

        foreach (SimonButtonColor color in activeSequence)
        {
            SimonSaysButton button = FindButton(color);
            if (button != null)
            {
                yield return button.PlayCue(buttonCueDuration);
            }

            yield return new WaitForSeconds(sequenceGap);
        }

        playerInputIndex = 0;
        phase = SimonSaysPhase.AwaitingInput;
        activeRoutine = null;
    }

    void SubmitButton(
        SimonSaysButton button,
        bool replayDriven,
        bool playCue = true)
    {
        if (phase != SimonSaysPhase.AwaitingInput
            || inputCuePlaying
            || button == null
            || playerInputIndex >= activeSequence.Count)
        {
            return;
        }

        if (!replayDriven)
        {
            button.RecordAcceptedPress();
        }

        if (playCue)
        {
            StartCoroutine(button.PlayCue(buttonCueDuration));
        }
        inputCuePlaying = true;

        SimonButtonColor expected = activeSequence[playerInputIndex];
        if (button.ButtonColor != expected)
        {
            phase = SimonSaysPhase.Failed;
            if (!replayDriven)
            {
                ReplayEventBus.Publish(
                    this,
                    FailedEvent,
                    ReplayObjectState.Deactivated,
                    false,
                    true,
                    textValue: currentRound.ToString());
                onPuzzleFailed?.Invoke();
            }

            StopActiveRoutine();
            activeRoutine = StartCoroutine(
                PlayFailureFeedbackAfterButtonCue());
            return;
        }

        playerInputIndex++;
        if (playerInputIndex < activeSequence.Count)
        {
            StartCoroutine(ReleaseInputAfterButtonCue());
            return;
        }

        phase = SimonSaysPhase.RoundSuccess;
        if (!replayDriven)
        {
            ReplayEventBus.Publish(
                this,
                RoundSucceededEvent,
                ReplayObjectState.Activated,
                true,
                true,
                textValue: currentRound.ToString());
            onRoundSucceeded?.Invoke();
        }

        StopActiveRoutine();
        activeRoutine = StartCoroutine(
            PlayRoundSuccessFeedbackAfterButtonCue());
    }

    IEnumerator ReleaseInputAfterButtonCue()
    {
        yield return new WaitForSeconds(buttonCueDuration);
        inputCuePlaying = false;
    }

    IEnumerator PlayRoundSuccessFeedbackAfterButtonCue()
    {
        yield return new WaitForSeconds(buttonCueDuration);
        inputCuePlaying = false;
        yield return PlayRoundSuccessFeedback();
    }

    IEnumerator PlayRoundSuccessFeedback()
    {
        yield return PulseAllButtons(
            roundSuccessColor,
            2,
            new[] { GetRoundLowClip(), GetRoundHighClip() });
        activeRoutine = null;

        if (currentRound >= fixedPattern.Length)
        {
            if (!ReplayManager.IsPlaybackActive())
            {
                CompletePuzzle(false);
            }
            yield break;
        }

        if (!ReplayManager.IsPlaybackActive())
        {
            activeRoutine = StartCoroutine(BeginNextRoundAfterDelay());
        }
    }

    IEnumerator PlayFailureFeedback()
    {
        yield return PulseAllButtons(
            failureColor,
            3,
            GetFailureClips());

        activeSequence.Clear();
        currentRound = 0;
        playerInputIndex = 0;
        phase = SimonSaysPhase.Idle;
        activeRoutine = null;
        UpdateStartInteractionSurface();
        ApplyIdleVisuals();
    }

    IEnumerator PlayFailureFeedbackAfterButtonCue()
    {
        yield return new WaitForSeconds(buttonCueDuration);
        inputCuePlaying = false;
        yield return PlayFailureFeedback();
    }

    IEnumerator PlayGameSuccessFeedback()
    {
        ResetAllButtonVisuals();
        SimonSaysButton[] chase = clockwiseButtons != null
            && clockwiseButtons.Length > 0
            ? clockwiseButtons
            : buttons;
        AudioClip[] ascendingClips = GetCompletionClips();

        for (int cycle = 0; cycle < 2; cycle++)
        {
            for (int index = 0; index < chase.Length; index++)
            {
                SimonSaysButton button = chase[index];
                if (button == null)
                {
                    continue;
                }

                button.SetFeedback(
                    SimonSaysButton.GetDisplayColor(button.ButtonColor),
                    resultBrightness,
                    true);
                PlayResultClip(
                    ascendingClips != null && ascendingClips.Length > 0
                        ? ascendingClips[index % ascendingClips.Length]
                        : null);
                yield return new WaitForSeconds(resultPulseDuration);
                button.ResetVisual();
            }
        }

        ApplySolvedVisuals();
        activeRoutine = null;
    }

    IEnumerator PulseAllButtons(
        Color color,
        int pulseCount,
        AudioClip[] clips)
    {
        for (int pulse = 0; pulse < pulseCount; pulse++)
        {
            SetAllButtonFeedback(color, true);
            PlayResultClip(
                clips != null && pulse < clips.Length
                    ? clips[pulse]
                    : null);
            yield return new WaitForSeconds(resultPulseDuration);
            ResetAllButtonVisuals();
            yield return new WaitForSeconds(resultPulseDuration);
        }
    }

    void CompletePuzzle(bool replayDriven)
    {
        StopActiveRoutine();
        phase = SimonSaysPhase.Solved;
        playerInputIndex = activeSequence.Count;
        UpdateStartInteractionSurface();

        if (!replayDriven)
        {
            ReplayEventBus.Publish(
                this,
                CompletedEvent,
                ReplayObjectState.Completed,
                true,
                true,
                customPayload: SerializeSequence(activeSequence));
            onPuzzleCompleted?.Invoke();
        }

        activeRoutine = StartCoroutine(PlayGameSuccessFeedback());
    }

    public override bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        if (replayEvent == null)
        {
            return false;
        }

        switch (replayEvent.eventKind)
        {
            case "simon_interacted":
                return true;
            case StartedEvent:
                StartPuzzleInternal(false, true);
                return true;
            case RoundStartedEvent:
                ApplyReplayRoundStarted(replayEvent);
                return true;
            case RoundSucceededEvent:
                StopActiveRoutine();
                phase = SimonSaysPhase.RoundSuccess;
                UpdateStartInteractionSurface();
                activeRoutine = StartCoroutine(
                    PlayRoundSuccessFeedbackAfterButtonCue());
                return true;
            case FailedEvent:
                StopActiveRoutine();
                phase = SimonSaysPhase.Failed;
                UpdateStartInteractionSurface();
                activeRoutine = StartCoroutine(
                    PlayFailureFeedbackAfterButtonCue());
                return true;
            case CompletedEvent:
                CompletePuzzle(true);
                return true;
            default:
                return false;
        }
    }

    void ApplyReplayRoundStarted(ReplayEventData replayEvent)
    {
        List<SimonButtonColor> replaySequence = ParseSequence(
            replayEvent.customPayload);
        if (replaySequence.Count == 0)
        {
            int round = Mathf.Clamp(
                ParseInt(replayEvent.textValue, 1),
                1,
                fixedPattern.Length);
            replaySequence.AddRange(fixedPattern.Take(round));
        }

        StopActiveRoutine();
        activeSequence.Clear();
        activeSequence.AddRange(replaySequence);
        currentRound = activeSequence.Count;
        playerInputIndex = 0;
        phase = SimonSaysPhase.ShowingSequence;
        UpdateStartInteractionSurface();
        activeRoutine = StartCoroutine(ShowCurrentSequence());
    }

    public void LoadData(GameData data)
    {
        SimonSaysSaveData saved = FindSavedState(data, StateId);
        if (saved != null && saved.isSolved)
        {
            RestoreSolvedState();
        }
        else
        {
            ResetPuzzle();
        }
    }

    public void SaveData(ref GameData data)
    {
        SimonSaysSaveData saved = GetOrCreateSavedState(
            ref data,
            StateId);
        if (saved == null)
        {
            return;
        }

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
        UpdateStartInteractionSurface();

        if (phase == SimonSaysPhase.Solved)
        {
            ApplySolvedVisuals();
        }
        else
        {
            ApplyIdleVisuals();
        }
    }

    void RestoreSolvedState()
    {
        StopActiveRoutine();
        activeSequence.Clear();
        activeSequence.AddRange(fixedPattern);
        currentRound = fixedPattern.Length;
        playerInputIndex = fixedPattern.Length;
        phase = SimonSaysPhase.Solved;
        UpdateStartInteractionSurface();
        ApplySolvedVisuals();
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
            startInteractionCollider.enabled = phase == SimonSaysPhase.Idle;
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
