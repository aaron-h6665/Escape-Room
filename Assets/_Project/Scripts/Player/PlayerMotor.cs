using System;
using UnityEngine;

public class PlayerMotor : MonoBehaviour, IDataPersistence, IReplayObject
{
    private CharacterController controller;
    private Vector3 horizontalVelocity;
    private Vector3 playerVelocity;
    private bool isGrounded;

    [Header("Movement (meters per second)")]
    [Min(0f)] public float speed = 3.5f;
    [SerializeField, Min(1f)] private float sprintMultiplier = 1.7f;
    [SerializeField, Min(0f)] private float acceleration = 18f;
    [SerializeField, Range(0f, 1f)] private float airControl = 0.35f;

    [Header("Jump and Gravity (meters)")]
    public float gravity = -9.81f;
    [Min(0f)] public float jumpHeight = 1.20f;
    [SerializeField] private float groundedVerticalSpeed = -2f;
    [SerializeField] private float terminalVelocity = -53f;

    [Header("Crouching (meters)")]
    [SerializeField, Min(0.8f)] private float crouchHeight = 1.2f;
    [SerializeField, Min(0.5f)] private float crouchEyeHeight = 1.1f;
    [SerializeField, Range(0.1f, 1f)] private float crouchSpeedMultiplier = 0.5f;
    [SerializeField, Min(0.1f)] private float crouchTransitionSpeed = 3f;

    [SerializeField] InputManager inputManager;
    [SerializeField] private Transform respawnPoint;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float nextAllowedDeathTime;
    private Camera playerCamera;
    private float standingControllerHeight;
    private Vector3 standingControllerCenter;
    private Vector3 standingCameraPosition;
    private Vector3 crouchingCameraPosition;
    private float crouchingControllerHeight;
    private bool crouchRequested;
    private readonly Collider[] standingClearanceResults = new Collider[16];

