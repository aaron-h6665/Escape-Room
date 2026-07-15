using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LockedSafeInteractable : InventoryLockedInteractable, IDataPersistence, IReplayObject
{
    [Header("Combined Safe Animation")]
    [SerializeField] Animator safeAnimator;
    [SerializeField] string safeOpenAnimationName = "SafeOpen";
    [SerializeField] string safeCloseAnimationName = "SafeClose";

    [Header("Optional Separate Animators")]
    [SerializeField] Animator doorAnimator;
    [SerializeField] string doorOpenAnimationName = "SafeDoorOpen";
    [SerializeField] string doorCloseAnimationName = "SafeDoorClose";
    [SerializeField] Animator valveAnimator;
    [SerializeField] string valveOpenAnimationName = "SafeValveOpen";
    [SerializeField] string valveCloseAnimationName = "SafeValveClose";

    [Header("Raycast Blocking")]
    [SerializeField] Collider[] closedInteriorBlockers;
    [SerializeField] Transform interactionColliderTarget;
    [SerializeField] Collider interactionCollider;

    [Header("State")]
    [SerializeField] float waitTimer = 1f;
    [SerializeField] bool pauseInteraction;
    [SerializeField] bool safeOpen;

    [Header("Save Data")]
    [SerializeField] private string id;

    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

    string StateId => ReplayIdentity.Resolve(this, id);
    protected override string ReplayIdentityValue => StateId;
    protected override string ReplayCategoryValue => "Safe";
    protected override string ReplayInteractionKind => "safe_interacted";
    protected override string ReplayStateChangeKind => safeOpen ? "safe_opened" : "safe_closed";
    public override ReplayObjectState ReplayState => safeOpen ? ReplayObjectState.Open : ReplayObjectState.Closed;

    void Awake()
    {
        if (safeAnimator == null && doorAnimator == null && valveAnimator == null)
        {
            safeAnimator = GetComponent<Animator>();
        }

        ResolveInventory();

        EnsureInteractionCollider();

        SetClosedInteriorBlockers(!safeOpen);

        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    public void LoadData(GameData data)
    {
        LoadSafeState(data, StateId);
    }

    public void SaveData(ref GameData data)
    {
        SaveSafeState(ref data, StateId);
    }

    public void SaveSnapshot(ref GameData data)
    {
        SaveSafeState(ref data, StateId);
    }

    public void LoadSnapshot(GameData data)
    {
        LoadSafeState(data, StateId);
    }

    public override string GetPromptMessage()
    {
        if (IsLocked)
        {
            return "Safe Locked";
        }

        return safeOpen ? "Press E to close safe" : "Press E to open safe";
    }

    protected override void Interact(GameObject interactor)
    {
        if (inventory == null)
        {
            ResolveInventory(interactor);
        }

        if (IsLocked)
        {
            return;
        }

        if (pauseInteraction)
        {
            return;
        }

        if (!PlaySafeAnimation(!safeOpen))
        {
            Debug.LogWarning("LockedSafeInteractable is missing an Animator.", this);
            return;
        }

        safeOpen = !safeOpen;
        SetClosedInteriorBlockers(!safeOpen);
        StartCoroutine(PauseSafeInteraction());
    }

    IEnumerator PauseSafeInteraction()
    {
        pauseInteraction = true;
        yield return new WaitForSeconds(waitTimer);
        pauseInteraction = false;
    }

    bool PlaySafeAnimation(bool opening)
    {
        if (safeAnimator != null)
        {
            PlayAnimation(safeAnimator, opening ? safeOpenAnimationName : safeCloseAnimationName);
            return true;
        }

        bool playedAnyAnimation = false;

        if (valveAnimator != null)
        {
            PlayAnimation(valveAnimator, opening ? valveOpenAnimationName : valveCloseAnimationName);
            playedAnyAnimation = true;
        }

        if (doorAnimator != null)
        {
            PlayAnimation(doorAnimator, opening ? doorOpenAnimationName : doorCloseAnimationName);
            playedAnyAnimation = true;
        }

        return playedAnyAnimation;
    }

    void PlayAnimation(Animator animator, string animationName)
    {
        animator.speed = 1f;
        animator.Play(animationName, 0, 0.0f);
    }

    void SetClosedInteriorBlockers(bool active)
    {
        if (closedInteriorBlockers == null)
        {
            return;
        }

        foreach (Collider blocker in closedInteriorBlockers)
        {
            if (blocker != null)
            {
                blocker.enabled = active;
            }
        }
    }

    void EnsureInteractionCollider()
    {
        if (interactionCollider != null)
        {
            return;
        }

        if (interactionColliderTarget == null)
        {
            if (doorAnimator != null)
            {
                interactionColliderTarget = doorAnimator.transform;
            }
            else if (safeAnimator != null)
            {
                interactionColliderTarget = safeAnimator.transform;
            }
        }

        if (interactionColliderTarget == null)
        {
            Debug.LogWarning("LockedSafeInteractable needs a door transform for its interaction collider.", this);
            return;
        }

        interactionCollider = interactionColliderTarget.GetComponent<Collider>();
        if (interactionCollider != null)
        {
            return;
        }

        MeshFilter meshFilter = interactionColliderTarget.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("The safe interaction collider target needs a MeshFilter or an assigned Collider.", interactionColliderTarget);
            return;
        }

        BoxCollider boxCollider = interactionColliderTarget.gameObject.AddComponent<BoxCollider>();
        boxCollider.center = meshFilter.sharedMesh.bounds.center;
        boxCollider.size = meshFilter.sharedMesh.bounds.size;
        interactionCollider = boxCollider;
    }

    void LoadSafeState(GameData data, string stateId)
    {
        if (string.IsNullOrEmpty(stateId) || data.safeStates == null)
        {
            return;
        }

        SafeSaveData safeData = data.safeStates.Find(safeState => safeState.id == stateId);
        if (safeData == null)
        {
            return;
        }

        safeOpen = safeData.isOpen;
        pauseInteraction = false;
        ApplySafeVisualState();
    }

    void SaveSafeState(ref GameData data, string stateId)
    {
        if (string.IsNullOrEmpty(stateId))
        {
            return;
        }

        if (data.safeStates == null)
        {
            data.safeStates = new List<SafeSaveData>();
        }

        SafeSaveData safeData = data.safeStates.Find(safeState => safeState.id == stateId);
        if (safeData == null)
        {
            safeData = new SafeSaveData();
            safeData.id = stateId;
            data.safeStates.Add(safeData);
        }

        safeData.isOpen = safeOpen;
    }

    void ApplySafeVisualState()
    {
        if (safeAnimator != null)
        {
            ApplyAnimationState(safeAnimator, safeOpen ? safeOpenAnimationName : safeCloseAnimationName);
        }
        else
        {
            ApplyAnimationState(valveAnimator, safeOpen ? valveOpenAnimationName : valveCloseAnimationName);
            ApplyAnimationState(doorAnimator, safeOpen ? doorOpenAnimationName : doorCloseAnimationName);
        }

        SetClosedInteriorBlockers(!safeOpen);
    }

    void ApplyAnimationState(Animator animator, string animationName)
    {
        if (animator == null || string.IsNullOrEmpty(animationName))
        {
            return;
        }

        animator.Play(animationName, 0, 1f);
        animator.Update(0f);
    }

}
