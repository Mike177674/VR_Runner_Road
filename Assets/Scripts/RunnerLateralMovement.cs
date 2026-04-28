using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class RunnerLateralMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float maxSideDistance = 2f;
    [SerializeField, Range(0f, 1f)] private float inputDeadzone = 0.2f;

    private Rigidbody cachedRigidbody;
    private InputAction fallbackMoveAction;
    private float laneCenterZ;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        laneCenterZ = transform.position.z;
    }

    private void OnEnable()
    {
        EnsureFallbackMoveAction();
    }

    private void OnDisable()
    {
        DisableFallbackMoveAction();
    }

    private void FixedUpdate()
    {
        float input = ReadLateralInput();

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

    private float ReadLateralInput()
    {
        float input = 0f;
        if (moveAction != null && moveAction.action != null)
        {
            Vector2 moveVector = moveAction.action.ReadValue<Vector2>();
            input = moveVector.x;
        }
        else if (fallbackMoveAction != null)
        {
            Vector2 moveVector = fallbackMoveAction.ReadValue<Vector2>();
            input = moveVector.x;
        }

        if (Mathf.Abs(input) < inputDeadzone)
        {
            input = 0f;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed)
            {
                input = -1f;
            }

            if (keyboard.rightArrowKey.isPressed)
            {
                input = 1f;
            }
        }

        return Mathf.Clamp(input, -1f, 1f);
    }

    private void EnsureFallbackMoveAction()
    {
        if (moveAction != null || fallbackMoveAction != null)
        {
            return;
        }

        fallbackMoveAction = new InputAction("RunnerMove", InputActionType.Value);
        fallbackMoveAction.AddBinding("<XRController>{LeftHand}/primary2DAxis");
        fallbackMoveAction.AddBinding("<Gamepad>/leftStick");
        fallbackMoveAction.Enable();
    }

    private void DisableFallbackMoveAction()
    {
        if (fallbackMoveAction == null)
        {
            return;
        }

        fallbackMoveAction.Disable();
        fallbackMoveAction.Dispose();
        fallbackMoveAction = null;
    }
}
