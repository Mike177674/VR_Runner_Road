using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class RunnerLateralMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float maxSideDistance = 2f;

    private Rigidbody cachedRigidbody;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        float input = 0f;
        if (Keyboard.current.leftArrowKey.isPressed) input = -1f;
        if (Keyboard.current.rightArrowKey.isPressed) input = 1f;

        // Use transform.right so rotation is respected
        Vector3 lateralVelocity = transform.right * input * moveSpeed;

        // Clamp using dot product against local right axis
        float lateralPos = Vector3.Dot(transform.position, transform.right);
        if ((lateralPos <= -maxSideDistance && input < 0) ||
            (lateralPos >= maxSideDistance && input > 0))
            lateralVelocity = Vector3.zero;

        Vector3 vel = cachedRigidbody.linearVelocity;
        // Preserve Y (gravity/jump), replace lateral component
        vel -= Vector3.Project(vel, transform.right);
        vel += lateralVelocity;
        cachedRigidbody.linearVelocity = vel;
    }
}