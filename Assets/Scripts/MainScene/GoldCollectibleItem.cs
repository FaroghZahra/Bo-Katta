using UnityEngine;

public class GoldCollectibleItem : MonoBehaviour
{
    [Header("Moveable Link")]
    public Moveable linkedMoveable;

    private bool _interactable = false;
    private Camera _sceneCamera;
    private Collider2D _col;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        foreach (Camera c in FindObjectsOfType<Camera>())
        {
            if (c.gameObject.scene == gameObject.scene) { _sceneCamera = c; break; }
        }
    }

    private void OnEnable()
    {
        GameEvents.OnBreakpointReached += OnBreakpointReached;
    }

    private void OnDisable()
    {
        GameEvents.OnBreakpointReached -= OnBreakpointReached;
    }

    public void Activate()
    {
        _interactable = true;
    }

    private void OnBreakpointReached()
    {
        // Only auto-activate on breakpoint if there is no moveable controlling this item
        if (linkedMoveable == null)
            _interactable = true;
    }

    private void Update()
    {
        if (!_interactable || _col == null || _sceneCamera == null) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 worldPos = _sceneCamera.ScreenToWorldPoint(Input.mousePosition);
        if (!_col.OverlapPoint(worldPos)) return;

        _interactable = false;
        CollectibleManager.Instance?.CollectGoldItem();
        linkedMoveable?.OnCollectableCollected();
        gameObject.SetActive(false);
    }
}
