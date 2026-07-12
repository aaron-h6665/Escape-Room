using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private Camera cam;
    [SerializeField]
    private float distance = 10f;
    [SerializeField]
    private LayerMask mask;
    [SerializeField]
    private LayerMask blockerMask;
    private PlayerUI playerUI;
    private InputManager inputManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cam = GetComponent<PlayerLook>().cam;
        playerUI = GetComponent<PlayerUI>();
        inputManager = GetComponent<InputManager>();
    }

    // Update is called once per frame
    void Update()
    {
        if (ReplayManager.IsPlaybackActive())
        {
            return;
        }

        if (cam == null || playerUI == null || inputManager == null)
        {
            return;
        }

        playerUI.UpdateText(string.Empty);

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hitInfo;
        int raycastMask = mask.value | blockerMask.value;
        if (Physics.Raycast(ray, out hitInfo, distance, raycastMask, QueryTriggerInteraction.Collide))
        {
            Interactable interactable = hitInfo.collider.GetComponentInParent<Interactable>();
            if (interactable != null)
            {
                playerUI.UpdateText(interactable.GetPromptMessage());
                if (inputManager.OnFoot.Interact.triggered)
                {
                    interactable.BaseInteract(gameObject);
                }
            }
        }
    }
}
