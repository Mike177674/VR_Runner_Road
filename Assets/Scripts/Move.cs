using UnityEngine;

public class Move : MonoBehaviour
{
    private const float FallbackWorldSpeed = 4f;

    private void Update()
    {
        float currentWorldSpeed = GameManager.Instance != null
            ? GameManager.Instance.CurrentWorldSpeed
            : FallbackWorldSpeed;

        transform.position += Vector3.left * (currentWorldSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Destroy"))
        {
            Destroy(gameObject);
        }
    }
}
