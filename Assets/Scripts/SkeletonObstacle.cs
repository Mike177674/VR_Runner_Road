using UnityEngine;

public class SkeletonObstacle : MonoBehaviour
{
    private const string PlayerTag = "Player";

    private void OnCollisionEnter(Collision collision)
    {
        TryGameOver(collision != null ? collision.collider : null);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryGameOver(other);
    }

    private static void TryGameOver(Collider other)
    {
        if (other == null || GameManager.Instance == null || !GameManager.Instance.IsPlaying)
        {
            return;
        }

        bool isPlayer = other.CompareTag(PlayerTag) || other.GetComponentInParent<RunnerLateralMovement>() != null;
        if (!isPlayer)
        {
            return;
        }

        GameManager.Instance.TriggerGameOver();
    }
}
