using DG.Tweening;
using UnityEngine;

public enum MoveableType { Rock, Barrel, Tree, Door }

public class Moveable : MonoBehaviour
{
    [Header("Type")]
    public MoveableType moveableType;

    [Header("Linked Collectible")]
    public CollectibleAnimator linkedCollectible;
    public GoldCollectibleItem linkedGoldCollectible;

    [Header("Rock / Tree — Translation")]
    public Vector2 moveOffset = new Vector2(-2f, 0f);
    public float moveDuration = 0.5f;

    [Header("Door — Child Activation")]
    public GameObject doorOpenChild;

    private bool _canInteract = false;
    private bool _activated   = false;
    private int  _collectiblesCollected = 0;
    private Vector3 _originalPosition;
    private Camera _sceneCamera;
    private Collider2D _col;

    private void Awake()
    {
        _originalPosition = transform.position;
        _col = GetComponent<Collider2D>();
        foreach (Camera c in FindObjectsOfType<Camera>())
            if (c.gameObject.scene == gameObject.scene) { _sceneCamera = c; break; }

    }

    private void OnEnable()  => GameEvents.OnBreakpointReached += HandleBreakpointReached;
    private void OnDisable()
    {
        GameEvents.OnBreakpointReached -= HandleBreakpointReached;
        DOTween.Kill(transform);
    }
    private void OnDestroy() => DOTween.Kill(transform);

    private void HandleBreakpointReached()
    {
        if (moveableType == MoveableType.Barrel)
        {
            // Barrel: collectible is already visible; just make it clickable
            linkedCollectible?.Activate();
            linkedGoldCollectible?.Activate();
            return;
        }
        _canInteract = true;
    }

    /// <summary>Directly makes this moveable interactable (used when revealed mid-game, not via BreakpointReached).</summary>
    public void MakeInteractable()
    {
        if (moveableType == MoveableType.Barrel)
        {
            linkedCollectible?.Activate();
            linkedGoldCollectible?.Activate();
            return;
        }
        _canInteract = true;
    }

    private void Update()
    {
        if (!_canInteract || _activated || _col == null || _sceneCamera == null) return;
        if (!Input.GetMouseButtonDown(0)) return;
        Vector2 worldPos = _sceneCamera.ScreenToWorldPoint(Input.mousePosition);
        if (!_col.OverlapPoint(worldPos)) return;
        OnClicked();
    }

    private void OnClicked()
    {
        _canInteract = false;
        _activated   = true;

        switch (moveableType)
        {
            case MoveableType.Rock:
                transform.DOMove(_originalPosition + (Vector3)(Vector2)moveOffset, moveDuration)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(ActivateCollectible);
                break;
            case MoveableType.Tree:
                transform.DOMove(_originalPosition + (Vector3)(Vector2)moveOffset, moveDuration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(ActivateCollectible);
                break;
            case MoveableType.Door:
                doorOpenChild?.SetActive(true);
                ActivateCollectible();
                break;
        }
    }

    private void ActivateCollectible()
    {
        linkedCollectible?.Activate();
        linkedGoldCollectible?.Activate();
    }

    // Called by CollectibleAnimator / GoldCollectibleItem after a collectible is collected
    public void OnCollectableCollected()
    {
        _collectiblesCollected++;
        int total = (linkedCollectible != null ? 1 : 0) + (linkedGoldCollectible != null ? 1 : 0);
        if (_collectiblesCollected < total) return;

        switch (moveableType)
        {
            case MoveableType.Rock:
                transform.DOMove(_originalPosition, moveDuration).SetEase(Ease.InQuad);
                break;
            case MoveableType.Tree:
                transform.DOMove(_originalPosition, moveDuration).SetEase(Ease.InBack);
                break;
            // Door: keep open sprite (no revert)
            // Barrel: nothing
        }
    }
}
