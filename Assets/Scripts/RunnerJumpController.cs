using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class RunnerJumpController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private XRTracker runner;
    [SerializeField] private InputActionReference jumpAction;

    [Header("Jump")]
    [SerializeField, Min(0.1f)] private float jumpVelocity = 5.1f;
    [SerializeField, Min(0.1f)] private float highSpeedJumpVelocity = 4.5f;
    [SerializeField, Min(0f)] private float jumpCooldown = 0.15f;
    [SerializeField, Min(0f)] private float groundedGraceTime = 0.1f;
    [SerializeField, Range(0f, 1f)] private float groundedNormalThreshold = 0.5f;

    private Rigidbody cachedRigidbody;
    private InputAction fallbackJumpAction;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpTime = float.NegativeInfinity;
    private bool jumpQueued;

    public bool IsGrounded => Time.time - lastGroundedTime <= groundedGraceTime;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        if (runner == null)
        {
            runner = Object.FindFirstObjectByType<XRTracker>();
        }
    }

    private void OnEnable()
    {
        EnsureFallbackJumpAction();
    }

    private void OnDisable()
    {
        jumpQueued = false;
        DisableFallbackJumpAction();
    }

    private void Update()
    {
        if (WasJumpPressedThisFrame())
        {
            jumpQueued = true;
        }
    }

    private void FixedUpdate()
    {
        if (!jumpQueued || cachedRigidbody == null)
        {
            return;
        }

        jumpQueued = false;
        if (!CanJump())
        {
            return;
        }

        Vector3 velocity = cachedRigidbody.linearVelocity;
        if (velocity.y < 0f)
        {
            velocity.y = 0f;
        }

        velocity.y = GetCurrentJumpVelocity();
        cachedRigidbody.linearVelocity = velocity;
        lastGroundedTime = float.NegativeInfinity;
        lastJumpTime = Time.time;
    }

    private void OnCollisionEnter(Collision collision)
    {
        CacheGroundContact(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        CacheGroundContact(collision);
    }

    private bool WasJumpPressedThisFrame()
    {
        if (runner != null)
        {
            return runner.JumpPressedThisFrame;
        }

        if (jumpAction != null && jumpAction.action != null)
        {
            return jumpAction.action.WasPressedThisFrame();
        }

        return fallbackJumpAction != null && fallbackJumpAction.WasPressedThisFrame();
    }

    private bool CanJump()
    {
        return IsGrounded && Time.time - lastJumpTime >= jumpCooldown;
    }

    private float GetCurrentJumpVelocity()
    {
        float baseJumpVelocity = jumpVelocity;
        float topSpeedJumpVelocity = highSpeedJumpVelocity;
        float speedProgress = 0f;

        if (GameManager.Instance != null)
        {
            baseJumpVelocity = GameManager.Instance.CurrentJumpVelocity;
            topSpeedJumpVelocity = GameManager.Instance.CurrentHighSpeedJumpVelocity;
            speedProgress = GameManager.Instance.CurrentWorldSpeedProgress;
        }

        if (topSpeedJumpVelocity >= baseJumpVelocity)
        {
            return baseJumpVelocity;
        }

        return Mathf.Lerp(baseJumpVelocity, topSpeedJumpVelocity, speedProgress);
    }

    private void CacheGroundContact(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y >= groundedNormalThreshold)
            {
                lastGroundedTime = Time.time;
                return;
            }
        }
    }

    private void EnsureFallbackJumpAction()
    {
        if (jumpAction != null || fallbackJumpAction != null)
        {
            return;
        }

        fallbackJumpAction = new InputAction("RunnerJump", InputActionType.Button);
        fallbackJumpAction.AddBinding("<XRController>{RightHand}/primaryButton");
        fallbackJumpAction.AddBinding("<Keyboard>/space");
        fallbackJumpAction.AddBinding("<Gamepad>/buttonSouth");
        fallbackJumpAction.Enable();
    }

    private void DisableFallbackJumpAction()
    {
        if (fallbackJumpAction == null)
        {
            return;
        }

        fallbackJumpAction.Disable();
        fallbackJumpAction.Dispose();
        fallbackJumpAction = null;
    }
}
