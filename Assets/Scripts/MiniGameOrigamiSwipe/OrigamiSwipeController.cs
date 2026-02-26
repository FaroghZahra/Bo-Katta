using UnityEngine;

/// <summary>
/// Origami Swipe minigame (Level 2).
/// Player performs horizontal drag strokes; accumulated distance drives
/// 6-frame sun sprite animation. Reaching completionDistance fires onComplete.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class OrigamiSwipeController : MonoBehaviour
{
    [Header("Frame Sprites")]
    public Sprite[] frames;                  // 6 elements: Suns/1(1), 2(1), 3, 4, 5, 6

    [Header("Display")]
    public SpriteRenderer displayRenderer;   // child GO "SunDisplay"

    [Header("Swipe Settings")]
    [Tooltip("Multiply how much each pixel of drag contributes.\n>1 = faster (shorter swipe needed)\n<1 = slower (larger swipe needed)")]
    public float swipeSpeed          = 1f;

    [Tooltip("When enabled, completionDistance is calculated as a fraction of Screen.width at runtime, so the required swipe scales with the device screen.")]
    public bool  useScreenWidth      = true;
    [Tooltip("Fraction of screen width required for a full swipe (0.8 = 80% of screen). Only used when useScreenWidth is enabled.")]
    public float screenWidthFraction = 0.8f;

    [Tooltip("Fixed pixel distance to complete the swipe. Only used when useScreenWidth is disabled.")]
    public float completionDistance  = 400f;
    public float deadZone            = 10f;  // pixels ignored at start of each stroke
    public bool  requireRightwardSwipe = true;

    [HideInInspector] public System.Action onComplete;

    [Header("Audio")]
    [SerializeField] private AudioClip _winSound;
    private AudioSource _audioSource;

    private bool    _isDragging    = false;
    private Vector2 _dragStart     = Vector2.zero;
    private float   _totalProgress = 0f;   // accumulates across multiple strokes
    private int     _currentFrame  = 0;
    private bool    _completed     = false;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
    }

    void Start()
    {
        if (frames == null || frames.Length == 0)
        {
            Debug.LogError("[OrigamiSwipe] frames array is empty — assign sprites in Inspector.");
            return;
        }
        if (displayRenderer == null)
        {
            Debug.LogError("[OrigamiSwipe] displayRenderer is not assigned.");
            return;
        }
        displayRenderer.sprite = frames[0];

        if (useScreenWidth)
            completionDistance = Screen.width * screenWidthFraction;
    }

    void Update()
    {
        if (_completed) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
    }

    // -------------------------------------------------------------------------
    // Input routing
    // -------------------------------------------------------------------------

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            BeginDrag(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0) && _isDragging)
        {
            ContinueDrag(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            EndDrag();
        }
    }

    void HandleTouchInput()
    {
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);
        switch (touch.phase)
        {
            case TouchPhase.Began:
                BeginDrag(touch.position);
                break;
            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                if (_isDragging) ContinueDrag(touch.position);
                break;
            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                EndDrag();
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Drag logic
    // -------------------------------------------------------------------------

    void BeginDrag(Vector2 screenPos)
    {
        _dragStart  = screenPos;
        _isDragging = true;
        // _totalProgress intentionally NOT reset — strokes accumulate
    }

    void ContinueDrag(Vector2 screenPos)
    {
        float deltaX = screenPos.x - _dragStart.x;
        _dragStart = screenPos;  // advance anchor so next call gets incremental delta

        if (requireRightwardSwipe && deltaX < 0f) return;

        // Strip dead zone from the raw delta magnitude
        float absDelta = Mathf.Abs(deltaX);
        if (absDelta <= deadZone) return;
        absDelta -= deadZone;

        _totalProgress += absDelta * swipeSpeed;

        // Map progress → frame index
        float t         = Mathf.Clamp01(_totalProgress / completionDistance);
        int   frameIndex = Mathf.Clamp(Mathf.FloorToInt(t * frames.Length), 0, frames.Length - 1);

        if (frameIndex != _currentFrame)
        {
            _currentFrame = frameIndex;
            displayRenderer.sprite = frames[_currentFrame];
        }

        if (_totalProgress >= completionDistance)
        {
            // Ensure last frame is showing
            displayRenderer.sprite = frames[frames.Length - 1];
            Complete();
        }
    }

    void EndDrag()
    {
        _isDragging = false;
        // Progress is kept so the next stroke continues from where the player left off
    }

    // -------------------------------------------------------------------------
    // Completion
    // -------------------------------------------------------------------------

    void Complete()
    {
        if (_completed) return;
        _completed = true;
        StartCoroutine(PlayWinSoundThenComplete());
    }

    System.Collections.IEnumerator PlayWinSoundThenComplete()
    {
        if (_winSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_winSound);
            yield return new WaitForSeconds(_winSound.length);
        }
        onComplete?.Invoke();
    }
}
