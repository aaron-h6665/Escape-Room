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
    [Min(0f)] public float jumpHeight = 0.45f;
    [SerializeField] private float groundedVerticalSpeed = -2f;
    [SerializeField] private float terminalVelocity = -53f;

    [SerializeField] InputManager inputManager;
    [SerializeField] private Transform respawnPoint;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float nextAllowedDeathTime;

    public int DeathCount { get; private set; }
    public event Action<int> DeathCountChanged;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
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
    }

    // receive the inputs from our InputManager and apply them to out character
    public void ProcessMove(Vector2 input, bool sprinting)
    {
        Vector2 normalizedInput = Vector2.ClampMagnitude(input, 1f);
        Vector3 moveDirection = transform.TransformDirection(
            new Vector3(normalizedInput.x, 0f, normalizedInput.y));
        float currentSpeed = sprinting ? speed * sprintMultiplier : speed;

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
        if (isGrounded)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
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
}
