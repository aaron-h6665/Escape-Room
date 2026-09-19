using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    [System.NonSerialized]
    private PlayerInput.OnFootActions onFoot;
    private bool playerControlLocked;
    readonly System.Collections.Generic.HashSet<object> controlOwners = new System.Collections.Generic.HashSet<object>();
    bool consumingTakeover;
    int consumeFrame;
    public bool GameplayInputSuppressed => consumingTakeover;
    public void AcquireControl(object owner) => controlOwners.Add(owner);
    public void ReleaseControl(object owner) => controlOwners.Remove(owner);
    public void ConsumeTakeoverInput() { consumingTakeover = true; consumeFrame = Time.frameCount; }
    void Update()
    {
        StudyOptions.PollDevice();
        if (consumingTakeover && Time.frameCount > consumeFrame
            && !(Gamepad.current?.rightTrigger.isPressed ?? false)
            && !(Keyboard.current?.tKey.isPressed ?? false)
            && !(Mouse.current?.leftButton.isPressed ?? false)) consumingTakeover = false;
    }
    private InputAction inventoryToggleAction;
    private InputAction inventoryToggleAlternateAction;
    private InputAction takeoverAlternateAction;
    private InputAction crouchAction;

    public PlayerInput.OnFootActions OnFoot => onFoot;
    public bool PlayerControlLocked => playerControlLocked || controlOwners.Count > 0;
    public event Action PausePressed;
    public event Action TakeoverPressed;
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
        CreateSupplementalActions();
        look = GetComponent<PlayerLook>();
    }

    void CreateSupplementalActions()
    {
        inventoryToggleAction = new InputAction("InventoryToggle", InputActionType.Value, expectedControlType: "Axis");
        inventoryToggleAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Gamepad>/leftShoulder")
            .With("Positive", "<Gamepad>/rightShoulder");
        inventoryToggleAction.AddBinding("<Mouse>/scroll/y");
        inventoryToggleAction.performed += OnInventoryTogglePerformed;

        inventoryToggleAlternateAction = new InputAction("InventoryToggleAlternate", InputActionType.PassThrough, expectedControlType: "Button");
        for (int slot = 1; slot <= 5; slot++)
        {
            inventoryToggleAlternateAction.AddBinding($"<Keyboard>/{slot}");
            inventoryToggleAlternateAction.AddBinding($"<Keyboard>/numpad{slot}");
        }
        inventoryToggleAlternateAction.performed += OnInventoryToggleAlternatePerformed;

        takeoverAlternateAction = new InputAction("TakeoverAlternate", InputActionType.Button, expectedControlType: "Button");
        takeoverAlternateAction.AddBinding("<Keyboard>/t");
        takeoverAlternateAction.AddBinding("<Gamepad>/rightTrigger").WithInteraction("press");
        takeoverAlternateAction.performed += OnTakeoverAlternatePerformed;

        crouchAction = new InputAction("Crouch", InputActionType.Button, expectedControlType: "Button");
        crouchAction.AddBinding("<Keyboard>/c").WithInteraction("press");
        crouchAction.AddBinding("<Gamepad>/rightStickPress").WithInteraction("press");
        crouchAction.performed += OnCrouchPerformed;
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
        return !ReplayManager.IsPlaybackActive() && !(ReplayManager.instance?.IsHandoffFrame ?? false) && !PlayerControlLocked && !consumingTakeover && motor != null && motor.enabled;
    }

    bool CanLook()
    {
        return !ReplayManager.IsPlaybackActive() && !(ReplayManager.instance?.IsHandoffFrame ?? false) && !PlayerControlLocked && !consumingTakeover && look != null && look.enabled;
    }

    void OnPausePerformed(InputAction.CallbackContext context)
    {
        PausePressed?.Invoke();
    }

    void OnTakeoverAlternatePerformed(InputAction.CallbackContext context)
    {
        if (ReplayManager.IsPlaybackActive() && Time.timeScale > 0f && !consumingTakeover)
        {
            TakeoverPressed?.Invoke();
        }
    }

    void OnCrouchPerformed(InputAction.CallbackContext context)
    {
        if (CanMove())
        {
            motor.ToggleCrouch();
        }
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
        return enabled && !ReplayManager.IsPlaybackActive() && !(ReplayManager.instance?.IsHandoffFrame ?? false) && !PlayerControlLocked && !consumingTakeover;
    }

    private void OnEnable()
    {
        if (playerInput != null)
        {
            onFoot.Enable();
            inventoryToggleAction?.Enable();
            inventoryToggleAlternateAction?.Enable();
            takeoverAlternateAction?.Enable();
            crouchAction?.Enable();
        }
    }

    private void OnDisable()
    {
        if (playerInput != null)
        {
            onFoot.Disable();
            inventoryToggleAction?.Disable();
            inventoryToggleAlternateAction?.Disable();
            takeoverAlternateAction?.Disable();
            crouchAction?.Disable();
        }
    }

    private void OnDestroy()
    {
        if (playerInput == null)
        {
            return;
        }

        onFoot.Pause.performed -= OnPausePerformed;
        if (inventoryToggleAction != null)
        {
            inventoryToggleAction.performed -= OnInventoryTogglePerformed;
            inventoryToggleAction.Dispose();
        }
        if (inventoryToggleAlternateAction != null)
        {
            inventoryToggleAlternateAction.performed -= OnInventoryToggleAlternatePerformed;
            inventoryToggleAlternateAction.Dispose();
        }
        if (takeoverAlternateAction != null)
        {
            takeoverAlternateAction.performed -= OnTakeoverAlternatePerformed;
            takeoverAlternateAction.Dispose();
        }
        if (crouchAction != null)
        {
            crouchAction.performed -= OnCrouchPerformed;
            crouchAction.Dispose();
        }
        onFoot.Disable();
        playerInput.Dispose();
    }
}
