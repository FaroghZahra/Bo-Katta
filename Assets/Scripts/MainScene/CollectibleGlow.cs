using DG.Tweening;
using UnityEngine;

/// <summary>
/// Soft pulsing halo drawn behind a collectible while it can be tapped, so small items read clearly
/// against busy art. Added automatically by CollectibleAnimator / GoldCollectibleItem.
/// </summary>
public class CollectibleGlow : MonoBehaviour
{
    const float SizeMultiplier = 2.4f;   // halo diameter relative to the item's largest side
    const float MinWorldSize   = 0.55f;  // tiny scraps still get a visible halo

    private SpriteRenderer _halo;
    private static Sprite _glowSprite;

    public static void Show(GameObject target)
    {
        var glow = target.GetComponent<CollectibleGlow>();
        if (glow == null) glow = target.AddComponent<CollectibleGlow>();
        glow.Begin();
    }

    private void Begin()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        if (_glowSprite == null) _glowSprite = Resources.Load<Sprite>("CollectibleGlow");
        if (_glowSprite == null) return;

        if (_halo == null)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(transform, false);
            _halo = go.AddComponent<SpriteRenderer>();
            _halo.sprite = _glowSprite;
            // Halo takes the item's slot (so it draws over whatever the item sits on, e.g. the matka)
            // and the item moves one step up to stay on top of its halo.
            _halo.sortingLayerID = sr.sortingLayerID;
            _halo.sortingOrder   = sr.sortingOrder;
            sr.sortingOrder     += 1;
            go.transform.position = sr.bounds.center;

            // Size in world units, independent of this object's (often tiny) scale.
            float target = Mathf.Max(MinWorldSize, Mathf.Max(sr.bounds.size.x, sr.bounds.size.y) * SizeMultiplier);
            float native = _glowSprite.bounds.size.x;
            Vector3 parentScale = transform.lossyScale;
            go.transform.localScale = new Vector3(target / native / Mathf.Abs(parentScale.x), target / native / Mathf.Abs(parentScale.y), 1f);
        }

        _halo.gameObject.SetActive(true);
        _halo.DOKill();
        _halo.transform.DOKill();
        _halo.color = new Color(1f, 0.95f, 0.8f, 0.55f);
        _halo.DOFade(0.95f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        Vector3 s = _halo.transform.localScale;
        _halo.transform.DOScale(s * 1.15f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }

    private void OnDisable()
    {
        if (_halo == null) return;
        _halo.DOKill();
        _halo.transform.DOKill();
    }
}
