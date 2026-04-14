using UnityEngine;

public class SectionTrigger : MonoBehaviour
{
    [Header("Drag all your Road Section prefabs in here")]
    public GameObject[] roadSections;

    [Header("Spawn position — match your current X=45 offset")]
    public Vector3 spawnPosition = new Vector3(45, 0, 0);

    [Header("Guarantee at least this many empty sections between obstacle sections")]
    public int minEasySectionGap = 2;

    private int sectionsSinceObstacle = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Trigger"))
        {
            SpawnNextSection();
        }
    }

    void SpawnNextSection()
    {
        GameObject chosen;

        if (sectionsSinceObstacle < minEasySectionGap)
        {
            // Force an empty/easy section
            chosen = roadSections[0]; // Assumes index 0 = RoadSection_Empty
            sectionsSinceObstacle++;
        }
        else
        {
            // Pick randomly from all sections
            chosen = roadSections[Random.Range(0, roadSections.Length)];

            // Reset counter if we just spawned a non-empty section
            if (chosen.name.Contains("Empty"))
                sectionsSinceObstacle++;
            else
                sectionsSinceObstacle = 0;
        }

        Instantiate(chosen, spawnPosition, Quaternion.identity);
    }
}