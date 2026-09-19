using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed class ColorKeyChoicePuzzle : MonoBehaviour, IDataPersistence, IReplayObject, IReplayEventTarget
{
    [SerializeField] string id;
    [SerializeField] Item blueKeyItem;
    [SerializeField] Inventory inventory;
    [SerializeField] PrototypeSlidingDoor exitDoor;
    [SerializeField] HingeDoor hingeExitDoor;
    [SerializeField] ColorKeyChoiceInteractable redKey;
    [SerializeField] ColorKeyChoiceInteractable blueKey;
    [SerializeField] TMP_Text feedbackText;
    [SerializeField] bool answerVerified;
    [SerializeField] bool isSolved;
    [SerializeField] int failedAttempts;
    [SerializeField] int answerAttempts;

    public string ReplayTargetId => ReplayIdentity.Resolve(this, id);
    public string ReplayTargetName => gameObject.name;
    public string ReplayTargetCategory => "KeyChoice";
    public ReplayObjectState ReplayState => isSolved ? ReplayObjectState.Completed : ReplayObjectState.Idle;
    public bool IsSolved => isSolved;
    public bool AnswerVerified => answerVerified;

    public bool VerifyDecodedAnswer(string answer)
    {
        if (isSolved || ReplayManager.IsPlaybackActive()) return answerVerified;
        answerAttempts++;
        string normalized = string.Concat((answer ?? string.Empty).ToUpperInvariant().Where(char.IsLetter));
        if (normalized != "BLUEKEY")
        {
            SetFeedback("That decoding is not correct yet. Recheck the Caesar shift.", new Color(1f, 0.55f, 0.25f));
            ReplayEventBus.Publish(this, "caesar_answer_submitted", ReplayObjectState.Attempted, false, false,
                textValue: normalized, numberValue: answerAttempts);
            return false;
        }

        answerVerified = true;
        SetFeedback("Decoded phrase verified. Key submissions are now unlocked.", new Color(0.3f, 0.9f, 1f));
        ReplayEventBus.Publish(this, "caesar_answer_submitted", ReplayObjectState.Activated, true, true,
            textValue: "BLUE KEY", numberValue: answerAttempts);
        return true;
    }

    void Awake()
    {
        if (inventory == null) inventory = FindAnyObjectByType<Inventory>();
        ApplyState();
        ReplayManager.instance?.Register(this);
    }

    public void Submit(bool choseBlue)
    {
        if (isSolved || ReplayManager.IsPlaybackActive()) return;
        if (!answerVerified)
        {
            SetFeedback("Decode the cipher and verify the phrase at the terminal first.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        if (!choseBlue)
        {
            failedAttempts++;
            SetFeedback("The red key does not match the decoded answer. Try again.", new Color(1f, 0.35f, 0.3f));
            ReplayEventBus.Publish(this, "key_choice_submitted", ReplayObjectState.Attempted, false, false, textValue: "red", numberValue: failedAttempts);
            return;
        }

        if (!EnsureBlueKeyAwarded())
        {
            SetFeedback("Your inventory is full.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        isSolved = true;
        SetFeedback("BLUE KEY verified — passage unlocked.", new Color(0.25f, 0.85f, 1f));
        ApplyState();
        OpenExitDoor();
        ReplayEventBus.Publish(this, "key_choice_submitted", ReplayObjectState.Completed, true, true, blueKeyItem.Id, textValue: "blue", numberValue: failedAttempts);
    }

    void SetFeedback(string message, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.color = color;
    }

    void ApplyState()
    {
        if (redKey != null) redKey.gameObject.SetActive(!isSolved);
        if (blueKey != null) blueKey.gameObject.SetActive(!isSolved);
        if (isSolved)
        {
            exitDoor?.SetOpen(true, false);
            hingeExitDoor?.SetOpen(true, false);
        }
    }

    void OpenExitDoor()
    {
        exitDoor?.Open();
        hingeExitDoor?.Open();
    }

    public bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        if (replayEvent == null) return false;
        if (replayEvent.eventKind == "caesar_answer_submitted")
        {
            answerAttempts = Mathf.Max(answerAttempts, Mathf.RoundToInt(replayEvent.numberValue));
            if (replayEvent.succeeded) answerVerified = true;
            SetFeedback(answerVerified
                ? "Decoded phrase verified. Key submissions are now unlocked."
                : "That decoding is not correct yet. Recheck the Caesar shift.",
                answerVerified ? new Color(0.3f, 0.9f, 1f) : new Color(1f, 0.55f, 0.25f));
            return true;
        }
        if (replayEvent.eventKind != "key_choice_submitted") return false;
        failedAttempts = Mathf.Max(failedAttempts, Mathf.RoundToInt(replayEvent.numberValue));
        if (replayEvent.succeeded)
        {
            isSolved = true;
            EnsureBlueKeyAwarded();
            SetFeedback("BLUE KEY verified — passage unlocked.", new Color(0.25f, 0.85f, 1f));
            ApplyState();
        }
        else
        {
            SetFeedback("The red key does not match the decoded answer. Try again.", new Color(1f, 0.35f, 0.3f));
        }
        return true;
    }

    bool EnsureBlueKeyAwarded()
    {
        if (inventory == null) inventory = FindAnyObjectByType<Inventory>();
        if (inventory == null || blueKeyItem == null) return false;
        return inventory.HasItem(blueKeyItem.Id)
            || inventory.AddItem(blueKeyItem, ReplayTargetId + ":blue");
    }

    public void LoadData(GameData data) => LoadState(data);
    public void LoadSnapshot(GameData data) => LoadState(data);
    public void SaveData(ref GameData data) => SaveState(ref data);
    public void SaveSnapshot(ref GameData data) => SaveState(ref data);

    void LoadState(GameData data)
    {
        KeyChoiceSaveData saved = data?.keyChoiceStates?.Find(value => value.id == ReplayTargetId);
        if (saved == null) return;
        isSolved = saved.isSolved;
        answerVerified = saved.answerVerified;
        failedAttempts = saved.failedAttempts;
        answerAttempts = saved.answerAttempts;
        ApplyState();
    }

    void SaveState(ref GameData data)
    {
        data ??= new GameData();
        data.keyChoiceStates ??= new List<KeyChoiceSaveData>();
        KeyChoiceSaveData saved = data.keyChoiceStates.Find(value => value.id == ReplayTargetId);
        if (saved == null)
        {
            saved = new KeyChoiceSaveData { id = ReplayTargetId };
            data.keyChoiceStates.Add(saved);
        }
        saved.isSolved = isSolved;
        saved.answerVerified = answerVerified;
        saved.failedAttempts = failedAttempts;
        saved.answerAttempts = answerAttempts;
    }
}
