using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public sealed class StudyOptions
{
    public float volume = 0.8f;
    public float mouseSensitivity = 1f;
    public float controllerSensitivity = 1f;
    public bool invertLook;
    public bool fullscreen;
    public int width = 1280, height = 720;
    static StudyOptions current;
    public static bool UsingGamepad { get; private set; }
    public static string DeviceName => UsingGamepad ? (Gamepad.current?.displayName ?? "Gamepad") : "KeyboardMouse";
    public static StudyOptions Current
    {
        get
        {
            if (current == null)
            {
                string saved = PlayerPrefs.GetString("escape.options.v1", "");
                try { current = string.IsNullOrEmpty(saved) ? new StudyOptions() : JsonUtility.FromJson<StudyOptions>(saved); }
                catch { current = new StudyOptions(); }
                current ??= new StudyOptions();
            }
            return current;
        }
    }
    public static void PollDevice()
    {
        bool before = UsingGamepad;
        if (Gamepad.current != null && (Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.1f || Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.1f || Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.rightTrigger.wasPressedThisFrame || Gamepad.current.dpad.ReadValue().sqrMagnitude > 0.1f)) UsingGamepad = true;
        if (Keyboard.current?.anyKey.wasPressedThisFrame == true || Mouse.current?.leftButton.wasPressedThisFrame == true || (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 4)) UsingGamepad = false;
        if (before != UsingGamepad) ReplayManager.instance?.RecordSessionEvent("input_device_changed", DeviceName);
    }
    public void Apply(bool display = false)
    {
        volume = Mathf.Clamp01(volume);
        mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.25f, 3f);
        controllerSensitivity = Mathf.Clamp(controllerSensitivity, 0.25f, 3f);
        AudioListener.volume = volume;
        if (display) Screen.SetResolution(width, height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        PlayerPrefs.SetString("escape.options.v1", JsonUtility.ToJson(this));
        PlayerPrefs.Save();
        ReplayManager.instance?.RecordSessionEvent("settings_changed", JsonUtility.ToJson(this));
    }
}
