using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    [System.NonSerialized]
    private PlayerInput.OnFootActions onFoot;
    private bool playerControlLocked;

    public PlayerInput.OnFootActions OnFoot => onFoot;
    public bool PlayerControlLocked => playerControlLocked;
    public event Action PausePressed;
    public event Action<int> InventoryNavigationPressed;
    public event Action<int> InventorySlotPressed;

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
        onFoot.Pause.performed += OnPausePerformed;
        onFoot.InventoryToggle.performed += OnInventoryTogglePerformed;
        onFoot.InventoryToggleAlternate.performed += OnInventoryToggleAlternatePerformed;
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
        return !ReplayManager.IsPlaybackActive() && !playerControlLocked && motor != null && motor.enabled;
    }

    bool CanLook()
    {
        return !ReplayManager.IsPlaybackActive() && !playerControlLocked && look != null && look.enabled;
    }

    void OnPausePerformed(InputAction.CallbackContext context)
    {
        PausePressed?.Invoke();
    }

    void OnInventoryTogglePerformed(InputAction.CallbackContext context)
    {
        if (!CanUseInventoryInput())
        {
            return;
        }

        float direction = context.ReadValue<float>();
        if (direction > 0.01f)
        {
            InventoryNavigationPressed?.Invoke(1);
        }
        else if (direction < -0.01f)
        {
            InventoryNavigationPressed?.Invoke(-1);
        }
    }

    void OnInventoryToggleAlternatePerformed(InputAction.CallbackContext context)
    {
        if (!CanUseInventoryInput())
        {
            return;
        }

        int slotIndex = ResolveInventorySlotIndex(context.control != null ? context.control.path : string.Empty);
        if (slotIndex >= 0)
        {
            InventorySlotPressed?.Invoke(slotIndex);
        }
    }

    int ResolveInventorySlotIndex(string controlPath)
    {
        if (controlPath.EndsWith("/1") || controlPath.EndsWith("/numpad1"))
        {
            return 0;
        }

        if (controlPath.EndsWith("/2") || controlPath.EndsWith("/numpad2"))
        {
            return 1;
        }

        if (controlPath.EndsWith("/3") || controlPath.EndsWith("/numpad3"))
        {
            return 2;
        }

        if (controlPath.EndsWith("/4") || controlPath.EndsWith("/numpad4"))
        {
            return 3;
        }

        if (controlPath.EndsWith("/5") || controlPath.EndsWith("/numpad5"))
        {
            return 4;
        }

        return -1;
    }

    bool CanUseInventoryInput()
    {
        return enabled && !ReplayManager.IsPlaybackActive() && !playerControlLocked;
    }

    private void OnEnable()
    {
        if (playerInput != null)
        {
            onFoot.Enable();
        }
    }

    private void OnDisable()
    {
        if (playerInput != null)
        {
            onFoot.Disable();
        }
    }

    private void OnDestroy()
    {
        if (playerInput == null)
        {
            return;
        }

        onFoot.Pause.performed -= OnPausePerformed;
        onFoot.InventoryToggle.performed -= OnInventoryTogglePerformed;
        onFoot.InventoryToggleAlternate.performed -= OnInventoryToggleAlternatePerformed;
        onFoot.Disable();
        playerInput.Dispose();
    }
}
