using DG.Tweening;
using UnityEngine;

public class CollectibleAnimator : MonoBehaviour
{
    [Header("Pulse Animation")]
    public float pulseScale = 1.15f;
    public float pulseDuration = 0.6f;

    private bool _interactable = false;

    private void OnEnable()  => GameEvents.OnBreakpointReached += Activate;
    private void OnDisable()
    {
        GameEvents.OnBreakpointReached -= Activate;
        DOTween.Kill(transform);
    }

    private void OnDestroy()
    {
        DOTween.Kill(transform);
    }

    private void Activate()
    {
        _interactable = true;
        PlayPulse();
    }

    private void OnMouseDown()
    {
        if (!_interactable) return;

        CollectibleLevel cl = GetComponent<CollectibleLevel>();
        int levelIndex = cl != null ? cl.levelIndex : 1;
        CollectibleManager.Instance?.Collect(gameObject, levelIndex);
    }

    public void Reactivate()
    {
        _interactable = true;
        DOTween.Kill(transform);
        transform.localScale = Vector3.one;
        PlayPulse();
    }

    private void PlayPulse()
    {
        transform.DOScale(Vector3.one * pulseScale, pulseDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }
}
