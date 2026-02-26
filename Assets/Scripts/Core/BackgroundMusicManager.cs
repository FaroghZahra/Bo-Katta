using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusicManager : MonoBehaviour
{
    public AudioClip bgClip;
    public float loopDuration = 15f;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.clip = bgClip;
        _audioSource.loop = false;
        _audioSource.playOnAwake = false;
    }

    private void Start()
    {
        if (bgClip != null)
            StartCoroutine(PlayLoop());
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
