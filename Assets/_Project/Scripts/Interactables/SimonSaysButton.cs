using System.Collections;
using UnityEngine;

public enum SimonButtonColor
{
    Red = 0,
    Blue = 1,
    Green = 2,
    Yellow = 3
}

public class SimonSaysButton : Interactable
{
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

    static Texture sharedMaskSource;
    static Texture2D sharedEmissionMask;

    [Header("Simon Button")]
    [SerializeField] SimonSaysController controller;
    [SerializeField] SimonButtonColor buttonColor;
    [SerializeField, Min(0)] int buttonIndex;
    [SerializeField] Renderer buttonRenderer;
    [SerializeField] Vector3 pressedLocalOffset = new Vector3(0f, -0.01f, 0f);
    [SerializeField, Min(0f)] float cueBrightness = 6f;
    [SerializeField, Min(0f)] float focusBrightness = 0.35f;
    [SerializeField, Range(1f, 1.25f)] float cueScaleMultiplier = 1.06f;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip toneClip;
    [SerializeField, Min(20f)] float fallbackFrequency = 261.63f;

    MaterialPropertyBlock propertyBlock;
    Vector3 restingLocalPosition;
    Vector3 restingLocalScale;
    AudioClip generatedToneClip;
    Texture emissionMask;
    Material[] runtimeMaterials;
    bool visualsInitialized;
    bool buttonFocused;
    bool feedbackActive;

    public SimonButtonColor ButtonColor => buttonColor;

    protected override string ReplayIdentityValue
    {
        get
        {
            string controllerId = controller != null
                ? controller.StateId
                : ReplayIdentity.Resolve(this, string.Empty);
            return controllerId
                + ":button:"
                + buttonIndex
                + ":"
                + buttonColor.ToString().ToLowerInvariant();
        }
    }

    protected override string ReplayCategoryValue => "Puzzle";
    protected override string ReplayInteractionKind => "simon_button_pressed";
    protected override bool RecordReplayInteraction => false;
    public override ReplayObjectState ReplayState => ReplayObjectState.Idle;

    void Awake()
    {
        if (controller == null)
        {
            controller = GetComponentInParent<SimonSaysController>();
        }

        if (buttonRenderer == null)
        {
            buttonRenderer = GetComponent<Renderer>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        EnsureVisualsInitialized();

        if (toneClip == null)
        {
            generatedToneClip = SimonToneUtility.CreateSineTone(
                "Simon_" + buttonColor,
                fallbackFrequency,
                0.3f,
                0.18f);
        }
    }

    void OnDestroy()
    {
        if (generatedToneClip != null)
        {
            Destroy(generatedToneClip);
        }

        if (runtimeMaterials != null)
        {
            foreach (Material runtimeMaterial in runtimeMaterials)
            {
                if (runtimeMaterial != null)
                {
                    Destroy(runtimeMaterial);
                }
            }
        }
    }

    public override string GetPromptMessage()
    {
        return controller != null
            ? controller.GetButtonPrompt(buttonColor)
            : "Press E to use Simon Says";
    }

    public override void SetFocused(bool isFocused)
    {
        base.SetFocused(isFocused);
        buttonFocused = isFocused;
        if (feedbackActive)
        {
            return;
        }

        if (buttonFocused)
        {
            ApplyMaskedEmission(
                GetDisplayColor(buttonColor),
                focusBrightness,
                false,
                false);
        }
        else
        {
            ClearVisualPropertyBlock();
        }
    }

    public override void UpdateFocusHighlight()
    {
        // Simon buttons use a steady, color-matched focus cue instead of the
        // global pulsing yellow highlight used by other interactables.
    }

    protected override void Interact(GameObject interactor)
    {
        controller?.HandleButtonInteraction(this);
    }

    public override bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        if (replayEvent == null || replayEvent.eventKind != "simon_button_pressed")
        {
            return false;
        }

        controller?.HandleReplayButtonPress(this);
        return true;
    }

