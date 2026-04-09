using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

public class XRTracker : MonoBehaviour
{
    [Header("World Motion")]
    [Min(0f)] public float moveSpeed = 4f;
    [SerializeField, Min(0f)] private float maxMoveSpeed = 8f;
    [SerializeField, Min(0f)] private float acceleration = 0.5f;

    [Header("Tracking References")]
    [SerializeField] private Transform head;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private bool autoAssignMainCamera = true;
    [SerializeField] private bool createRuntimeHandAnchors = true;
    [SerializeField] private bool pinRigToStart = true;

    [Header("Dodge Thresholds")]
    [SerializeField, Min(0.01f)] private float stepThreshold = 0.18f;
    [SerializeField, Min(1f)] private float leanAngleThreshold = 12f;
    [SerializeField, Min(0.01f)] private float duckDistance = 0.25f;
    [SerializeField, Min(0f)] private float grabStrengthThreshold = 0.6f;

    [Header("Debug")]
    [SerializeField] private InputActionReference recalibrateAction;
    [SerializeField] private InputActionReference leftGrabAction;
    [SerializeField] private InputActionReference rightGrabAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private bool enableInputActions = true;
    [SerializeField] private bool allowKeyboardDebugFallback = true;
    [SerializeField] private Key recalibrateKey = Key.R;
    [SerializeField] private Key leftGrabKey = Key.Q;
    [SerializeField] private Key rightGrabKey = Key.E;
    [SerializeField] private Key jumpKey = Key.Space;

    private Vector3 rigStartPosition;
    private Quaternion rigStartRotation;
    private Vector3 neutralHeadLocalPosition;
    private bool isCalibrated;
    private bool ownsRecalibrateAction;
    private bool ownsLeftGrabAction;
    private bool ownsRightGrabAction;
    private bool ownsJumpAction;
    private bool previousJumpDevicePressed;
    private Transform runtimeLeftHandAnchor;
    private Transform runtimeRightHandAnchor;

    public float CurrentMoveSpeed { get; private set; }
    public float LateralOffset { get; private set; }
    public float CrouchDepth { get; private set; }
    public float HeadRollDegrees { get; private set; }
    public bool LeftHandTracked { get; private set; }
    public bool RightHandTracked { get; private set; }
    public bool LeftGrabActive { get; private set; }
    public bool RightGrabActive { get; private set; }
    public float LeftGrabStrength { get; private set; }
    public float RightGrabStrength { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool JumpPressedThisFrame { get; private set; }
    public Transform HeadTransform => head;
    public Transform LeftHandTransform => leftHand != null ? leftHand : runtimeLeftHandAnchor;
    public Transform RightHandTransform => rightHand != null ? rightHand : runtimeRightHandAnchor;
    public Vector3 RunnerCenter => pinRigToStart ? rigStartPosition : transform.position;
    public Vector3 RunnerForward
    {
        get
        {
            Quaternion referenceRotation = pinRigToStart ? rigStartRotation : transform.rotation;
            Vector3 forward = Vector3.ProjectOnPlane(referenceRotation * Vector3.forward, Vector3.up);
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        }
    }

    public bool IsSteppingLeft => LateralOffset <= -stepThreshold;
    public bool IsSteppingRight => LateralOffset >= stepThreshold;
    public bool IsLeaningLeft => HeadRollDegrees <= -leanAngleThreshold;
    public bool IsLeaningRight => HeadRollDegrees >= leanAngleThreshold;
    public bool IsDucking => CrouchDepth >= duckDistance;

    private void OnEnable()
    {
        ownsRecalibrateAction = EnableActionIfNeeded(recalibrateAction);
        ownsLeftGrabAction = EnableActionIfNeeded(leftGrabAction);
        ownsRightGrabAction = EnableActionIfNeeded(rightGrabAction);
        ownsJumpAction = EnableActionIfNeeded(jumpAction);
    }

    private void OnDisable()
    {
        DisableActionIfOwned(recalibrateAction, ownsRecalibrateAction);
        DisableActionIfOwned(leftGrabAction, ownsLeftGrabAction);
        DisableActionIfOwned(rightGrabAction, ownsRightGrabAction);
        DisableActionIfOwned(jumpAction, ownsJumpAction);
        ownsRecalibrateAction = false;
        ownsLeftGrabAction = false;
        ownsRightGrabAction = false;
        ownsJumpAction = false;
        previousJumpDevicePressed = false;
        JumpHeld = false;
        JumpPressedThisFrame = false;
    }

    private void Awake()
    {
        rigStartPosition = transform.position;
        rigStartRotation = transform.rotation;
        CurrentMoveSpeed = moveSpeed;
    }

    private void Start()
    {
        ResolveTrackingReferences();
        Calibrate();
    }

    private void Update()
    {
        ResolveTrackingReferences();
        UpdateHandAnchors();
        UpdateRunnerState();
        UpdateGrabState();
        UpdateJumpState();
        UpdateWorldSpeed();

        if (WasPressedThisFrame(recalibrateAction, recalibrateKey))
        {
            Calibrate();
        }
    }

    private void LateUpdate()
    {
        if (!pinRigToStart)
        {
            return;
        }

        transform.SetPositionAndRotation(rigStartPosition, rigStartRotation);
    }

    public void Calibrate()
    {
        if (head == null)
        {
            return;
        }

        neutralHeadLocalPosition = head.localPosition;
        isCalibrated = true;
        LateralOffset = 0f;
        CrouchDepth = 0f;
        HeadRollDegrees = 0f;
    }

    public bool IsHoldingItem(RunnerGrabbableItem item, bool leftSide)
    {
        if (item == null || !item.IsHeld)
        {
            return false;
        }

        return leftSide ? item.HeldByLeftHand : item.HeldByRightHand;
    }

    private void ResolveTrackingReferences()
    {
        if (autoAssignMainCamera && head == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                head = mainCamera.transform;
            }
        }

        if (createRuntimeHandAnchors)
        {
            if (leftHand == null && runtimeLeftHandAnchor == null)
            {
                runtimeLeftHandAnchor = CreateRuntimeHandAnchor("Runtime Left Hand Anchor");
            }

            if (rightHand == null && runtimeRightHandAnchor == null)
            {
                runtimeRightHandAnchor = CreateRuntimeHandAnchor("Runtime Right Hand Anchor");
            }
        }
    }