    public int DeathCount { get; private set; }
    public bool IsCrouching { get; private set; }
    public event Action<int> DeathCountChanged;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        ConfigureCrouchDimensions();
        spawnPosition = respawnPoint != null ? respawnPoint.position : transform.position;
        spawnRotation = respawnPoint != null ? respawnPoint.rotation : transform.rotation;
    }

    private void Start()
    {
        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    // Update is called once per frame
    void Update()
    {
        isGrounded = controller.isGrounded;
        UpdateCrouch();
    }

    // receive the inputs from our InputManager and apply them to out character
    public void ProcessMove(Vector2 input, bool sprinting)
    {
        Vector2 normalizedInput = Vector2.ClampMagnitude(input, 1f);
        Vector3 moveDirection = transform.TransformDirection(
            new Vector3(normalizedInput.x, 0f, normalizedInput.y));
        float currentSpeed = IsCrouching
            ? speed * crouchSpeedMultiplier
            : sprinting ? speed * sprintMultiplier : speed;

        float currentAcceleration = isGrounded ? acceleration : acceleration * airControl;
        Vector3 targetHorizontalVelocity = moveDirection * currentSpeed;
        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetHorizontalVelocity,
            currentAcceleration * Time.deltaTime);

        if (isGrounded && playerVelocity.y < 0)
        {
            playerVelocity.y = groundedVerticalSpeed;
        }
        else
        {
            playerVelocity.y = Mathf.Max(
                playerVelocity.y + gravity * Time.deltaTime,
                terminalVelocity);
        }

        Vector3 movement = horizontalVelocity + Vector3.up * playerVelocity.y;
        controller.Move(movement * Time.deltaTime);
    }

    public void Jump()
    {
        if (isGrounded && !IsCrouching)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    public void ToggleCrouch()
    {
        crouchRequested = !crouchRequested;

        // Slow movement and lock jumping as soon as the crouch transition begins.
        if (crouchRequested)
        {
            IsCrouching = true;
        }
    }

    public bool DieAndRespawn()
    {
        // A player can overlap more than one laser in the same physics step.
        // Count that contact as one death rather than one death per collider.
        if (Time.unscaledTime < nextAllowedDeathTime)
        {
            return false;
        }

        nextAllowedDeathTime = Time.unscaledTime + 0.2f;
        DeathCount++;
        DeathCountChanged?.Invoke(DeathCount);
        Respawn();
        return true;
    }

    public void Respawn()
    {
        ResetVelocity();

        if (controller != null)
        {
            controller.enabled = false;
        }

        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        ResetCrouch();
        Physics.SyncTransforms();

        if (controller != null)
        {
            controller.enabled = true;
        }
    }

    public void LoadData(GameData data)
    {
        ApplyPosition(data.playerPosition);
    }

    public void SaveData(ref GameData data)
    {
        data.playerPosition = this.transform.position;
    }

    public void SaveSnapshot(ref GameData data)
    {
        data.playerPosition = transform.position;
    }

    public void LoadSnapshot(GameData data)
    {
        ApplyPosition(data.playerPosition);
    }

    public void ApplyReplayPosition(Vector3 position)
    {
        ApplyPosition(position);
    }

    void ApplyPosition(Vector3 position)
    {
        ResetVelocity();

        if (controller != null)
        {
            controller.enabled = false;
        }

        transform.position = position;

        if (controller != null)
        {
            controller.enabled = true;
        }
    }

    private void ResetVelocity()
    {
        horizontalVelocity = Vector3.zero;
        playerVelocity = Vector3.zero;
    }

    private void ConfigureCrouchDimensions()
    {
        standingControllerHeight = controller.height;
        standingControllerCenter = controller.center;

        float verticalScale = Mathf.Max(Mathf.Abs(transform.lossyScale.y), 0.0001f);
        float minimumHeight = controller.radius * 2f;
        crouchingControllerHeight = Mathf.Clamp(
            crouchHeight / verticalScale,
            minimumHeight,
            standingControllerHeight);

        // Keep the capsule bottom fixed so stance changes do not lift the
        // player from the floor or push them through it.
        float standingBottom = standingControllerCenter.y - standingControllerHeight * 0.5f;
        if (playerCamera != null)
        {
            standingCameraPosition = playerCamera.transform.localPosition;
            crouchingCameraPosition = standingCameraPosition;
            crouchingCameraPosition.y = standingBottom + crouchEyeHeight / verticalScale;
        }
    }

    private void UpdateCrouch()
    {
        bool standingIsBlocked = !crouchRequested && IsCrouching && !CanStand();
        bool shouldCrouch = crouchRequested || standingIsBlocked;
        float targetHeight = shouldCrouch ? crouchingControllerHeight : standingControllerHeight;
        Vector3 targetCameraPosition = shouldCrouch
            ? crouchingCameraPosition
            : standingCameraPosition;

        float verticalScale = Mathf.Max(Mathf.Abs(transform.lossyScale.y), 0.0001f);
        float localTransitionSpeed = crouchTransitionSpeed / verticalScale;
        float nextHeight = Mathf.MoveTowards(
            controller.height,
            targetHeight,
            localTransitionSpeed * Time.deltaTime);

        float standingBottom = standingControllerCenter.y - standingControllerHeight * 0.5f;
        Vector3 nextCenter = standingControllerCenter;
        nextCenter.y = standingBottom + nextHeight * 0.5f;
        controller.height = nextHeight;
        controller.center = nextCenter;

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = Vector3.MoveTowards(
                playerCamera.transform.localPosition,
                targetCameraPosition,
                localTransitionSpeed * Time.deltaTime);
        }

        IsCrouching = shouldCrouch ||
            controller.height < standingControllerHeight - 0.001f;
    }

    private bool CanStand()
    {
        float verticalScale = Mathf.Max(Mathf.Abs(transform.lossyScale.y), 0.0001f);
        float horizontalScale = Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.z));
        float radius = controller.radius * horizontalScale;
        float checkRadius = Mathf.Max(radius - controller.skinWidth * horizontalScale, 0.01f);
        float standingHeight = standingControllerHeight * verticalScale;
        float halfSegment = Mathf.Max(standingHeight * 0.5f - radius, 0f);
        Vector3 worldCenter = transform.TransformPoint(standingControllerCenter);
        Vector3 top = worldCenter + transform.up * halfSegment;
        Vector3 bottom = worldCenter - transform.up * halfSegment;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            top,
            bottom,
            checkRadius,
            standingClearanceResults,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = standingClearanceResults[i];
            if (hit != null && hit != controller && !hit.transform.IsChildOf(transform))
            {
                return false;
            }
        }

        return true;
    }

    private void ResetCrouch()
    {
        crouchRequested = false;
        IsCrouching = false;
        controller.height = standingControllerHeight;
        controller.center = standingControllerCenter;

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = standingCameraPosition;
        }
    }
}
