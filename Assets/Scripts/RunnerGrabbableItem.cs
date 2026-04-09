using UnityEngine;

public class RunnerGrabbableItem : MonoBehaviour
{
    [SerializeField] private XRTracker runner;
    [SerializeField, Min(0.01f)] private float grabRadius = 0.15f;
    [SerializeField, Min(0.1f)] private float grabAssistRadius = 1.00f;
    [SerializeField, Min(0.1f)] private float pullSpeed = 6f;
    [SerializeField] private Renderer[] highlightRenderers;
    [SerializeField] private Color highlightColor = new Color(0.2f, 0.95f, 1f, 1f);
    [SerializeField, Min(0f)] private float highlightEmissionIntensity = 1.5f;
    [SerializeField] private Vector3 heldLocalPositionOffset;
    [SerializeField] private Vector3 heldLocalEulerOffset;

    private Rigidbody cachedRigidbody;
    private Transform currentHand;
    private bool heldByLeftHand;
    private bool restoreKinematicState;
    private bool restoreGravityState;
    private bool isPulling;
    private bool pullingToLeftHand;
    private bool isHighlighted;
    private MaterialPropertyBlock highlightBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public bool IsHeld => currentHand != null;
    public bool HeldByLeftHand => IsHeld && heldByLeftHand;
    public bool HeldByRightHand => IsHeld && !heldByLeftHand;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        highlightBlock = new MaterialPropertyBlock();
        if (runner == null)
        {
            runner = Object.FindFirstObjectByType<XRTracker>();
        }

        if (highlightRenderers == null || highlightRenderers.Length == 0)
        {
            highlightRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private void Update()
    {
        if (runner == null)
        {
            return;
        }

        if (IsHeld)
        {
            SetHighlighted(false);

            bool grabStillActive = heldByLeftHand ? runner.LeftGrabActive : runner.RightGrabActive;
            Transform hand = heldByLeftHand ? runner.LeftHandTransform : runner.RightHandTransform;
            if (!grabStillActive || hand == null)
            {
                Release();
                return;
            }

            currentHand = hand;
            SnapToHand(currentHand);
            return;
        }

        HandCandidate nearestHand = GetNearestHandInAssistRange();
        SetHighlighted(nearestHand.IsValid);

        if (isPulling)
        {
            UpdatePulling();
            return;
        }

        TryGrab(runner.LeftHandTransform, true, runner.LeftGrabActive);
        if (IsHeld)
        {
            return;
        }

        TryGrab(runner.RightHandTransform, false, runner.RightGrabActive);
    }

    public void Release()
    {
        SetHighlighted(false);
        transform.SetParent(null, true);
        currentHand = null;
        isPulling = false;

        if (cachedRigidbody != null)
        {
            cachedRigidbody.isKinematic = restoreKinematicState;
            cachedRigidbody.useGravity = restoreGravityState;
        }
    }

    private void TryGrab(Transform hand, bool leftSide, bool grabActive)
    {
        if (hand == null || !grabActive)
        {
            return;
        }

        if (Vector3.Distance(hand.position, transform.position) > grabRadius)
        {
            if (!grabActive || Vector3.Distance(hand.position, transform.position) > grabAssistRadius)
            {
                return;
            }

            StartPull(hand, leftSide);
            return;
        }

        AttachToHand(hand, leftSide);
    }

    private void StartPull(Transform hand, bool leftSide)
    {
        if (hand == null)
        {
            return;
        }

        isPulling = true;
        currentHand = hand;
        pullingToLeftHand = leftSide;

        if (cachedRigidbody != null)
        {
            restoreKinematicState = cachedRigidbody.isKinematic;
            restoreGravityState = cachedRigidbody.useGravity;
            cachedRigidbody.isKinematic = true;
            cachedRigidbody.useGravity = false;
            cachedRigidbody.linearVelocity = Vector3.zero;
            cachedRigidbody.angularVelocity = Vector3.zero;
        }

        transform.SetParent(null, true);
    }

    private void UpdatePulling()
    {
        Transform hand = pullingToLeftHand ? runner.LeftHandTransform : runner.RightHandTransform;
        bool grabStillActive = pullingToLeftHand ? runner.LeftGrabActive : runner.RightGrabActive;
        if (hand == null || !grabStillActive)
        {
            Release();
            return;
        }

        currentHand = hand;
        SetHighlighted(true);

        Vector3 targetPosition = hand.TransformPoint(heldLocalPositionOffset);
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, pullSpeed * Time.deltaTime);

        Quaternion targetRotation = hand.rotation * Quaternion.Euler(heldLocalEulerOffset);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, pullSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) <= grabRadius)
        {
            AttachToHand(hand, pullingToLeftHand);
        }
    }

    private void AttachToHand(Transform hand, bool leftSide)
    {
        currentHand = hand;
        heldByLeftHand = leftSide;
        isPulling = false;
        SetHighlighted(false);

        if (cachedRigidbody != null)
        {
            restoreKinematicState = cachedRigidbody.isKinematic;
            restoreGravityState = cachedRigidbody.useGravity;
            cachedRigidbody.isKinematic = true;
            cachedRigidbody.useGravity = false;
        }

        transform.SetParent(hand, false);
        SnapToHand(hand);
    }

    private void SnapToHand(Transform hand)
    {
        transform.SetParent(hand, false);
        transform.localPosition = heldLocalPositionOffset;
        transform.localRotation = Quaternion.Euler(heldLocalEulerOffset);
    }

    private HandCandidate GetNearestHandInAssistRange()
    {
        HandCandidate best = default;
        EvaluateHand(runner.LeftHandTransform, true, ref best);
        EvaluateHand(runner.RightHandTransform, false, ref best);
        return best;
    }

    private void EvaluateHand(Transform hand, bool leftSide, ref HandCandidate best)
    {
        if (hand == null)
        {
            return;
        }

        float distance = Vector3.Distance(hand.position, transform.position);
        if (distance > grabAssistRadius)
        {
            return;
        }

        if (!best.IsValid || distance < best.Distance)
        {
            best = new HandCandidate
            {
                Hand = hand,
                IsLeft = leftSide,
                Distance = distance
            };
        }
    }

    private void SetHighlighted(bool highlighted)
    {
        if (isHighlighted == highlighted || highlightRenderers == null)
        {
            return;
        }

        isHighlighted = highlighted;
        Color emission = highlighted ? highlightColor * highlightEmissionIntensity : Color.black;
        for (int i = 0; i < highlightRenderers.Length; i++)
        {
            Renderer targetRenderer = highlightRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            highlightBlock.Clear();
            highlightBlock.SetColor(EmissionColorId, emission);
            targetRenderer.SetPropertyBlock(highlighted ? highlightBlock : null);
        }
    }

    private void OnDisable()
    {
        SetHighlighted(false);
    }

    private struct HandCandidate
    {
        public Transform Hand;
        public bool IsLeft;
        public float Distance;
        public bool IsValid => Hand != null;
    }
}
