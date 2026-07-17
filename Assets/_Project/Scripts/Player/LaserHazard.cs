using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class LaserHazard : MonoBehaviour
{
    private void Awake()
    {
        Rigidbody hazardBody = GetComponent<Rigidbody>();
        hazardBody.useGravity = false;
        hazardBody.isKinematic = true;

        // The laser visuals keep their colliders, but triggers let the player
        // pass through instead of treating the beams like solid geometry.
        foreach (Collider hazardCollider in GetComponentsInChildren<Collider>())
        {
            hazardCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMotor player = other.GetComponentInParent<PlayerMotor>();
        if (player != null)
        {
            player.DieAndRespawn();
        }
    }
}