    public void RecordAcceptedPress()
    {
        ReplayEventBus.Publish(
            this,
            "simon_button_pressed",
            ReplayObjectState.Attempted,
            true,
            false);
    }

    public IEnumerator PlayCue(float duration)
    {
        SetFeedback(GetDisplayColor(buttonColor), cueBrightness, true);
        PlayTone();
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        ResetVisual();
    }

    public void SetFeedback(Color color, float intensity, bool pressed)
    {
        ApplyMaskedEmission(color, intensity, pressed, true);
    }

    public void ResetVisual()
    {
        EnsureVisualsInitialized();
        feedbackActive = false;
        transform.localPosition = restingLocalPosition;
        transform.localScale = restingLocalScale;
        if (buttonFocused)
        {
            ApplyMaskedEmission(
                GetDisplayColor(buttonColor),
                focusBrightness,
                false,
                false);
        }
        else
        {
            ClearVisualPropertyBlock();
        }
    }

    public void PlayTone()
    {
        AudioClip clip = toneClip != null ? toneClip : generatedToneClip;
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public void Configure(
        SimonSaysController targetController,
        SimonButtonColor color,
        Renderer targetRenderer,
        AudioSource targetAudioSource,
        float frequency)
    {
        controller = targetController;
        buttonColor = color;
        buttonIndex = (int)color;
        buttonRenderer = targetRenderer;
        audioSource = targetAudioSource;
        fallbackFrequency = frequency;
        promptMessage = "Press E to choose " + color;
        highlightOnFocus = false;
        highlightColor = GetDisplayColor(color);
        highlightRenderers = new Renderer[0];
    }

    void ApplyMaskedEmission(
        Color color,
        float intensity,
        bool pressed,
        bool isFeedback)
    {
        EnsureVisualsInitialized();
        feedbackActive = isFeedback;
        transform.localPosition = pressed
            ? restingLocalPosition + pressedLocalOffset
            : restingLocalPosition;
        transform.localScale = pressed
            ? restingLocalScale * cueScaleMultiplier
            : restingLocalScale;

        if (buttonRenderer == null)
        {
            return;
        }

        propertyBlock.Clear();
        if (emissionMask != null)
        {
            propertyBlock.SetTexture(EmissionMapId, emissionMask);
        }
        propertyBlock.SetColor(
            EmissionColorId,
            color * Mathf.Max(0f, intensity));
        buttonRenderer.SetPropertyBlock(propertyBlock);
    }

    void ClearVisualPropertyBlock()
    {
        EnsureVisualsInitialized();
        buttonRenderer?.SetPropertyBlock(null);
    }

    void EnsureVisualsInitialized()
    {
        if (visualsInitialized)
        {
            return;
        }

        visualsInitialized = true;
        if (buttonRenderer == null)
        {
            buttonRenderer = GetComponent<Renderer>();
        }

        propertyBlock = new MaterialPropertyBlock();
        restingLocalPosition = transform.localPosition;
        restingLocalScale = transform.localScale;
        emissionMask = GetOrCreateEmissionMask(buttonRenderer);
        CreateRuntimeMaterials();
    }

    void CreateRuntimeMaterials()
    {
        if (buttonRenderer == null)
        {
            return;
        }

        // MaterialPropertyBlock cannot enable shader keywords. URP Lit ignores
        // _EmissionColor entirely unless _EMISSION is enabled, so use renderer-
        // local material instances while leaving the imported shared asset intact.
        runtimeMaterials = buttonRenderer.materials;
        foreach (Material runtimeMaterial in runtimeMaterials)
        {
            if (runtimeMaterial == null)
            {
                continue;
            }

            if (runtimeMaterial.HasProperty(EmissionMapId)
                && emissionMask != null)
            {
                runtimeMaterial.SetTexture(EmissionMapId, emissionMask);
            }

            if (runtimeMaterial.HasProperty(EmissionColorId))
            {
                runtimeMaterial.EnableKeyword("_EMISSION");
                runtimeMaterial.SetColor(EmissionColorId, Color.black);
            }
        }
    }

    static Texture GetOrCreateEmissionMask(Renderer targetRenderer)
    {
        Material material = targetRenderer != null
            ? targetRenderer.sharedMaterial
            : null;
        if (material == null)
        {
            return null;
        }

        Texture source = material.HasProperty(BaseMapId)
            ? material.GetTexture(BaseMapId)
            : null;
        if (source == null && material.HasProperty(MainTextureId))
        {
            source = material.GetTexture(MainTextureId);
        }
        if (source == null)
        {
            return null;
        }

        if (sharedEmissionMask == null || sharedMaskSource != source)
        {
            sharedMaskSource = source;
            sharedEmissionMask = BuildColoredPixelMask(source);
        }

        return sharedEmissionMask;
    }

    static Texture2D BuildColoredPixelMask(Texture source)
    {
        int width = Mathf.Max(1, source.width);
        int height = Mathf.Max(1, source.height);
        RenderTexture temporary = RenderTexture.GetTemporary(
            width,
            height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);
        RenderTexture previous = RenderTexture.active;
        Texture2D readable = null;

        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            readable = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                true);
            readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            readable.Apply(false, false);

            Color32[] sourcePixels = readable.GetPixels32();
            Color32[] maskPixels = new Color32[sourcePixels.Length];
            for (int index = 0; index < sourcePixels.Length; index++)
            {
                Color32 pixel = sourcePixels[index];
                byte maximum = System.Math.Max(
                    pixel.r,
                    System.Math.Max(pixel.g, pixel.b));
                byte minimum = System.Math.Min(
                    pixel.r,
                    System.Math.Min(pixel.g, pixel.b));
                bool isColoredSquare = pixel.a > 16
                    && maximum > 40
                    && maximum - minimum > 24;
                byte value = isColoredSquare ? (byte)255 : (byte)0;
                maskPixels[index] = new Color32(value, value, value, value);
            }

            Texture2D mask = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = source.name + "_SimonEmissionMask",
                hideFlags = HideFlags.DontSave,
                filterMode = source.filterMode,
                wrapMode = source.wrapMode
            };
            mask.SetPixels32(maskPixels);
            mask.Apply(false, true);
            return mask;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            if (readable != null)
            {
                Destroy(readable);
            }
        }
    }

    public static Color GetDisplayColor(SimonButtonColor color)
    {
        switch (color)
        {
            case SimonButtonColor.Red:
                return new Color(1f, 0.04f, 0.02f, 1f);
            case SimonButtonColor.Blue:
                return new Color(0.03f, 0.2f, 1f, 1f);
            case SimonButtonColor.Green:
                return new Color(0.03f, 1f, 0.08f, 1f);
            case SimonButtonColor.Yellow:
                return new Color(1f, 0.75f, 0.02f, 1f);
            default:
                return Color.white;
        }
    }
}

public static class SimonToneUtility
{
    const int SampleRate = 44100;

    public static AudioClip CreateSineTone(
        string clipName,
        float frequency,
        float duration,
        float volume)
    {
        int sampleCount = Mathf.Max(
            1,
            Mathf.CeilToInt(SampleRate * Mathf.Max(0.02f, duration)));
        float[] samples = new float[sampleCount];
        int fadeSamples = Mathf.Min(
            sampleCount / 2,
            Mathf.CeilToInt(SampleRate * 0.015f));

        for (int i = 0; i < sampleCount; i++)
        {
            float envelope = 1f;
            if (i < fadeSamples)
            {
                envelope = (float)i / Mathf.Max(1, fadeSamples);
            }
            else if (i >= sampleCount - fadeSamples)
            {
                envelope = (float)(sampleCount - i - 1) / Mathf.Max(1, fadeSamples);
            }

            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / SampleRate)
                * Mathf.Clamp01(volume)
                * Mathf.Clamp01(envelope);
        }

        AudioClip clip = AudioClip.Create(
            clipName,
            sampleCount,
            1,
            SampleRate,
            false);
        clip.SetData(samples, 0);
        return clip;
    }
}
