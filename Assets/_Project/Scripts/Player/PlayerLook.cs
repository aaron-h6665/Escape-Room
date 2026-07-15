using UnityEngine;

public class PlayerLook : MonoBehaviour, IDataPersistence, IReplayObject
{
    public Camera cam;
    private float xRotation = 0f;

    public float xSensitivity = 30f;
    public float ySensitivity = 30f;
    public float CameraPitch => xRotation;

    void Start()
    {
        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    public void ProcessLook(Vector2 input)
    {
        float mouseX = input.x;
        float mouseY = input.y;

        xRotation -= (mouseY * Time.deltaTime) * ySensitivity;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        cam.transform.localRotation = Quaternion.Euler(xRotation, 0,0);

        transform.Rotate(Vector3.up * (mouseX * Time.deltaTime) * xSensitivity);
    }

    public void LoadData(GameData data)
    {
        ApplyLookData(data);
    }

    public void SaveData(ref GameData data)
    {
        SaveLookData(ref data);
    }

    public void SaveSnapshot(ref GameData data)
    {
        SaveLookData(ref data);
    }

    public void LoadSnapshot(GameData data)
    {
        ApplyLookData(data);
    }

    public void ApplyReplayLook(Quaternion playerRotation, float cameraPitch)
    {
        transform.rotation = ReplayIdentity.IsZero(playerRotation) ? Quaternion.identity : playerRotation;
        xRotation = cameraPitch;
        if (cam != null)
        {
            cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }

    void SaveLookData(ref GameData data)
    {
        data.playerRotation = transform.rotation;
        data.playerCameraPitch = xRotation;
        data.playerCameraRotation = cam != null ? cam.transform.localRotation : Quaternion.Euler(xRotation, 0f, 0f);
    }

    void ApplyLookData(GameData data)
    {
        transform.rotation = ReplayIdentity.IsZero(data.playerRotation) ? Quaternion.identity : data.playerRotation;
        xRotation = data.playerCameraPitch;

        if (cam == null)
        {
            return;
        }

        cam.transform.localRotation = ReplayIdentity.IsZero(data.playerCameraRotation)
            ? Quaternion.Euler(xRotation, 0f, 0f)
            : data.playerCameraRotation;
    }
}
