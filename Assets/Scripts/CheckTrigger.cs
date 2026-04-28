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

    [Header("Skeleton obstacle spawning")]
    [SerializeField] private Transform skeletonTemplate;
    [SerializeField, Range(0f, 1f)] private float skeletonSpawnChance = 0.75f;
    [SerializeField, Min(0)] private int minSectionsBetweenSkeletonSpawns = 1;
    [SerializeField, Min(0)] private int maxSectionsBetweenSkeletonSpawns = 3;
    [SerializeField] private bool spawnSkeletonsOnEasySections = true;
    [SerializeField] private bool hideTemplateSkeletonInScene = false;
    [SerializeField] private Vector3 skeletonLocalSpawnOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector2 skeletonLocalXJitterRange = new Vector2(-0.5f, 0.5f);
    [SerializeField] private Vector2 skeletonLocalZJitterRange = new Vector2(-1.1f, 1.1f);
    [SerializeField] private Vector3 skeletonLocalRotationOffset = new Vector3(0f, -90f, 0f);
    [SerializeField] private bool logSkeletonSpawns = true;

    private int sectionsSinceObstacle = 0;
    private readonly HashSet<int> processedTriggerIds = new();
    private bool skeletonTemplatePrepared;
    private bool missingTemplateWarned;
    private int sectionsUntilNextSkeletonSpawn;

    private void Awake()
    {
        sectionsUntilNextSkeletonSpawn = RollSectionsUntilNextSkeletonSpawn();
        PrepareSkeletonTemplate();
    }

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

        GameObject spawnedSection = Instantiate(chosen, nextSpawnPosition, nextSpawnRotation);
        TrySpawnSkeletonObstacle(spawnedSection, chosen == easySection);
    }

    private void PrepareSkeletonTemplate()
    {
        if (skeletonTemplatePrepared || skeletonTemplate == null)
        {
            return;
        }

        EnsureObstacleCollision(skeletonTemplate.gameObject);
        if (hideTemplateSkeletonInScene)
        {
            skeletonTemplate.gameObject.SetActive(false);
        }
        skeletonTemplatePrepared = true;
    }

    private void TrySpawnSkeletonObstacle(GameObject spawnedSection, bool spawnedEasySection)
    {
        if (spawnedSection == null)
        {
            return;
        }

        if (skeletonTemplate == null)
        {
            if (!missingTemplateWarned)
            {
                Debug.LogWarning("SectionTrigger: Assign Skeleton Template in the inspector to spawn skeleton obstacles.");
                missingTemplateWarned = true;
            }
            return;
        }

        PrepareSkeletonTemplate();

        if (!spawnSkeletonsOnEasySections && spawnedEasySection && HasNonEasySectionsConfigured())
        {
            if (logSkeletonSpawns)
            {
                Debug.Log("SectionTrigger: Skipped skeleton spawn on easy section.");
            }
            return;
        }

        if (sectionsUntilNextSkeletonSpawn > 0)
        {
            sectionsUntilNextSkeletonSpawn--;
            if (logSkeletonSpawns)
            {
                Debug.Log($"SectionTrigger: Waiting {sectionsUntilNextSkeletonSpawn} more sections for next skeleton.");
            }
            return;
        }

        if (Random.value > skeletonSpawnChance)
        {
            sectionsUntilNextSkeletonSpawn = RollSectionsUntilNextSkeletonSpawn();
            if (logSkeletonSpawns)
            {
                Debug.Log($"SectionTrigger: Skipped skeleton spawn due to spawn chance. Next attempt in {sectionsUntilNextSkeletonSpawn} sections.");
            }
            return;
        }

        GameObject obstacle = Instantiate(skeletonTemplate.gameObject);
        obstacle.name = $"{skeletonTemplate.gameObject.name}_Spawned";
        obstacle.SetActive(true);

        float localXOffset = Random.Range(skeletonLocalXJitterRange.x, skeletonLocalXJitterRange.y);
        float localZOffset = Random.Range(skeletonLocalZJitterRange.x, skeletonLocalZJitterRange.y);
        Vector3 randomizedOffset = skeletonLocalSpawnOffset + new Vector3(localXOffset, 0f, localZOffset);

        Transform obstacleTransform = obstacle.transform;
        obstacleTransform.position = spawnedSection.transform.TransformPoint(randomizedOffset);
        obstacleTransform.rotation = spawnedSection.transform.rotation * Quaternion.Euler(skeletonLocalRotationOffset);
        obstacleTransform.SetParent(spawnedSection.transform, true);
        sectionsUntilNextSkeletonSpawn = RollSectionsUntilNextSkeletonSpawn();

        EnsureObstacleCollision(obstacle);

        if (logSkeletonSpawns)
        {
            Debug.Log($"SectionTrigger: Spawned skeleton '{obstacle.name}' at {obstacleTransform.position}. Next spawn in {sectionsUntilNextSkeletonSpawn} sections.");
        }
    }

    private bool HasNonEasySectionsConfigured()
    {
        return roadSections != null && roadSections.Length > 1;
    }

    private int RollSectionsUntilNextSkeletonSpawn()
    {
        int minSections = Mathf.Max(0, minSectionsBetweenSkeletonSpawns);
        int maxSections = Mathf.Max(minSections, maxSectionsBetweenSkeletonSpawns);
        return Random.Range(minSections, maxSections + 1);
    }

    private static void EnsureObstacleCollision(GameObject obstacleRoot)
    {
        if (obstacleRoot == null)
        {
            return;
        }

        if (obstacleRoot.GetComponent<SkeletonObstacle>() == null)
        {
            obstacleRoot.AddComponent<SkeletonObstacle>();
        }

        Collider[] colliders = obstacleRoot.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].isTrigger = false;
            }

            return;
        }

        CapsuleCollider fallbackCollider = obstacleRoot.AddComponent<CapsuleCollider>();
        fallbackCollider.center = new Vector3(0f, 1f, 0f);
        fallbackCollider.height = 2f;
        fallbackCollider.radius = 0.35f;
        fallbackCollider.isTrigger = false;
    }
}
