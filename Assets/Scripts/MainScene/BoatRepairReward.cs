using System;
using DG.Tweening;
using UnityEngine;

public class BoatRepairReward : MonoBehaviour
{
    [Header("Sprite")]
    public SpriteRenderer rewardRenderer;
    public Sprite finalSprite;          // swapped in after the snap lands
    public Transform snapTarget;
    public Transform reparentTo;        // boat GO — sprite becomes its child so it sails away with it

    [Header("Timing")]
    public float scaleDuration     = 0.5f;
    public float scaleBackDuration = 0.3f;
    public float snapDuration      = 0.4f;
    public float bigScale          = 1.8f;
    public float snapScale         = 0.4f; // final scale to match the snap target

    private Camera _cam;

    private void Awake()
    {
        foreach (Camera c in FindObjectsOfType<Camera>())
            if (c.gameObject.scene == gameObject.scene) { _cam = c; break; }

        rewardRenderer?.gameObject.SetActive(false);
    }

    public void Play(Action onComplete = null)
    {
        if (rewardRenderer == null || snapTarget == null) { onComplete?.Invoke(); return; }

        Transform t = rewardRenderer.transform;

        Vector3 centre = _cam != null
            ? new Vector3(_cam.transform.position.x, _cam.transform.position.y, 0f)
            : Vector3.zero;
        t.position   = centre;
        t.localScale = Vector3.one;
        t.rotation   = Quaternion.identity;

        rewardRenderer.gameObject.SetActive(true);

        DOTween.Sequence()
            .Append(t.DOScale(bigScale * Vector3.one, scaleDuration).SetEase(Ease.OutCubic))
            .Join(t.DORotate(new Vector3(0f, 0f, -180f), scaleDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear))
            .Append(t.DOScale(Vector3.one, scaleBackDuration).SetEase(Ease.InCubic))
            .Append(t.DOMove(snapTarget.position, snapDuration).SetEase(Ease.OutBack))
            .Join(t.DOScale(snapScale * Vector3.one, snapDuration).SetEase(Ease.OutBack))
            .OnComplete(() =>
            {
                if (finalSprite != null)
                    rewardRenderer.sprite = finalSprite;
                // Parent to snapTarget directly — localPosition zero = exact position, no drift.
                // snapTarget must be a child of the boat in the hierarchy so it sails away with it.
                t.SetParent(snapTarget, worldPositionStays: false);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale    = Vector3.one;

                if (reparentTo != null)
                {
                    var boatRenderer = reparentTo.GetComponent<SpriteRenderer>();
                    if (boatRenderer != null)
                    {
                        rewardRenderer.sortingLayerName = boatRenderer.sortingLayerName;
                        rewardRenderer.sortingOrder     = boatRenderer.sortingOrder + 1;
                    }
                }
                DOVirtual.DelayedCall(1f, () => onComplete?.Invoke());
            });
    }
}
