using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    [System.NonSerialized]
    private PlayerInput.OnFootActions onFoot;
    private bool playerControlLocked;

    public PlayerInput.OnFootActions OnFoot => onFoot;
    public bool PlayerControlLocked => playerControlLocked;

    private PlayerMotor motor;
    private PlayerLook look;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        playerInput = new PlayerInput();
        onFoot = playerInput.OnFoot;
        motor = GetComponent<PlayerMotor>();
        onFoot.Jump.performed += ctx =>
        {
            if (CanMove())
            {
                motor.Jump();
            }
        };
        look = GetComponent<PlayerLook>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!CanMove())
        {
            return;
        }
        bool sprinting = onFoot.Sprint.ReadValue<float>() > 0;
        // tell the playermotor to move using the value from our movement action
        motor.ProcessMove(onFoot.Movement.ReadValue<Vector2>(), sprinting);
    }

    void LateUpdate()
    {
        if (!CanLook())
        {
            return;
        }

        look.ProcessLook(onFoot.Look.ReadValue<Vector2>());
    }

    public void SetPlayerControlLocked(bool locked)
    {
        playerControlLocked = locked;
    }

    bool CanMove()
    {
        return !playerControlLocked && motor != null && motor.enabled;
    }

    bool CanLook()
    {
        return !playerControlLocked && look != null && look.enabled;
    }

    private void OnEnable()
    {
        onFoot.Enable();
    }

    private void OnDisable()
    {
        onFoot.Disable();
    }
}
