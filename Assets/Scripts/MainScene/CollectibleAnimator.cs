using DG.Tweening;
using UnityEngine;

public class CollectibleAnimator : MonoBehaviour
{
    [Header("Pulse Animation")]
    public float pulseScale = 1.15f;
    public float pulseDuration = 0.6f;

    private bool _interactable = false;
    private Camera _sceneCamera;
    private Collider2D _col;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col == null)
            Debug.LogWarning($"CollectibleAnimator on '{name}': no Collider2D — clicks will never register.", this);

        // Find the camera that belongs to this scene (same approach as MinigameMenuController).
        // Avoids relying on Camera.main which may be untagged.
        foreach (Camera c in FindObjectsOfType<Camera>())
        {
            if (c.gameObject.scene == gameObject.scene) { _sceneCamera = c; break; }
        }
    }

    private void OnEnable()  => GameEvents.OnBreakpointReached += Activate;
    private void OnDisable()
    {
        GameEvents.OnBreakpointReached -= Activate;
        DOTween.Kill(transform);
    }

    private void OnDestroy() => DOTween.Kill(transform);

    private void Activate()
    {
        _interactable = true;
        PlayPulse();
    }

    private void Update()
    {
        if (!_interactable || _col == null || _sceneCamera == null) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 worldPos = _sceneCamera.ScreenToWorldPoint(Input.mousePosition);
        if (!_col.OverlapPoint(worldPos)) return;

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
