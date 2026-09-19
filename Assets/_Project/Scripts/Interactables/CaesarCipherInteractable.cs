using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public sealed class CaesarCipherInteractable : Interactable, IDataPersistence, IReplayObject, IReplayHandoff, IReplayTimeline
{
    public enum RingSelection
    {
        Inner = 0,
        Outer = 1
    }

    [Header("Wheel")]
    [SerializeField] Transform wheelCenter;
    [SerializeField] Transform innerRing;
    [SerializeField] Transform outerRing;
    [SerializeField] Vector3 parentLocalRotationAxis = Vector3.forward;
    [SerializeField] float innerRotationDirection = 1f;
    [SerializeField] float outerRotationDirection = 1f;
    [SerializeField, Range(0, CaesarCipherMath.NotchCount - 1)] int innerIndex = 13;
    [SerializeField, Range(0, CaesarCipherMath.NotchCount - 1)] int outerIndex = 14;
    [SerializeField] RingSelection selectedRing = RingSelection.Inner;

    [Header("Top Letter Calibration")]
    [Tooltip("Symbol physically at screen-top on the inner ring when its rotation index is zero. The imported model starts on O.")]
    [SerializeField, Range(0, CaesarCipherMath.NotchCount - 1)] int innerTopSymbolAtZero = 14;
    [Tooltip("Symbol physically at screen-top on the outer ring when its rotation index is zero. The imported model starts on N.")]
    [SerializeField, Range(0, CaesarCipherMath.NotchCount - 1)] int outerTopSymbolAtZero = 13;

    [Header("Colliders")]
    [SerializeField] Collider worldInteractionCollider;
    [SerializeField] Collider innerRingCollider;
    [SerializeField] Collider outerRingCollider;

    [Header("Inspection Presentation")]
    [SerializeField] Renderer[] inspectionRenderers;
    [SerializeField] Camera inspectionCamera;
    [SerializeField] string inspectionLayerName = "CipherInspection";
    [SerializeField] Vector3 inspectionLocalViewNormal = Vector3.up;
    [SerializeField, Min(1f)] float inspectionPadding = 1.2f;
    [SerializeField, Min(0.01f)] float snapDuration = 0.12f;
    [SerializeField] GameObject inspectionHud;
    [SerializeField] TMP_Text selectedRingText;
    [SerializeField] TMP_Text controlsText;
    [SerializeField] GameObject pinnedCluePanel;
    [SerializeField] TMP_Text pinnedClueText;
    [SerializeField] Image pinnedClueImage;

    [Header("Pinned Note")]
    [SerializeField] NoteInteractable sourceNote;
    [SerializeField, TextArea] string undiscoveredNoteMessage = "Find the related note first.";

    [Header("Player References")]
    [SerializeField] InputManager inputManager;
    [SerializeField] PlayerInteract playerInteract;
    [SerializeField] InventoryUI inventoryUI;
    [SerializeField] PlayerUI playerUI;
    [SerializeField] GameObject crosshair;

    [Header("Save and Replay")]
    [SerializeField] string id;

    Quaternion innerBaselineRotation = Quaternion.identity;
    Quaternion outerBaselineRotation = Quaternion.identity;
    bool baselinesCaptured;
    bool isInspecting;
    bool pointerDragging;
    RingSelection draggedRing;
    float dragStartPointerAngle;
    float dragStartIndex;
    float displayedInnerIndex;
    Quaternion snapStart, snapTarget;
    float snapElapsed;
    bool snapping;
    int openedFrame = -1;
    Coroutine innerSnapCoroutine;
    Coroutine outerSnapCoroutine;

    bool inputSessionActive;
    bool playerInteractWasEnabled;
    bool playerControlWasLocked;
    bool cursorWasVisible;
    CursorLockMode cursorWasLocked;
    bool hudHidden;
    bool inventoryWasVisible;
    bool promptWasVisible;
    bool crosshairWasVisible;
    bool wasPlaybackActive;

    Camera mainCamera;
    int mainCameraOriginalCullingMask;
    bool mainCameraMaskChanged;
    UniversalAdditionalCameraData mainCameraData;
    bool runtimeInspectionCamera;
    bool runtimeInspectionHud;
    readonly Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();

    string StateId => ReplayIdentity.Resolve(this, id);
    protected override string ReplayIdentityValue => StateId;
    protected override string ReplayCategoryValue => "CaesarCipher";
    protected override string ReplayInteractionKind => "caesar_inspection_interacted";
    protected override string ReplayStateChangeKind => isInspecting ? "caesar_inspection_opened" : "caesar_inspection_closed";
    protected override bool RecordReplayInteraction => false;
    public override ReplayObjectState ReplayState => isInspecting ? ReplayObjectState.Activated : ReplayObjectState.Idle;
    public bool IsInspecting => isInspecting;
    public int InnerIndex => innerIndex;
    public int OuterIndex => outerIndex;
    public RingSelection SelectedRing => selectedRing;
    public char InnerTopSymbol => TopSymbol(RingSelection.Inner, innerIndex);
    public char OuterTopSymbol => TopSymbol(RingSelection.Outer, outerIndex);

    [ContextMenu("Generate guid for id")]
    void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying && gameObject.scene.IsValid() &&
            !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject) && string.IsNullOrWhiteSpace(id))
        {
            GenerateGuid();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    void Awake()
    {
        selectedRing = RingSelection.Inner;
        outerIndex = 14;
        CaptureBaselines();
        ResolveModelReferences();
        SetRingColliderState(false);
        ApplyRingRotation(RingSelection.Inner, innerIndex, false);
        ApplyRingRotation(RingSelection.Outer, outerIndex, false);
        SetInspectionHudVisible(false);
        wasPlaybackActive = ReplayManager.IsPlaybackActive();

        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    void OnEnable()
    {
        PauseManager.PauseStarting += HandlePauseStarting;
    }

    public void OnTakeover() { if (isInspecting) { AcquireInputSession(); pointerDragging = false; } }

    void Update()
    {
        bool playbackActive = ReplayManager.IsPlaybackActive();
        if (!playbackActive && !(ReplayManager.instance?.IsHandoffFrame ?? false)) AdvanceReplayPresentation(Time.deltaTime);
        if (!wasPlaybackActive && playbackActive && inputSessionActive)
        {
            ReleaseInputSession();
        }
        if (wasPlaybackActive && !playbackActive && isInspecting)
        {
            bool takeoverActive = ReplayManager.instance != null && ReplayManager.instance.CurrentState == ReplayManager.State.Takeover;
            if (takeoverActive)
            {
                AcquireInputSession();
                pointerDragging = false;
            }
            else
            {
                SetInspectionState(false, false);
            }
        }
        wasPlaybackActive = playbackActive;

        if (playbackActive || !isInspecting)
        {
            return;
        }

        if (!inputSessionActive && ReplayManager.instance != null && ReplayManager.instance.CurrentState == ReplayManager.State.Takeover)
        {
            AcquireInputSession();
        }

        if (!inputSessionActive || Time.timeScale == 0f || (inputManager?.GameplayInputSuppressed ?? false) || (PauseManager.Instance != null && PauseManager.Instance.IsPaused))
        {
            return;
        }

        ProcessCloseInput();
        if (!isInspecting)
        {
            return;
        }

        ProcessStepInput();
        ProcessPointerInput();
    }

    void LateUpdate()
    {
        if (isInspecting)
        {
            RefreshPinnedClue();
            UpdateHudText();
        }
    }

    public override string GetPromptMessage()
    {
        return isInspecting ? "Use the cipher wheel" : "Press E to inspect cipher";
    }

    protected override void Interact(GameObject interactor)
    {
        if (isInspecting)
        {
            CloseInspection(true);
            return;
        }

        ResolvePlayerReferences(interactor);
        OpenInspection(true);
    }

    public void LoadData(GameData data)
    {
        LoadCipherState(data, false);
    }

    public void SaveData(ref GameData data)
    {
        SaveCipherState(ref data, false);
    }

    public void LoadSnapshot(GameData data)
    {
        LoadCipherState(data, true);
    }

    public void SaveSnapshot(ref GameData data)
    {
        SaveCipherState(ref data, true);
    }

    public override bool ApplyReplayEvent(ReplayEventData replayEvent)
    {
        if (replayEvent == null)
        {
            return false;
        }

        switch (replayEvent.eventKind)
        {
            case "caesar_inspection_opened":
                SetInspectionState(true, false);
                return true;
            case "caesar_inspection_closed":
                SetInspectionState(false, false);
                return true;
            case "caesar_ring_selected":
                SetSelectedRing(RingSelection.Inner, false);
                return true;
            case "caesar_ring_rotated":
                if (ParseRing(replayEvent.textValue) == RingSelection.Inner)
                {
                    SetRingIndex(RingSelection.Inner, Mathf.RoundToInt(replayEvent.numberValue), true, false);
                }
                return true;
            default:
                return false;
        }
    }

    void OpenInspection(bool acquireInput)
    {
        if (isInspecting)
        {
            return;
        }

        SetFocused(false);
        isInspecting = true;
        openedFrame = Time.frameCount;
        SetInspectionVisuals(true);
        if (acquireInput)
        {
            AcquireInputSession();
        }
        RefreshPinnedClue();
        UpdateHudText();
    }

    void CloseInspection(bool recordEvent)
    {
        if (!isInspecting)
        {
            return;
        }

        pointerDragging = false;
        isInspecting = false;
        ReleaseInputSession();
        SetInspectionVisuals(false);

        if (recordEvent)
        {
            ReplayEventBus.Publish(this, "caesar_inspection_closed", ReplayObjectState.Idle, true, true);
        }
    }

    void SetInspectionState(bool visible, bool acquireInput)
    {
        if (visible)
        {
            if (!isInspecting)
            {
                OpenInspection(acquireInput);
            }
            else
            {
                RefreshPinnedClue();
                UpdateHudText();
            }
            return;
        }

        if (isInspecting)
        {
            CloseInspection(false);
        }
    }

    void ProcessCloseInput()
    {
        if (Time.frameCount == openedFrame)
        {
            return;
        }

        bool keyboardClose = Keyboard.current != null &&
            (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
        bool gamepadClose = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        if (keyboardClose || gamepadClose)
        {
            CloseInspection(true);
        }
    }

    void ProcessStepInput()
    {
        int direction = 0;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                direction = 1;
            }
            else if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                direction = -1;
            }
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.dpad.right.wasPressedThisFrame)
            {
                direction = 1;
            }
            else if (Gamepad.current.dpad.left.wasPressedThisFrame)
            {
                direction = -1;
            }
        }

        if (direction != 0)
        {
            SetRingIndex(RingSelection.Inner, innerIndex + direction, true, true);
        }
    }

    void ProcessPointerInput()
    {
        if (!TryGetPointerState(out Vector2 position, out bool pressed, out bool held, out bool released))
        {
            return;
        }

        if (pressed)
        {
            BeginPointerDrag(position);
        }

        if (pointerDragging && held)
        {
            UpdatePointerDrag(position);
        }

        if (pointerDragging && released)
        {
            EndPointerDrag(position);
        }
    }

    bool TryGetPointerState(out Vector2 position, out bool pressed, out bool held, out bool released)
    {
        position = Vector2.zero;
        pressed = false;
        held = false;
        released = false;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            pressed = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            held = true;
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            released = true;
            return true;
        }

        if (Mouse.current == null)
        {
            return false;
        }

        position = Mouse.current.position.ReadValue();
        pressed = Mouse.current.leftButton.wasPressedThisFrame;
        held = Mouse.current.leftButton.isPressed;
        released = Mouse.current.leftButton.wasReleasedThisFrame;
        return pressed || held || released;
    }

    void BeginPointerDrag(Vector2 screenPosition)
    {
        if (inspectionCamera == null)
        {
            return;
        }

        Ray ray = inspectionCamera.ScreenPointToRay(screenPosition);
        int layer = ResolveInspectionLayer();
        int mask = layer >= 0 ? 1 << layer : Physics.DefaultRaycastLayers;
        RaycastHit[] hits = Physics.RaycastAll(ray, inspectionCamera.farClipPlane, mask, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RingSelection? hitRing = null;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == innerRingCollider)
            {
                hitRing = RingSelection.Inner;
                break;
            }
        }

        if (!hitRing.HasValue)
        {
            return;
        }

        SetSelectedRing(hitRing.Value, true);
        StopRingSnap(hitRing.Value);
        draggedRing = hitRing.Value;
        dragStartIndex = displayedInnerIndex;
        dragStartPointerAngle = PointerAngle(screenPosition);
        pointerDragging = true;
    }

    void UpdatePointerDrag(Vector2 screenPosition)
    {
        float direction = RotationDirection(draggedRing);
        float pointerDelta = Mathf.DeltaAngle(dragStartPointerAngle, PointerAngle(screenPosition));
        float previewIndex = dragStartIndex + pointerDelta / (CaesarCipherMath.DegreesPerNotch * direction);
        ApplyRingRotation(draggedRing, previewIndex, false);
        UpdateHudText(draggedRing, previewIndex);
    }

    void EndPointerDrag(Vector2 screenPosition)
    {
        float direction = RotationDirection(draggedRing);
        float pointerDelta = Mathf.DeltaAngle(dragStartPointerAngle, PointerAngle(screenPosition));
        int snappedIndex = Mathf.RoundToInt(dragStartIndex + pointerDelta / (CaesarCipherMath.DegreesPerNotch * direction));
        pointerDragging = false;
        SetRingIndex(draggedRing, snappedIndex, true, true);
    }

    float PointerAngle(Vector2 pointerPosition)
    {
        Vector3 center = WheelWorldCenter();
        Vector3 screenCenter = inspectionCamera != null ? inspectionCamera.WorldToScreenPoint(center) : Vector3.zero;
        Vector2 offset = pointerPosition - new Vector2(screenCenter.x, screenCenter.y);
        return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
    }

    void SetSelectedRing(RingSelection ring, bool recordEvent)
    {
        selectedRing = RingSelection.Inner;
        UpdateHudText();
    }

    void SetRingIndex(RingSelection ring, int value, bool animate, bool recordEvent)
    {
        int normalized = CaesarCipherMath.NormalizeIndex(value);
        int current = ring == RingSelection.Inner ? innerIndex : outerIndex;
        if (ring == RingSelection.Inner)
        {
            innerIndex = normalized;
        }
        else
        {
            outerIndex = normalized;
        }

        ApplyRingRotation(ring, normalized, animate);
        UpdateHudText();

        if (recordEvent && current != normalized)
        {
            ReplayEventBus.Publish(this, "caesar_ring_rotated", ReplayObjectState.Activated, true, true,
                textValue: RingName(ring), numberValue: normalized);
        }
    }

    void ApplyRingRotation(RingSelection ring, float index, bool animate)
    {
        CaptureBaselines();
        if (ring == RingSelection.Inner) displayedInnerIndex = index;
        Transform target = ring == RingSelection.Inner ? innerRing : outerRing;
        if (target == null)
        {
            return;
        }

        Quaternion baseline = ring == RingSelection.Inner ? innerBaselineRotation : outerBaselineRotation;
        Quaternion destination = CaesarCipherMath.RotationForIndex(baseline, parentLocalRotationAxis, index, RotationDirection(ring));
        StopRingSnap(ring);
        if (!animate || !isActiveAndEnabled || snapDuration <= 0f)
        {
            target.localRotation = destination;
            return;
        }

        if (ring == RingSelection.Inner)
        {
            snapStart = target.localRotation; snapTarget = destination; snapElapsed = 0f; snapping = true;
        }
        else target.localRotation = destination;
    }

    public void AdvanceReplayPresentation(float seconds)
    {
        if (!snapping || innerRing == null) return;
        snapElapsed = Mathf.Min(snapDuration, snapElapsed + seconds);
        float t = Mathf.SmoothStep(0f, 1f, snapElapsed / Mathf.Max(0.001f, snapDuration));
        innerRing.localRotation = Quaternion.Slerp(snapStart, snapTarget, t);
        if (snapElapsed >= snapDuration) snapping = false;
    }

    void StopRingSnap(RingSelection ring)
    {
        if (ring == RingSelection.Inner) snapping = false;
        Coroutine coroutine = ring == RingSelection.Inner ? innerSnapCoroutine : outerSnapCoroutine;
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }
        if (ring == RingSelection.Inner)
        {
            innerSnapCoroutine = null;
        }
        else
        {
            outerSnapCoroutine = null;
        }
    }

    float RotationDirection(RingSelection ring)
    {
        float direction = ring == RingSelection.Inner ? innerRotationDirection : outerRotationDirection;
        return Mathf.Approximately(direction, 0f) ? 1f : Mathf.Sign(direction);
    }

    void CaptureBaselines()
    {
        if (baselinesCaptured)
        {
            return;
        }

        if (innerRing != null)
        {
            innerBaselineRotation = innerRing.localRotation;
        }
        if (outerRing != null)
        {
            outerBaselineRotation = outerRing.localRotation;
        }
        baselinesCaptured = true;
    }

    void ResolveModelReferences()
    {
        if (wheelCenter == null)
        {
            wheelCenter = transform;
        }
        if (worldInteractionCollider == null)
        {
            worldInteractionCollider = GetComponent<Collider>();
        }
        if (inspectionRenderers == null || inspectionRenderers.Length == 0)
        {
            inspectionRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    void SetInspectionVisuals(bool visible)
    {
        if (visible)
        {
            ResolveModelReferences();
            ResolvePlayerReferences(null);
            EnsureInspectionCamera();
            EnsureInspectionHud();
            int inspectionLayer = ResolveInspectionLayer();
            originalLayers.Clear();
            if (inspectionLayer >= 0)
            {
                foreach (Renderer targetRenderer in inspectionRenderers)
                {
                    RememberAndSetLayer(targetRenderer != null ? targetRenderer.gameObject : null, inspectionLayer);
                }
                RememberAndSetLayer(innerRingCollider != null ? innerRingCollider.gameObject : null, inspectionLayer);
                RememberAndSetLayer(outerRingCollider != null ? outerRingCollider.gameObject : null, inspectionLayer);
            }

            if (mainCamera != null && inspectionLayer >= 0)
            {
                mainCameraOriginalCullingMask = mainCamera.cullingMask;
                mainCamera.cullingMask &= ~(1 << inspectionLayer);
                mainCameraMaskChanged = true;
            }

            SetRingColliderState(true);
            if (worldInteractionCollider != null)
            {
                worldInteractionCollider.enabled = false;
            }
            ConfigureInspectionCamera();
            SetInspectionHudVisible(true);
            SetGameplayHudVisible(false);
            return;
        }

        RestoreOriginalLayers();
        SetRingColliderState(false);
        if (worldInteractionCollider != null)
        {
            worldInteractionCollider.enabled = true;
        }
        if (inspectionCamera != null)
        {
            inspectionCamera.enabled = false;
        }
        if (mainCamera != null && mainCameraMaskChanged)
        {
            mainCamera.cullingMask = mainCameraOriginalCullingMask;
            mainCameraMaskChanged = false;
        }
        SetInspectionHudVisible(false);
        SetGameplayHudVisible(true);
    }

    void EnsureInspectionCamera()
    {
        if (mainCamera == null)
        {
            PlayerLook playerLook = inputManager != null ? inputManager.GetComponent<PlayerLook>() : null;
            mainCamera = playerLook != null ? playerLook.cam : Camera.main;
        }

        if (inspectionCamera == null)
        {
            GameObject cameraObject = new GameObject("Caesar Cipher Inspection Camera");
            cameraObject.transform.SetParent(transform, true);
            inspectionCamera = cameraObject.AddComponent<Camera>();
            runtimeInspectionCamera = true;
        }

        inspectionCamera.enabled = false;
        inspectionCamera.orthographic = true;
        inspectionCamera.clearFlags = CameraClearFlags.Depth;
        inspectionCamera.depth = mainCamera != null ? mainCamera.depth + 1f : 1f;
        inspectionCamera.allowHDR = mainCamera == null || mainCamera.allowHDR;
        inspectionCamera.allowMSAA = mainCamera == null || mainCamera.allowMSAA;
        inspectionCamera.nearClipPlane = 0.01f;

        int inspectionLayer = ResolveInspectionLayer();
        inspectionCamera.cullingMask = inspectionLayer >= 0 ? 1 << inspectionLayer : 0;

        UniversalAdditionalCameraData overlayData = inspectionCamera.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay;

        if (mainCamera != null)
        {
            mainCameraData = mainCamera.GetUniversalAdditionalCameraData();
            List<Camera> cameraStack = mainCameraData.cameraStack;
            if (cameraStack != null && !cameraStack.Contains(inspectionCamera))
            {
                cameraStack.Add(inspectionCamera);
            }
        }
    }

    void ConfigureInspectionCamera()
    {
        if (inspectionCamera == null)
        {
            return;
        }

        Bounds bounds = CalculateInspectionBounds();
        Vector3 center = bounds.center;
        Vector3 viewNormal = transform.TransformDirection(
            inspectionLocalViewNormal.sqrMagnitude > 0.0001f ? inspectionLocalViewNormal.normalized : Vector3.up);
        if (mainCamera != null && Vector3.Dot(viewNormal, mainCamera.transform.position - center) < 0f)
        {
            viewNormal = -viewNormal;
        }
        Vector3 viewDirection = -viewNormal.normalized;

        float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
        inspectionCamera.transform.position = center + viewNormal * (radius * 2f + 1f);
        Vector3 up = Vector3.ProjectOnPlane(transform.forward, viewDirection).normalized;
        if (up.sqrMagnitude < 0.001f)
        {
            up = Vector3.ProjectOnPlane(mainCamera != null ? mainCamera.transform.up : Vector3.forward, viewDirection).normalized;
        }
        if (up.sqrMagnitude < 0.001f)
        {
            up = Vector3.ProjectOnPlane(Vector3.right, viewDirection).normalized;
        }
        inspectionCamera.transform.rotation = Quaternion.LookRotation(viewDirection, up);

        float maxX = 0f;
        float maxY = 0f;
        foreach (Vector3 corner in BoundsCorners(bounds))
        {
            Vector3 offset = corner - center;
            maxX = Mathf.Max(maxX, Mathf.Abs(Vector3.Dot(offset, inspectionCamera.transform.right)));
            maxY = Mathf.Max(maxY, Mathf.Abs(Vector3.Dot(offset, inspectionCamera.transform.up)));
        }
        float aspect = Mathf.Max(0.1f, inspectionCamera.aspect);
        bool reservePinnedClueSpace = sourceNote != null;
        float usableWidthFraction = reservePinnedClueSpace ? 0.6f : 1f;
        inspectionCamera.orthographicSize = Mathf.Max(0.1f,
            Mathf.Max(maxY, maxX / (aspect * usableWidthFraction)) * inspectionPadding);
        if (reservePinnedClueSpace)
        {
            const float wheelViewportCenterX = 0.36f;
            float horizontalShift = (0.5f - wheelViewportCenterX) * 2f * inspectionCamera.orthographicSize * aspect;
            inspectionCamera.transform.position += inspectionCamera.transform.right * horizontalShift;
        }
        inspectionCamera.farClipPlane = radius * 6f + 10f;
        inspectionCamera.enabled = true;
    }

    Bounds CalculateInspectionBounds()
    {
        bool found = false;
        Bounds bounds = new Bounds(WheelWorldCenter(), Vector3.one * 0.1f);
        foreach (Renderer targetRenderer in inspectionRenderers)
        {
            if (targetRenderer == null)
            {
                continue;
            }
            if (!found)
            {
                bounds = targetRenderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(targetRenderer.bounds);
            }
        }
        return bounds;
    }

    static IEnumerable<Vector3> BoundsCorners(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int z = 0; z <= 1; z++)
                {
                    yield return new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                }
            }
        }
    }

    int ResolveInspectionLayer()
    {
        int layer = LayerMask.NameToLayer(inspectionLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"Layer '{inspectionLayerName}' is missing. Add it before using the Caesar cipher inspection view.", this);
        }
        return layer;
    }

    void RememberAndSetLayer(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }
        if (!originalLayers.ContainsKey(target))
        {
            originalLayers.Add(target, target.layer);
        }
        target.layer = layer;
    }

    void RestoreOriginalLayers()
    {
        foreach (KeyValuePair<GameObject, int> layerState in originalLayers)
        {
            if (layerState.Key != null)
            {
                layerState.Key.layer = layerState.Value;
            }
        }
        originalLayers.Clear();
    }

    void SetRingColliderState(bool enabledState)
    {
        if (innerRingCollider != null && innerRingCollider != worldInteractionCollider)
        {
            innerRingCollider.enabled = enabledState;
        }
        if (outerRingCollider != null && outerRingCollider != worldInteractionCollider)
        {
            outerRingCollider.enabled = false;
        }
    }

    void AcquireInputSession()
    {
        if (inputSessionActive)
        {
            return;
        }
        ResolvePlayerReferences(null);
        playerControlWasLocked = inputManager != null && inputManager.PlayerControlLocked;
        playerInteractWasEnabled = playerInteract != null && playerInteract.enabled;
        cursorWasVisible = Cursor.visible;
        cursorWasLocked = Cursor.lockState;

        if (inputManager != null) inputManager.AcquireControl(this);
        if (playerInteract != null)
        {
            playerInteract.enabled = false;
        }
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        inputSessionActive = true;
    }

    void ReleaseInputSession()
    {
        if (!inputSessionActive)
        {
            return;
        }
        if (inputManager != null)
        {
            inputManager.ReleaseControl(this);
        }
        if (playerInteract != null)
        {
            playerInteract.enabled = playerInteractWasEnabled;
        }
        Cursor.visible = cursorWasVisible;
        Cursor.lockState = cursorWasLocked;
        inputSessionActive = false;
    }

    void ResolvePlayerReferences(GameObject interactor)
    {
        if (interactor != null)
        {
            inputManager = inputManager != null ? inputManager : interactor.GetComponent<InputManager>();
            playerInteract = playerInteract != null ? playerInteract : interactor.GetComponent<PlayerInteract>();
            playerUI = playerUI != null ? playerUI : interactor.GetComponent<PlayerUI>();
        }

        if (inputManager == null) inputManager = FindAnyObjectByType<InputManager>();
        if (playerInteract == null) playerInteract = FindAnyObjectByType<PlayerInteract>();
        if (playerUI == null) playerUI = FindAnyObjectByType<PlayerUI>();
        if (inventoryUI == null) inventoryUI = FindAnyObjectByType<InventoryUI>();
        if (crosshair == null) crosshair = GameObject.Find("Crosshair");
        if (mainCamera == null)
        {
            PlayerLook look = inputManager != null ? inputManager.GetComponent<PlayerLook>() : null;
            mainCamera = look != null ? look.cam : Camera.main;
        }
    }

    void SetGameplayHudVisible(bool visible)
    {
        if (!visible && !hudHidden)
        {
            inventoryWasVisible = inventoryUI == null || inventoryUI.IsVisible;
            promptWasVisible = playerUI == null || playerUI.PromptVisible;
            crosshairWasVisible = crosshair == null || crosshair.activeSelf;
            if (inventoryUI != null) inventoryUI.SetVisible(false);
            if (playerUI != null) playerUI.SetPromptVisible(false);
            if (crosshair != null) crosshair.SetActive(false);
            hudHidden = true;
            return;
        }

        if (visible && hudHidden)
        {
            if (inventoryUI != null) inventoryUI.SetVisible(inventoryWasVisible);
            if (playerUI != null) playerUI.SetPromptVisible(promptWasVisible);
            if (crosshair != null) crosshair.SetActive(crosshairWasVisible);
            hudHidden = false;
        }
    }

    void EnsureInspectionHud()
    {
        if (inspectionHud != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Caesar Cipher HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        controlsText = CreateHudText("Controls", canvasObject.transform, new Vector2(0.2f, 0.01f), new Vector2(0.8f, 0.12f), 25f, TextAlignmentOptions.Center);

        GameObject panel = new GameObject("Pinned Clue", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        panel.layer = canvasObject.layer;
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.72f, 0.17f);
        panelRect.anchorMax = new Vector2(0.98f, 0.83f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.32f);
        panelImage.raycastTarget = false;

        pinnedClueImage = CreateHudImage("Clue Image", panel.transform, new Vector2(0.06f, 0.52f), new Vector2(0.94f, 0.94f));
        pinnedClueText = CreateHudText("Clue Text", panel.transform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.5f), 28f, TextAlignmentOptions.TopLeft);

        inspectionHud = canvasObject;
        pinnedCluePanel = panel;
        runtimeInspectionHud = true;
        inspectionHud.SetActive(false);
    }

    static TMP_Text CreateHudText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        textObject.layer = parent.gameObject.layer;
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    static Image CreateHudImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        imageObject.layer = parent.gameObject.layer;
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    void SetInspectionHudVisible(bool visible)
    {
        if (inspectionHud != null)
        {
            inspectionHud.SetActive(visible);
        }
    }

    void UpdateHudText(RingSelection? previewRing = null, float previewIndex = 0f)
    {
        if (selectedRingText != null)
        {
            selectedRingText.gameObject.SetActive(false);
        }
        if (controlsText != null)
        {
            controlsText.text = "Drag the inner ring • A/D or ←/→ rotates • E/Esc/B exits";
        }
    }

    char TopSymbol(RingSelection ring, float index)
    {
        int baselineTopSymbol = ring == RingSelection.Inner ? innerTopSymbolAtZero : outerTopSymbolAtZero;
        return CaesarCipherMath.TopSymbol(baselineTopSymbol, index, RotationDirection(ring));
    }

    void RefreshPinnedClue()
    {
        if (pinnedCluePanel == null)
        {
            return;
        }

        pinnedCluePanel.SetActive(sourceNote != null);
        if (sourceNote == null)
        {
            return;
        }

        bool discovered = sourceNote.HasBeenRead;
        if (pinnedClueText != null)
        {
            pinnedClueText.text = discovered ? sourceNote.ClueText : undiscoveredNoteMessage;
        }
        if (pinnedClueImage != null)
        {
            Sprite sprite = discovered ? sourceNote.ClueSprite : null;
            pinnedClueImage.sprite = sprite;
            pinnedClueImage.gameObject.SetActive(sprite != null);
        }
    }

    void SaveCipherState(ref GameData data, bool includeInspectionState)
    {
        if (data == null || string.IsNullOrEmpty(StateId))
        {
            return;
        }
        if (data.caesarCipherStates == null)
        {
            data.caesarCipherStates = new List<CaesarCipherSaveData>();
        }

        CaesarCipherSaveData saved = data.caesarCipherStates.Find(state => state.id == StateId);
        if (saved == null)
        {
            saved = new CaesarCipherSaveData { id = StateId };
            data.caesarCipherStates.Add(saved);
        }
        saved.innerIndex = innerIndex;
        saved.outerIndex = 14;
        saved.selectedRing = (int)RingSelection.Inner;
        saved.isInspecting = includeInspectionState && isInspecting;
        saved.hasPresentation = includeInspectionState;
        saved.innerRotation = innerRing != null ? innerRing.localRotation : Quaternion.identity;
        saved.snapStart = snapStart; saved.snapTarget = snapTarget; saved.snapElapsed = snapElapsed;
        saved.snapping = snapping; saved.displayedInnerIndex = displayedInnerIndex;
    }

    void LoadCipherState(GameData data, bool restoreInspectionState)
    {
        if (data == null || string.IsNullOrEmpty(StateId) || data.caesarCipherStates == null)
        {
            if (!restoreInspectionState)
            {
                SetInspectionState(false, false);
            }
            return;
        }

        CaesarCipherSaveData saved = data.caesarCipherStates.Find(state => state.id == StateId);
        if (saved == null)
        {
            if (!restoreInspectionState)
            {
                SetInspectionState(false, false);
            }
            return;
        }

        selectedRing = RingSelection.Inner;
        outerIndex = 14;
        bool animate = restoreInspectionState && ReplayManager.IsPlaybackActive() && !saved.hasPresentation;
        SetRingIndex(RingSelection.Inner, saved.innerIndex, animate, false);
        SetRingIndex(RingSelection.Outer, 14, animate, false);
        SetInspectionState(restoreInspectionState && saved.isInspecting, false);
        if (restoreInspectionState && saved.hasPresentation)
        {
            if (innerRing != null) innerRing.localRotation = saved.innerRotation;
            snapStart = saved.snapStart; snapTarget = saved.snapTarget; snapElapsed = saved.snapElapsed;
            snapping = saved.snapping; displayedInnerIndex = saved.displayedInnerIndex;
        }
    }

    static RingSelection ParseRing(string value)
    {
        return string.Equals(value, "inner", System.StringComparison.OrdinalIgnoreCase) ? RingSelection.Inner : RingSelection.Outer;
    }

    static string RingName(RingSelection ring)
    {
        return ring == RingSelection.Inner ? "inner" : "outer";
    }

    Vector3 WheelWorldCenter()
    {
        return wheelCenter != null ? wheelCenter.position : transform.position;
    }

    void HandlePauseStarting() { }

    protected override void OnDisable()
    {
        PauseManager.PauseStarting -= HandlePauseStarting;
        if (isInspecting || inputSessionActive || hudHidden)
        {
            isInspecting = false;
            ReleaseInputSession();
            SetInspectionVisuals(false);
        }
        base.OnDisable();
    }

    void OnDestroy()
    {
        if (mainCameraData != null && inspectionCamera != null)
        {
            mainCameraData.cameraStack?.Remove(inspectionCamera);
        }
        if (runtimeInspectionCamera && inspectionCamera != null)
        {
            Destroy(inspectionCamera.gameObject);
        }
        if (runtimeInspectionHud && inspectionHud != null)
        {
            Destroy(inspectionHud);
        }
    }
}
