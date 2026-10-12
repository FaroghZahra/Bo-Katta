using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusicManager : MonoBehaviour
{
    public AudioClip bgClip;
    [Tooltip("Restart the clip every N seconds (for short loops). 0 = play the whole track and loop it seamlessly.")]
    public float loopDuration = 15f;
    [Range(0f, 1f)] public float volume = 0.6f;
    public float fadeInSeconds = 2f;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.clip = bgClip;
        _audioSource.loop = loopDuration <= 0f;
        _audioSource.playOnAwake = false;
    }

    private void Start()
    {
        if (bgClip == null) return;
        if (loopDuration <= 0f)
        {
            _audioSource.volume = 0f;
            _audioSource.Play();
            StartCoroutine(FadeIn());
        }
        else
        {
            _audioSource.volume = volume;
            StartCoroutine(PlayLoop());
        }
    }

    private IEnumerator FadeIn()
    {
        for (float t = 0f; t < fadeInSeconds; t += Time.unscaledDeltaTime)
        {
            _audioSource.volume = Mathf.Lerp(0f, volume, t / fadeInSeconds);
            yield return null;
        }
        _audioSource.volume = volume;
    }

    private IEnumerator PlayLoop()
    {
        while (true)
        {
            _audioSource.time = 0f;
            _audioSource.Play();
            yield return new WaitForSeconds(loopDuration);
            _audioSource.Stop();
        }
    }
}
