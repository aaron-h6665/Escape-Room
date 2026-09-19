using UnityEngine;

public sealed class ColorKeyChoiceInteractable : Interactable
{
    [SerializeField] ColorKeyChoicePuzzle puzzle;
    [SerializeField] bool isBlueKey;

    protected override string ReplayCategoryValue => "KeyChoice";
    protected override string ReplayInteractionKind => "key_examined";
    protected override string ReplayItemIdValue => isBlueKey ? "blue_key" : "red_key";
    protected override bool RecordReplayInteraction => false;
    public override string GetPromptMessage()
    {
        if (puzzle != null && !puzzle.AnswerVerified) return "Key submissions locked — verify the decoded phrase first";
        return isBlueKey ? "Press E to submit BLUE KEY" : "Press E to submit RED KEY";
    }
    protected override void Interact(GameObject interactor) => puzzle?.Submit(isBlueKey);
}
