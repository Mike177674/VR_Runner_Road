using UnityEngine;

public class DragonAudio : MonoBehaviour
{
    public AudioClip flappingClip;
    public AudioClip roarClip;

    [Range(5f, 60f)] public float roarIntervalMin = 10f;
    [Range(5f, 60f)] public float roarIntervalMax = 25f;

    private AudioSource flappingSource;
    private AudioSource roarSource;
    private float nextRoarTime;
    private bool wasPlaying;

    void Start()
    {
        flappingSource = gameObject.AddComponent<AudioSource>();
        flappingSource.clip = flappingClip;
        flappingSource.loop = true;
        flappingSource.spatialBlend = 1f;

        roarSource = gameObject.AddComponent<AudioSource>();
        roarSource.spatialBlend = 1f;
    }

    void Update()
    {
        bool isPlaying = GameManager.Instance != null && GameManager.Instance.IsPlaying;

        if (isPlaying && !wasPlaying)
        {
            flappingSource.Play();
            roarSource.PlayOneShot(roarClip);
            ScheduleNextRoar();
        }
        else if (!isPlaying && wasPlaying)
        {
            flappingSource.Stop();
        }

        if (isPlaying && Time.time >= nextRoarTime && !roarSource.isPlaying)
        {
            roarSource.PlayOneShot(roarClip);
            ScheduleNextRoar();
        }

        wasPlaying = isPlaying;
    }

    void ScheduleNextRoar()
    {
        nextRoarTime = Time.time + Random.Range(roarIntervalMin, roarIntervalMax);
    }
}