    private Transform CreateRuntimeHandAnchor(string anchorName)
    {
        GameObject anchor = new GameObject(anchorName);
        Transform anchorParent = head != null && head.parent != null ? head.parent : transform;
        anchor.transform.SetParent(anchorParent, false);
        anchor.SetActive(false);
        return anchor.transform;
    }

    private void UpdateHandAnchors()
    {
        if (leftHand == null)
        {
            UpdateRuntimeHandAnchor(XRNode.LeftHand, runtimeLeftHandAnchor);
        }

        if (rightHand == null)
        {
            UpdateRuntimeHandAnchor(XRNode.RightHand, runtimeRightHandAnchor);
        }
    }

    private void UpdateRuntimeHandAnchor(XRNode node, Transform anchor)
    {
        if (anchor == null)
        {
            return;
        }

        XRInputDevice device = InputDevices.GetDeviceAtXRNode(node);
        bool isTracked = false;
        bool tracked = device.isValid
            && device.TryGetFeatureValue(XRCommonUsages.isTracked, out isTracked)
            && isTracked;

        anchor.gameObject.SetActive(tracked);
        if (!tracked)
        {
            return;
        }

        if (device.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 devicePosition))
        {
            anchor.localPosition = devicePosition;
        }

        if (device.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion deviceRotation))
        {
            anchor.localRotation = deviceRotation;
        }
    }

    private void UpdateRunnerState()
    {
        if (head == null)
        {
            return;
        }

        if (!isCalibrated)
        {
            Calibrate();
        }

        Vector3 headOffset = head.localPosition - neutralHeadLocalPosition;
        LateralOffset = headOffset.x;
        CrouchDepth = Mathf.Max(0f, neutralHeadLocalPosition.y - head.localPosition.y);
        HeadRollDegrees = NormalizeSignedAngle(head.localEulerAngles.z);
    }

    private void UpdateGrabState()
    {
        UpdateSingleGrabState(
            XRNode.LeftHand,
            LeftHandTransform,
            leftGrabAction,
            leftGrabKey,
            out bool leftTracked,
            out float leftStrength,
            out bool leftActive);

        UpdateSingleGrabState(
            XRNode.RightHand,
            RightHandTransform,
            rightGrabAction,
            rightGrabKey,
            out bool rightTracked,
            out float rightStrength,
            out bool rightActive);

        LeftHandTracked = leftTracked;
        RightHandTracked = rightTracked;
        LeftGrabStrength = leftStrength;
        RightGrabStrength = rightStrength;
        LeftGrabActive = leftActive;
        RightGrabActive = rightActive;
    }

    private void UpdateSingleGrabState(
        XRNode node,
        Transform trackedTransform,
        InputActionReference grabAction,
        Key fallbackKey,
        out bool tracked,
        out float grabStrength,
        out bool grabActive)
    {
        XRInputDevice device = InputDevices.GetDeviceAtXRNode(node);

        tracked = trackedTransform != null && trackedTransform.gameObject.activeInHierarchy;
        bool deviceTracked = false;
        if (device.isValid
            && device.TryGetFeatureValue(XRCommonUsages.isTracked, out deviceTracked)
            && deviceTracked)
        {
            tracked = true;
        }

        grabStrength = 0f;
        if (device.isValid)
        {
            if (device.TryGetFeatureValue(XRCommonUsages.grip, out float grip))
            {
                grabStrength = Mathf.Max(grabStrength, grip);
            }

            if (device.TryGetFeatureValue(XRCommonUsages.trigger, out float trigger))
            {
                grabStrength = Mathf.Max(grabStrength, trigger);
            }

            if (device.TryGetFeatureValue(XRCommonUsages.gripButton, out bool gripPressed) && gripPressed)
            {
                grabStrength = 1f;
            }

            if (device.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool triggerPressed) && triggerPressed)
            {
                grabStrength = 1f;
            }
        }

        if (IsPressed(grabAction, fallbackKey))
        {
            grabStrength = 1f;
        }

        grabActive = tracked && grabStrength >= grabStrengthThreshold;
    }

    private void UpdateWorldSpeed()
    {
        float targetSpeed = Mathf.Max(moveSpeed, maxMoveSpeed);
        CurrentMoveSpeed = Mathf.MoveTowards(CurrentMoveSpeed, targetSpeed, acceleration * Time.deltaTime);
    }

    private void UpdateJumpState()
    {
        bool actionHeld = IsPressed(jumpAction, jumpKey);
        bool actionPressedThisFrame = WasPressedThisFrame(jumpAction, jumpKey);
        bool devicePressed = IsBoolFeaturePressed(XRNode.RightHand, XRCommonUsages.primaryButton);

        JumpHeld = actionHeld || devicePressed;
        JumpPressedThisFrame = actionPressedThisFrame || (devicePressed && !previousJumpDevicePressed);
        previousJumpDevicePressed = devicePressed;
    }

    private static float NormalizeSignedAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    private bool WasPressedThisFrame(InputActionReference actionReference, Key fallbackKey)
    {
        if (actionReference != null && actionReference.action != null)
        {
            return actionReference.action.WasPressedThisFrame();
        }

        if (!allowKeyboardDebugFallback)
        {
            return false;
        }

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[fallbackKey].wasPressedThisFrame;
    }

    private static bool IsBoolFeaturePressed(XRNode node, InputFeatureUsage<bool> featureUsage)
    {
        XRInputDevice device = InputDevices.GetDeviceAtXRNode(node);
        return device.isValid
            && device.TryGetFeatureValue(featureUsage, out bool pressed)
            && pressed;
    }

    private bool IsPressed(InputActionReference actionReference, Key fallbackKey)
    {
        if (actionReference != null && actionReference.action != null)
        {
            return actionReference.action.IsPressed();
        }

        if (!allowKeyboardDebugFallback)
        {
            return false;
        }

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[fallbackKey].isPressed;
    }

    private bool EnableActionIfNeeded(InputActionReference actionReference)
    {
        if (!enableInputActions || actionReference == null || actionReference.action == null)
        {
            return false;
        }

        if (actionReference.action.enabled)
        {
            return false;
        }

        actionReference.action.Enable();
        return true;
    }

    private static void DisableActionIfOwned(InputActionReference actionReference, bool owned)
    {
        if (!owned || actionReference == null || actionReference.action == null)
        {
            return;
        }

        actionReference.action.Disable();
    }
}
