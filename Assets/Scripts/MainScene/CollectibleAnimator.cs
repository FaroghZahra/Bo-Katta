using UnityEngine;

public class CollectibleAnimator : MonoBehaviour
{
    [Header("Moveable Link")]
    public Moveable linkedMoveable;

    private bool _interactable = false;
    private Camera _sceneCamera;
    private Collider2D _col;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col == null)
            Debug.LogWarning($"CollectibleAnimator on '{name}': no Collider2D — clicks will never register.", this);

        foreach (Camera c in FindObjectsOfType<Camera>())
        {
            if (c.gameObject.scene == gameObject.scene) { _sceneCamera = c; break; }
        }
    }

    public void Activate()
    {
        _interactable = true;
    }

    private void Update()
    {
        if (!_interactable || _col == null || _sceneCamera == null) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 worldPos = _sceneCamera.ScreenToWorldPoint(Input.mousePosition);
        if (!_col.OverlapPoint(worldPos)) return;

        _interactable = false;
        CollectibleLevel cl = GetComponent<CollectibleLevel>();
        int levelIndex = cl != null ? cl.levelIndex : 1;
        CollectibleManager.Instance?.Collect(gameObject, levelIndex);
        linkedMoveable?.OnCollectableCollected();
    }
}
