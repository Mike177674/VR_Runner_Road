using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Move : MonoBehaviour
{
    [SerializeField] private float baseUnitsPerSecond = 4f;
    [SerializeField] private float initialSpeedMultiplier = 1f;
    [SerializeField] private float accelerationPerSecond = 0.06f;

    // Update is called once per frame
    void Update()
    {
        // All road pieces use the same run-time speed so spawned sections stay aligned.
        float currentSpeedMultiplier = initialSpeedMultiplier + (Time.time * accelerationPerSecond);
        transform.position += Vector3.left * (baseUnitsPerSecond * currentSpeedMultiplier * Time.deltaTime);
    }
    
    
    
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Destroy"))
        {
            Destroy(gameObject);
        }
    }
    
    
}
