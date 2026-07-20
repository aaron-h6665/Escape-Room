using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    const int MaxRaycastHits = 16;

    private Camera cam;
    [SerializeField]
    private float distance = 10f;
    [SerializeField]
    private LayerMask mask;
    [SerializeField]
    private LayerMask blockerMask;
    private PlayerUI playerUI;
    private InputManager inputManager;
    private Interactable focusedInteractable;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[MaxRaycastHits];
    private static readonly RaycastHitDistanceComparer hitDistanceComparer = new RaycastHitDistanceComparer();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cam = GetComponent<PlayerLook>().cam;
        playerUI = GetComponent<PlayerUI>();
        inputManager = GetComponent<InputManager>();
    }

    // Update is called once per frame
    void Update()
    {
        if (ReplayManager.IsPlaybackActive())
        {
            SetFocusedInteractable(null);
            playerUI?.UpdateText(string.Empty);
            return;
        }

        if (cam == null || playerUI == null || inputManager == null)
        {
            SetFocusedInteractable(null);
            return;
        }

        playerUI.UpdateText(string.Empty);

        int raycastMask = mask.value | blockerMask.value;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Interactable interactable = FindInteractableInView(ray, raycastMask);
        if (interactable != null)
        {
            playerUI.UpdateText(interactable.GetPromptMessage());
            if (inputManager.OnFoot.Interact.WasPressedThisFrame())
            {
                interactable.BaseInteract(gameObject);
                if (!isActiveAndEnabled)
                {
                    SetFocusedInteractable(null);
                    return;
                }
            }
        }

        SetFocusedInteractable(interactable);
        focusedInteractable?.UpdateFocusHighlight();
    }

    void OnDisable()
    {
        SetFocusedInteractable(null);
    }

    void SetFocusedInteractable(Interactable interactable)
    {
        if (focusedInteractable == interactable)
        {
            return;
        }

        if (focusedInteractable != null)
        {
            focusedInteractable.SetFocused(false);
        }

        focusedInteractable = interactable;

        if (focusedInteractable != null)
        {
            focusedInteractable.SetFocused(true);
        }
    }

    Interactable FindInteractableInView(Ray ray, int raycastMask)
    {
        int hitCount = Physics.RaycastNonAlloc(ray, hitBuffer, distance, raycastMask, QueryTriggerInteraction.Collide);
        if (hitCount == hitBuffer.Length)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, distance, raycastMask, QueryTriggerInteraction.Collide);
            Array.Sort(hits, hitDistanceComparer);
            return FindInteractableInHits(hits, hits.Length);
        }

        Array.Sort(hitBuffer, 0, hitCount, hitDistanceComparer);
        return FindInteractableInHits(hitBuffer, hitCount);
    }

    Interactable FindInteractableInHits(RaycastHit[] hits, int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            Interactable interactable = hitCollider.GetComponentInParent<Interactable>();
            if (interactable != null)
            {
                return interactable;
            }

            if (IsInLayerMask(hitCollider.gameObject.layer, blockerMask))
            {
                return null;
            }
        }

        return null;
    }

    bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public int Compare(RaycastHit x, RaycastHit y)
        {
            return x.distance.CompareTo(y.distance);
        }
    }
}
