using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [SerializeField]
    protected string promptMessage;

    public virtual string GetPromptMessage()
    {
        return promptMessage;
    }

    public void BaseInteract(GameObject interactor)
    {
        Interact(interactor);
    }
    
    protected virtual void Interact(GameObject interactor)
    {

    }
}
