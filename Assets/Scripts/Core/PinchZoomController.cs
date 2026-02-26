using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PinchZoomController : MonoBehaviour
{
    [Header("Zoom Limits")]
    [Tooltip("Minimum orthographic size (closest zoom-in)")]
    public float minOrthographicSize = 2f;
    [Tooltip("Maximum orthographic size; 0 = auto-read from CameraAutoFit at Start")]
    public float maxOrthographicSize = 0f;

    [Header("Sensitivity")]
    public float pinchZoomSpeed = 0.05f;
    public float scrollZoomSpeed = 0.5f;

    [Header("Bounds")]
    [Tooltip("Background SpriteRenderer used to clamp camera position")]
    public SpriteRenderer background;

    private Camera _camera;
    private bool _zoomEnabled = true;
    private float _prevPinchDist;
    private bool _isPinching;

    private void Awake() => _camera = GetComponent<Camera>();

    private void Start()
    {
        if (maxOrthographicSize <= 0f)
            maxOrthographicSize = _camera.orthographicSize;
    }

    /// Called by MinigameMenuController to block zoom during puzzles.
    public void SetZoomEnabled(bool enabled)
    {
        _zoomEnabled = enabled;
        if (!enabled) _isPinching = false;
    }

    /// Called by CameraAutoFit after Refit() so max-size and position stay in sync.
    public void OnCameraAutoFitRefit()
    {
        maxOrthographicSize = _camera.orthographicSize;
        ResetCameraPosition();
    }

    private void ResetCameraPosition()
    {
        if (background == null) return;
        Vector3 pos = transform.position;
        pos.x = background.bounds.center.x;
        pos.y = background.bounds.center.y;
        transform.position = pos;
    }

    private void Update()
    {
        if (!_zoomEnabled) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        HandleScrollZoom();
#else
        HandlePinchZoom();
#endif
    }

    private void HandleScrollZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f) return;

        Vector3 worldBefore = _camera.ScreenToWorldPoint(Input.mousePosition);
        _camera.orthographicSize = Mathf.Clamp(
            _camera.orthographicSize - scroll * scrollZoomSpeed,
            minOrthographicSize, maxOrthographicSize);
        Vector3 worldAfter = _camera.ScreenToWorldPoint(Input.mousePosition);

        transform.position += worldBefore - worldAfter;
        ClampCameraPosition();
    }

    private void HandlePinchZoom()
    {
        if (Input.touchCount != 2)
        {
            _isPinching = false;
            return;
        }

        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);
        float curDist = Vector2.Distance(t0.position, t1.position);

        if (!_isPinching || t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
        {
            _prevPinchDist = curDist;
            _isPinching = true;
            return;
        }

        float delta = _prevPinchDist - curDist; // positive = pinch in = zoom in
        _prevPinchDist = curDist;
        if (Mathf.Abs(delta) < 0.001f) return;

        Vector2 midScreen = (t0.position + t1.position) * 0.5f;
        Vector3 screenPoint = new Vector3(midScreen.x, midScreen.y, _camera.nearClipPlane);

        Vector3 worldBefore = _camera.ScreenToWorldPoint(screenPoint);
        _camera.orthographicSize = Mathf.Clamp(
            _camera.orthographicSize + delta * pinchZoomSpeed,
            minOrthographicSize, maxOrthographicSize);
        Vector3 worldAfter = _camera.ScreenToWorldPoint(screenPoint);

        transform.position += worldBefore - worldAfter;
        ClampCameraPosition();
    }

    private void ClampCameraPosition()
    {
        if (background == null) return;

        float camH = _camera.orthographicSize;
        float camW = camH * (float)Screen.width / Screen.height;
        Bounds bg = background.bounds;

        float minX = bg.center.x - bg.extents.x + camW;
        float maxX = bg.center.x + bg.extents.x - camW;
        float minY = bg.center.y - bg.extents.y + camH;
        float maxY = bg.center.y + bg.extents.y - camH;

        // If camera view >= background size (zoomed out to max), just center it
        float clampedX = (minX <= maxX) ? Mathf.Clamp(transform.position.x, minX, maxX) : bg.center.x;
        float clampedY = (minY <= maxY) ? Mathf.Clamp(transform.position.y, minY, maxY) : bg.center.y;

        transform.position = new Vector3(clampedX, clampedY, transform.position.z);
    }
}
