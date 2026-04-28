// Claude
using UnityEngine;

public class InfiniteGround : MonoBehaviour
{
    [SerializeField] private float baseUnitsPerSecond = 4f;
    [SerializeField] private float initialSpeedMultiplier = 1f;
    [SerializeField] private float accelerationPerSecond = 0.06f;

    private Material mat;

    void Start()
    {
        mat = GetComponent<Renderer>().material;
    }

    void Update()
    {
        // Same speed logic as wall
        float currentSpeedMultiplier = initialSpeedMultiplier + (Time.time * accelerationPerSecond);
        float speed = baseUnitsPerSecond * currentSpeedMultiplier;

        Vector2 offset = mat.mainTextureOffset;
        offset.y -= speed * Time.deltaTime * 0.01f; // keep your scaling factor
        mat.mainTextureOffset = offset;
    }
}