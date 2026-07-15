using UnityEngine;
using System.Collections.Generic;

public abstract class Interactable : MonoBehaviour, IReplayEventTarget
{
    [SerializeField]
    protected string promptMessage;

    [Header("Focus Highlight")]
    [SerializeField] protected bool highlightOnFocus = true;
    [SerializeField] protected Color highlightColor = new Color(1f, 0.82f, 0.18f, 1f);
    [SerializeField] protected float highlightMinIntensity = 0.15f;
    [SerializeField] protected float highlightMaxIntensity = 0.55f;
    [SerializeField] protected float highlightPulseSpeed = 2.5f;
    [SerializeField] protected Renderer[] highlightRenderers;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    readonly List<HighlightMaterialState> highlightMaterials = new List<HighlightMaterialState>();
    bool highlightInitialized;
    bool focused;

    protected virtual string ReplayIdentityValue => ReplayIdentity.Resolve(this, string.Empty);
    protected virtual string ReplayCategoryValue => GetType().Name.Replace("Interactable", string.Empty);
    protected virtual string ReplayItemIdValue => string.Empty;
    protected virtual string ReplayInteractionKind => ReplayCategoryValue.ToLowerInvariant() + "_interacted";
    protected virtual string ReplayStateChangeKind => ReplayCategoryValue.ToLowerInvariant() + "_changed";
    public virtual ReplayObjectState ReplayState => ReplayObjectState.Idle;
    public string ReplayTargetId => ReplayIdentityValue;
    public virtual string ReplayTargetName => gameObject.name;
    public string ReplayTargetCategory => ReplayCategoryValue;

    struct HighlightMaterialState
    {
        public Material material;
        public Color originalEmissionColor;
        public bool originallyEmissionEnabled;
    }

    public virtual string GetPromptMessage()
    {
        return promptMessage;
    }

    public void BaseInteract(GameObject interactor)
    {
        ReplayObjectState stateBefore = ReplayState;
        ReplayEventBus.Publish(this, ReplayInteractionKind, ReplayObjectState.Attempted, false, false, ReplayItemIdValue);
        Interact(interactor);
        ReplayObjectState stateAfter = ReplayState;
        if (stateAfter != stateBefore)
        {
            ReplayEventBus.Publish(this, ReplayStateChangeKind, stateAfter, true, true, ReplayItemIdValue, transform.position, transform.rotation);
        }
    }

    public virtual bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        return false;
    }
    
    protected virtual void Interact(GameObject interactor)
    {

    }

    public void SetFocused(bool isFocused)
    {
        if (focused == isFocused)
        {
            return;
        }

        focused = isFocused;

        if (!highlightOnFocus)
        {
            return;
        }

        if (focused)
        {
            EnsureHighlightMaterials();
            ApplyFocusHighlight();
        }
        else
        {
            ClearFocusHighlight();
        }
    }

    public void UpdateFocusHighlight()
    {
        if (!focused || !highlightOnFocus)
        {
            return;
        }

        ApplyFocusHighlight();
    }

    protected virtual void OnDisable()
    {
        if (focused)
        {
            SetFocused(false);
        }
    }

    void EnsureHighlightMaterials()
    {
        if (highlightInitialized)
        {
            return;
        }

        highlightInitialized = true;
        Renderer[] renderers = highlightRenderers != null && highlightRenderers.Length > 0
            ? highlightRenderers
            : GetComponentsInChildren<Renderer>(true);

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null)
            {
                continue;
            }

            Material[] materials = targetRenderer.materials;
            foreach (Material material in materials)
            {
                if (material == null || !material.HasProperty(EmissionColorId))
                {
                    continue;
                }

                highlightMaterials.Add(new HighlightMaterialState
                {
                    material = material,
                    originalEmissionColor = material.GetColor(EmissionColorId),
                    originallyEmissionEnabled = material.IsKeywordEnabled("_EMISSION")
                });
            }
        }
    }

    void ApplyFocusHighlight()
    {
        EnsureHighlightMaterials();

        float pulse = Mathf.PingPong(Time.time * highlightPulseSpeed, 1f);
        float intensity = Mathf.Lerp(highlightMinIntensity, highlightMaxIntensity, pulse);
        Color emissionColor = highlightColor * Mathf.Max(0f, intensity);

        foreach (HighlightMaterialState highlightMaterial in highlightMaterials)
        {
            if (highlightMaterial.material == null)
            {
                continue;
            }

            highlightMaterial.material.EnableKeyword("_EMISSION");
            highlightMaterial.material.SetColor(EmissionColorId, emissionColor);
        }
    }

    void ClearFocusHighlight()
    {
        foreach (HighlightMaterialState highlightMaterial in highlightMaterials)
        {
            if (highlightMaterial.material == null)
            {
                continue;
            }

            highlightMaterial.material.SetColor(EmissionColorId, highlightMaterial.originalEmissionColor);
            if (!highlightMaterial.originallyEmissionEnabled)
            {
                highlightMaterial.material.DisableKeyword("_EMISSION");
            }
        }
    }
}
