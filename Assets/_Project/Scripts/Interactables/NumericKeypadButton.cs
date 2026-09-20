using UnityEngine;

public sealed class NumericKeypadButton : Interactable
{
    [SerializeField] NumericKeypadPuzzle keypad;
    [SerializeField] string value;
    protected override string ReplayCategoryValue => "Keypad";
    protected override string ReplayInteractionKind => "keypad_button_pressed";
    protected override bool RecordReplayInteraction => false;
    public override string GetPromptMessage() => value == "enter" ? "Press E to ENTER"
        : value == "clear" ? "Press E to CLEAR"
        : value == "delete" ? "Press E to DELETE"
        : "Press E to enter " + value;
    protected override void Interact(GameObject interactor) => keypad?.Press(value);
}
