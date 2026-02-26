using DG.Tweening;
using UnityEngine;

public class BridgeRepairReward : MonoBehaviour
{
    [Header("Sprites")]
    public SpriteRenderer rewardRenderer;   // the l1-bridge-repaired SpriteRenderer

    [Header("Target")]
    public Transform snapTarget;            // empty GO placed at the broken bridge position

    [Header("Timing")]
    public float spinDuration  = 0.8f;
    public float snapDuration  = 0.4f;

    private Camera _cam;

    private void Awake()
    {
        foreach (Camera c in FindObjectsOfType<Camera>())
            if (c.gameObject.scene == gameObject.scene) { _cam = c; break; }

        // starts hidden; Play() reveals it
        if (rewardRenderer != null)
            rewardRenderer.gameObject.SetActive(false);
    }

    public void Play(System.Action onComplete = null)
    {
        if (rewardRenderer == null || snapTarget == null) { onComplete?.Invoke(); return; }

        Transform t = rewardRenderer.transform;

        // Place at screen centre, reset scale & rotation
        Vector3 centre = _cam != null
            ? new Vector3(_cam.transform.position.x, _cam.transform.position.y, 0f)
            : Vector3.zero;
        t.position   = centre;
        t.localScale = Vector3.one;
        t.rotation   = Quaternion.identity;

        rewardRenderer.gameObject.SetActive(true);

        DOTween.Sequence()
            // Phase 1 — one full spin at screen centre
            .Append(t.DORotate(new Vector3(0f, 0f, -360f), spinDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.OutCubic))
            // Phase 2 — fly & snap to bridge position
            .Append(t.DOMove(snapTarget.position, snapDuration)
                .SetEase(Ease.OutBack))
            .OnComplete(() => onComplete?.Invoke());
    }
}
