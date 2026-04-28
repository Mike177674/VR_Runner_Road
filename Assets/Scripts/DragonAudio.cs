using UnityEngine;

public class DragonAudio : MonoBehaviour
{
    [SerializeField] private AudioClip roarClip;
    [SerializeField] private AudioClip flappingClip;
    [SerializeField, Range(0f, 1f)] private float flappingVolume = 0.25f;
    [SerializeField, Min(1f)] private float minRoarInterval = 20f;
    [SerializeField, Min(1f)] private float maxRoarInterval = 35f;

    private AudioSource roarSource;
    private AudioSource flappingSource;
    private bool wasPlaying;
    private float nextRoarCountdown;

    private void Awake()
    {
        roarSource = gameObject.AddComponent<AudioSource>();
        roarSource.playOnAwake = false;
        roarSource.spatialBlend = 0f;

        flappingSource = gameObject.AddComponent<AudioSource>();
        flappingSource.clip = flappingClip;
        flappingSource.loop = true;
        flappingSource.volume = flappingVolume;
        flappingSource.playOnAwake = false;
        flappingSource.spatialBlend = 0f;
    }

    private void Update()
    {
        bool isPlaying = GameManager.Instance != null && GameManager.Instance.IsPlaying;

        if (isPlaying && !wasPlaying)
        {
            OnRunStarted();
        }
        else if (!isPlaying && wasPlaying)
        {
            flappingSource.Stop();
        }

        if (isPlaying)
        {
            nextRoarCountdown -= Time.deltaTime;
            if (nextRoarCountdown <= 0f)
            {
                PlayRoar();
                ScheduleNextRoar();
            }
        }

        wasPlaying = isPlaying;
    }

    private void OnRunStarted()
    {
        PlayRoar();
        ScheduleNextRoar();

        if (flappingClip != null)
            flappingSource.Play();
    }

    private void PlayRoar()
    {
        if (roarClip != null)
            roarSource.PlayOneShot(roarClip);
    }

    private void ScheduleNextRoar()
    {
        nextRoarCountdown = Random.Range(minRoarInterval, maxRoarInterval);
    }
}
