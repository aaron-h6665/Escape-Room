using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class NumericKeypadPuzzle : MonoBehaviour, IDataPersistence, IReplayObject, IReplayEventTarget, IReplayTimeline
{
    [SerializeField] string id;
    [SerializeField] string correctCode = "4271";
    [SerializeField, Min(1)] int codeLength = 4;
    [SerializeField] bool autoSubmitOnCodeLength;
    [SerializeField] TMP_Text displayText;
    [SerializeField] PrototypeSlidingDoor finalDoor;
    [SerializeField] string enteredCode = "";
    [SerializeField] bool isSolved;
    [SerializeField] int failedAttempts;
    float feedbackRemaining;

    public string ReplayTargetId => ReplayIdentity.Resolve(this, id);
    public string ReplayTargetName => gameObject.name;
    public string ReplayTargetCategory => "Keypad";
    public ReplayObjectState ReplayState => isSolved ? ReplayObjectState.Completed : ReplayObjectState.Idle;
    public bool IsSolved => isSolved;
    public string EnteredCode => enteredCode;

    void Awake()
    {
        RefreshDisplay();
        ReplayManager.instance?.Register(this);
    }

    public void Press(string value)
    {
        if (isSolved || ReplayManager.IsPlaybackActive() || string.IsNullOrEmpty(value)) return;
        ApplyInput(value, true);
    }

    void ApplyInput(string value, bool record)
    {
        if (value == "enter")
        {
            if (record)
            {
                ReplayEventBus.Publish(this, "keypad_input", ReplayState, true, false, textValue: value, customPayload: enteredCode);
            }
            Submit(record);
            return;
        }

        if (value == "clear") enteredCode = "";
        else if (enteredCode.Length < codeLength && value.Length == 1 && char.IsDigit(value[0])) enteredCode += value;

        feedbackRemaining = 0f;
        RefreshDisplay();
        if (record)
        {
            ReplayEventBus.Publish(this, "keypad_input", ReplayState, true, false, textValue: value, customPayload: enteredCode);
        }
        if (autoSubmitOnCodeLength && enteredCode.Length == codeLength)
        {
            Submit(record);
        }
    }

    void Submit(bool record)
    {
        if (enteredCode == correctCode)
        {
            isSolved = true;

            feedbackRemaining = 0f;
            SetSolvedDisplay();
            finalDoor?.Open();
            if (record) ReplayEventBus.Publish(this, "keypad_solved", ReplayObjectState.Completed, true, true, textValue: correctCode, numberValue: failedAttempts);
            return;
        }

        failedAttempts++;
        enteredCode = "";

        feedbackRemaining = 0.65f;
        RefreshDisplay();
        if (record) ReplayEventBus.Publish(this, "keypad_denied", ReplayObjectState.Attempted, false, false, numberValue: failedAttempts);
    }

    void Update()
    {
        if (!ReplayManager.IsPlaybackActive() && !(ReplayManager.instance?.IsHandoffFrame ?? false)) AdvanceReplayPresentation(Time.deltaTime);
    }
    public void AdvanceReplayPresentation(float seconds)
    {
        feedbackRemaining = Mathf.Max(0f, feedbackRemaining - seconds);
        RefreshDisplay();
    }

    void RefreshDisplay()
    {
        if (displayText == null || isSolved) return;
        if (feedbackRemaining > 0f)
        {
            displayText.text = "TRY AGAIN"; displayText.color = new Color(1f, 0.25f, 0.2f); return;
        }
        displayText.color = new Color(0.35f, 0.95f, 1f);
        displayText.text = string.IsNullOrEmpty(enteredCode) ? "----" : enteredCode.PadRight(codeLength, '-');
    }

    void SetSolvedDisplay()
    {
        if (displayText == null) return;
        displayText.text = "OPEN";
        displayText.color = new Color(0.25f, 1f, 0.45f);
    }

    public bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        if (replayEvent == null) return false;
        if (replayEvent.eventKind == "keypad_input")
        {
            if (!isSolved) ApplyInput(replayEvent.textValue, false);
            return true;
        }
        if (replayEvent.eventKind == "keypad_denied")
        {
            failedAttempts = Mathf.RoundToInt(replayEvent.numberValue);
            enteredCode = "";

            feedbackRemaining = 0.65f;
        RefreshDisplay();
            return true;
        }
        if (replayEvent.eventKind == "keypad_solved")
        {
            isSolved = true;
            enteredCode = correctCode;

            feedbackRemaining = 0f;
            finalDoor?.SetOpen(true, false);
            SetSolvedDisplay();
            return true;
        }
        return false;
    }

    public void LoadData(GameData data) => LoadState(data);
    public void LoadSnapshot(GameData data) => LoadState(data);
    public void SaveData(ref GameData data) => SaveState(ref data);
    public void SaveSnapshot(ref GameData data) => SaveState(ref data);

    void LoadState(GameData data)
    {
        KeypadSaveData saved = data?.keypadStates?.Find(value => value.id == ReplayTargetId);
        if (saved == null) return;
        isSolved = saved.isOpen;
        enteredCode = saved.enteredCode ?? "";
        failedAttempts = saved.failedAttempts;
        feedbackRemaining = saved.feedbackRemaining;
        if (isSolved && !(ReplayManager.instance?.IsRestoring ?? false)) finalDoor?.SetOpen(true, false);
        RefreshDisplay();
        if (isSolved) SetSolvedDisplay();
    }

    void SaveState(ref GameData data)
    {
        data ??= new GameData();
        data.keypadStates ??= new List<KeypadSaveData>();
        KeypadSaveData saved = data.keypadStates.Find(value => value.id == ReplayTargetId);
        if (saved == null)
        {
            saved = new KeypadSaveData { id = ReplayTargetId };
            data.keypadStates.Add(saved);
        }
        saved.isOpen = isSolved;
        saved.enteredCode = enteredCode;
        saved.failedAttempts = failedAttempts;
        saved.feedbackRemaining = feedbackRemaining;
    }
}
