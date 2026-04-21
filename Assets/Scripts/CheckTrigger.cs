using UnityEngine;
using System.Collections.Generic;

public class SectionTrigger : MonoBehaviour
{
    [Header("Drag all your Road Section prefabs in here")]
    public GameObject[] roadSections;

    [Header("Spawn position — match your current X=45 offset")]
    public Vector3 spawnPosition = new Vector3(45, 0, 0);

    [Header("Guarantee at least this many empty sections between obstacle sections")]
    public int minEasySectionGap = 2;

    private int sectionsSinceObstacle = 0;
    private readonly HashSet<int> processedTriggerIds = new();

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Trigger"))
        {
            if (!processedTriggerIds.Add(other.GetInstanceID()))
            {
                return;
            }

            Transform currentSection = other.transform.parent != null ? other.transform.parent : other.transform;
            SpawnNextSection(currentSection);
        }
    }

    void SpawnNextSection(Transform currentSection)
    {
        GameObject chosen;
        GameObject easySection = roadSections != null && roadSections.Length > 0 ? roadSections[0] : null;
        int effectiveMinEasySectionGap = GameManager.Instance != null
            ? GameManager.Instance.CurrentMinEasySectionGap
            : minEasySectionGap;

        if (easySection != null && sectionsSinceObstacle < effectiveMinEasySectionGap)
        {
            // Force an easy section between obstacle sections.
            chosen = easySection;
            sectionsSinceObstacle++;
        }
        else
        {
            chosen = roadSections[Random.Range(0, roadSections.Length)];

            if (chosen == easySection)
                sectionsSinceObstacle++;
            else
                sectionsSinceObstacle = 0;
        }

        Vector3 nextSpawnPosition = spawnPosition;
        Quaternion nextSpawnRotation = Quaternion.identity;

        if (currentSection != null)
        {
            nextSpawnPosition = currentSection.position + spawnPosition;
            nextSpawnRotation = currentSection.rotation;
        }

        Instantiate(chosen, nextSpawnPosition, nextSpawnRotation);
    }
}
