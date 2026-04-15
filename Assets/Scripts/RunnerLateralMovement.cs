using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class RunnerLateralMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float maxSideDistance = 2f;

    private Rigidbody cachedRigidbody;
    private float laneCenterZ;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        laneCenterZ = transform.position.z;
    }

    private void FixedUpdate()
    {
        float input = 0f;
        if (Keyboard.current.leftArrowKey.isPressed) input = -1f;
        if (Keyboard.current.rightArrowKey.isPressed) input = 1f;

        // Road sections move along world X, so lane changes should happen across world Z.
        float lateralOffset = transform.position.z - laneCenterZ;
        float desiredLateralVelocity = -input * moveSpeed;

        if ((lateralOffset <= -maxSideDistance && desiredLateralVelocity < 0f) ||
            (lateralOffset >= maxSideDistance && desiredLateralVelocity > 0f))
        {
            desiredLateralVelocity = 0f;
        }

        Vector3 vel = cachedRigidbody.linearVelocity;
        vel.z = desiredLateralVelocity;
        cachedRigidbody.linearVelocity = vel;

        // Keep the runner inside the lane bounds even after a wall collision nudges it outward.
        Vector3 position = cachedRigidbody.position;
        position.z = Mathf.Clamp(position.z, laneCenterZ - maxSideDistance, laneCenterZ + maxSideDistance);
        cachedRigidbody.position = position;
    }
}
