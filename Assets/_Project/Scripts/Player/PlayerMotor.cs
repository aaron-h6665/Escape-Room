using UnityEngine;

public class PlayerMotor : MonoBehaviour, IDataPersistence
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
        controller.enabled = false;
        this.transform.position = data.playerPosition;
        controller.enabled = true;
    }

    public void SaveData(ref GameData data)
    {
        data.playerPosition = this.transform.position;
    }
}
