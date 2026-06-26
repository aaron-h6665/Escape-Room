using UnityEngine;

public class HingeDoor : Interactable
{
    [SerializeField] private Animator myDoor = null;

    [SerializeField] private string doorOpen = "HingeDoorOpen";

    protected override void Interact(GameObject interactor)
    {
        if (myDoor != null)
        {
            myDoor.Play(doorOpen, 0, 0.0f);
        }
    }
}
