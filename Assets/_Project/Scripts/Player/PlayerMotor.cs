using UnityEngine;

public class PlayerMotor : MonoBehaviour, IDataPersistence, IReplayObject
{
    private CharacterController controller;
    private Vector3 playerVelocity;
    public float speed = 20f;
    private float sprintMultiplier = 2f;
    private bool isGrounded;
    public float gravity = -9.8f;
    public float jumpHeight = 0.5f;
    [SerializeField] InputManager inputManager;
    [SerializeField] private Transform respawnPoint;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
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
        Vector3 moveDirection = Vector3.zero;
        moveDirection.x = input.x;
        moveDirection.z = input.y;
        float currentSpeed = sprinting ? speed * sprintMultiplier : speed;
        controller.Move(transform.TransformDirection(moveDirection) * currentSpeed * Time.deltaTime);
        playerVelocity.y += gravity * Time.deltaTime;
        if (isGrounded && playerVelocity.y < 0)
            playerVelocity.y = -2f;
        controller.Move(playerVelocity * Time.deltaTime);
    }

    public void Jump()
    {
        if (isGrounded)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -3.0f * gravity);
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
        playerVelocity = Vector3.zero;

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
}
