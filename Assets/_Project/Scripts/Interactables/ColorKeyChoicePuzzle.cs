using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed class ColorKeyChoicePuzzle : MonoBehaviour, IDataPersistence, IReplayObject, IReplayEventTarget
{
    public const string SolutionPhrase = CaesarPuzzleClue.Solution;
    [SerializeField] SimonSaysController clueSimon;
    [SerializeField] NoteInteractable clueNote;
    int displayedGreenCount = -1;

    [SerializeField] string id;
    [SerializeField] PrototypeSlidingDoor exitDoor;
    [SerializeField] HingeDoor hingeExitDoor;
    [SerializeField] TMP_Text feedbackText;
    [SerializeField] bool answerVerified;
    [SerializeField] bool isSolved;
    [SerializeField] int failedAttempts;
    [SerializeField] int answerAttempts;

    public string ReplayTargetId => ReplayIdentity.Resolve(this, id);
    public string ReplayTargetName => gameObject.name;
    public string ReplayTargetCategory => "CaesarPhrase";
    public ReplayObjectState ReplayState => isSolved ? ReplayObjectState.Completed : ReplayObjectState.Idle;
    public bool IsSolved => isSolved;
    public bool AnswerVerified => answerVerified;

    public bool VerifyDecodedAnswer(string answer)
    {
        if (isSolved || ReplayManager.IsPlaybackActive()) return answerVerified;
        answerAttempts++;
        string normalized = string.Concat((answer ?? string.Empty).ToUpperInvariant().Where(char.IsLetter));
        if (normalized != "SILENTORBIT")
        {
            SetFeedback("That decoding is not correct yet. Recheck the Caesar shift.", new Color(1f, 0.55f, 0.25f));
            ReplayEventBus.Publish(this, "caesar_answer_submitted", ReplayObjectState.Attempted, false, false,
                textValue: normalized, numberValue: answerAttempts);
            return false;
        }

        answerVerified = true;
        isSolved = true;
        SetFeedback("PHRASE VERIFIED — PASSAGE UNLOCKED", new Color(0.3f, 0.9f, 1f));
        ApplyState();
        OpenExitDoor();
        ReplayEventBus.Publish(this, "caesar_answer_submitted", ReplayObjectState.Completed, true, true,
            textValue: SolutionPhrase, numberValue: answerAttempts);
        return true;
    }

    void Awake()
    {
        RemoveObsoleteKeyChoices();
        ApplyState();
        ReplayManager.instance?.Register(this);
    }

    void RemoveObsoleteKeyChoices()
    {
        foreach (ColorKeyChoiceInteractable choice in FindObjectsByType<ColorKeyChoiceInteractable>(FindObjectsInactive.Include))
        {
            if (choice != null && choice.gameObject.scene == gameObject.scene)
            {
                Destroy(choice.gameObject);
            }
        }

        Transform oldAward = transform.parent != null ? transform.parent.Find("AwardedBlueKey") : null;
        if (oldAward != null) Destroy(oldAward.gameObject);
    }

    void Start() => RefreshClue();

    void Update()
    {
        if (clueSimon != null && clueSimon.FinalGreenCount != displayedGreenCount) RefreshClue();
    }

    [ContextMenu("Refresh Caesar Clue")]
    public void RefreshClue()
    {
        if (clueSimon == null)
            clueSimon = FindObjectsByType<SimonSaysController>(FindObjectsInactive.Include)
                .FirstOrDefault(value => value.gameObject.scene == gameObject.scene);
        if (clueNote == null && transform.parent != null)
            clueNote = transform.parent.GetComponentsInChildren<NoteInteractable>(true)
                .FirstOrDefault(value => value.ClueText.Contains("CAESAR"));
        if (clueSimon == null || clueNote == null)
        {
            Debug.LogError("The Caesar clue requires its Simon puzzle and paper note.", this);
            return;
        }
        clueNote.SetClueText(CaesarPuzzleClue.ForSimon(clueSimon));
        displayedGreenCount = clueSimon.FinalGreenCount;
    }

    // Legacy scene components call this until the editor migration permanently deletes them.
    public void Submit(bool choseBlue)
    {
        if (!isSolved)
        {
            SetFeedback("Enter the decoded phrase at the terminal.", new Color(1f, 0.75f, 0.2f));
        }
    }

    void SetFeedback(string message, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.color = color;
    }

    void ApplyState()
    {
        if (isSolved && !(ReplayManager.instance?.IsRestoring ?? false))
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
            if (replayEvent.succeeded)
            {
                answerVerified = true;
                isSolved = true;
                ApplyState();
            }
            SetFeedback(isSolved
                ? "PHRASE VERIFIED — PASSAGE UNLOCKED"
                : "That decoding is not correct yet. Recheck the Caesar shift.",
                isSolved ? new Color(0.3f, 0.9f, 1f) : new Color(1f, 0.55f, 0.25f));
            return true;
        }
        // Retained only so an explicitly allowed legacy preview can finish an old recording.
        if (replayEvent.eventKind != "key_choice_submitted") return false;
        failedAttempts = Mathf.Max(failedAttempts, Mathf.RoundToInt(replayEvent.numberValue));
        if (replayEvent.succeeded)
        {
            answerVerified = true;
            isSolved = true;
            SetFeedback("PHRASE VERIFIED — PASSAGE UNLOCKED", new Color(0.3f, 0.9f, 1f));
            ApplyState();
        }
        return true;
    }

    public void LoadData(GameData data) => LoadState(data);
    public void LoadSnapshot(GameData data)
    {
        LoadState(data);
        var ui = data.uiStates?.Find(v => v.id == ReplayTargetId);
        if (ui != null && feedbackText != null) { feedbackText.text = ui.feedback; feedbackText.color = ui.color; }
    }
    public void SaveData(ref GameData data) => SaveState(ref data);
    public void SaveSnapshot(ref GameData data)
    {
        SaveState(ref data);
        data.uiStates.Add(new UiSnapshot { id = ReplayTargetId, feedback = feedbackText != null ? feedbackText.text : "", color = feedbackText != null ? feedbackText.color : Color.white });
    }

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
