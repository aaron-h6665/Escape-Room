using UnityEngine;
using System.Collections;

public class LockedDoorInteractable : Interactable
{
    [Header("Inventory Requirement")]
    [SerializeField] Inventory inventory;
    [SerializeField] Item requiredItem;
    [SerializeField] string requiredItemId = "red_key";

    [Header("Animation Names")]
    [SerializeField] Animator doorAnimator;
    [SerializeField] string openAnimationName = "HingeDoorOpen";
    [SerializeField] string closeAnimationName = "HingeDoorClose";

    [Header("UI")]
    [SerializeField] int timeToShowUI = 1;
    [SerializeField] GameObject showDoorLockedUI;

    [Header("State")]
    [SerializeField] int waitTimer = 1;
    [SerializeField] bool pauseInteraction;
    [SerializeField] bool doorOpen;

    Coroutine doorLockedCoroutine;

    bool IsLocked => inventory == null || !inventory.HasItem(RequiredItemId);
    string RequiredItemId => requiredItem != null ? requiredItem.Id : requiredItemId;

    void Awake()
    {
        if (doorAnimator == null)
        {
            doorAnimator = GetComponent<Animator>();
        }

        ResolveInventory();

        if (showDoorLockedUI != null)
        {
            showDoorLockedUI.SetActive(false);
        }
    }

    public override string GetPromptMessage()
    {
        if (IsLocked)
        {
            return "Door Locked";
        }

        return doorOpen ? "Press E to close door" : "Press E to open door";
    }

    protected override void Interact(GameObject interactor)
    {
        if (inventory == null)
        {
            ResolveInventory(interactor);
        }

        if (IsLocked)
        {
            ShowDoorLockedMessage();
            return;
        }

        if (pauseInteraction)
        {
            return;
        }

        if (doorAnimator == null)
        {
            Debug.LogWarning("LockedDoorInteractable is missing an Animator.", this);
            return;
        }

        doorAnimator.speed = 1f;
        doorAnimator.Play(doorOpen ? closeAnimationName : openAnimationName, 0, 0.0f);
        doorOpen = !doorOpen;
        StartCoroutine(PauseDoorInteraction());
    }

    IEnumerator PauseDoorInteraction()
    {
        pauseInteraction = true;
        yield return new WaitForSeconds(waitTimer);
        pauseInteraction = false;
    }

    void ShowDoorLockedMessage()
    {
        if (showDoorLockedUI == null)
        {
            return;
        }

        if (doorLockedCoroutine != null)
        {
            StopCoroutine(doorLockedCoroutine);
        }

        doorLockedCoroutine = StartCoroutine(ShowDoorLocked());
    }

    IEnumerator ShowDoorLocked()
    {
        showDoorLockedUI.SetActive(true);
        yield return new WaitForSeconds(timeToShowUI);
        showDoorLockedUI.SetActive(false);
        doorLockedCoroutine = null;
    }

    void ResolveInventory(GameObject interactor = null)
    {
        if (inventory != null)
        {
            return;
        }

        if (interactor != null)
        {
            inventory = interactor.GetComponentInParent<Inventory>();
            if (inventory != null)
            {
                return;
            }
        }

#if UNITY_2023_1_OR_NEWER
        inventory = FindFirstObjectByType<Inventory>();
#else
        inventory = FindObjectOfType<Inventory>();
#endif
    }
}
