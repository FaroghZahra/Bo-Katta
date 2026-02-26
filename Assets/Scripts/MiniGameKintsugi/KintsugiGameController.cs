using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tracks the tracing state of a single gold seam between two snapped pieces.
/// </summary>
public class SeamTraceState
{
    public LineRenderer goldLR;    // Progress indicator — grows from 0 to N points
    public LineRenderer guideLR;   // Full-path faint hint — visible when seam is active
    public Vector3[]    points;    // World-space seam points (N = tearSubdivisions+1)
    public int          progress;  // Points confirmed traced (0 = untouched, N = done)
    public bool         isActive;  // True once both adjacent pieces are snapped

    public bool IsFullyTraced => progress >= points.Length;
}

/// <summary>
/// Central input controller for the Kintsugi puzzle.
/// Mirrors GameController.cs (FoldIt): isEmbedded flag, clickBlocked, GameEvents.MinigameWon().
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class KintsugiGameController : MonoBehaviour
{
    [Header("Embed / Standalone")]
    public bool   isEmbedded    = false;
    public string nextSceneName = "MainScene";

    [HideInInspector] public System.Action onComplete;
    [HideInInspector] public bool clickBlocked = false;

    [Header("Audio")]
    [SerializeField] private AudioClip _snapSound;
    private AudioSource _audioSource;

    [Header("Tracing")]
    public float traceProximity = 0.25f;   // World-unit reach for cursor→seam-point detection

    private List<KintsugiPiece>    _pieces    = new List<KintsugiPiece>();
    private int _snappedCount = 0;
    private int _totalPieces  = 0;

    private KintsugiPiece _draggedPiece;
    private Camera        _cam;

    private List<SeamTraceState> _allSeams   = new List<SeamTraceState>();
    private SeamTraceState       _activeSeam;

    // Layer mask for Physics2D overlap — layer 8 "KintsugiPieces"
    private static readonly int PieceLayer     = 8;
    private static readonly int PieceLayerMask = 1 << 8;

    // -------------------------------------------------------------------------

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        // Prefer a camera in the same scene (safe under additive loading).
        // Camera.main can be ambiguous or null when two scenes are loaded together.
        foreach (Camera c in FindObjectsOfType<Camera>())
        {
            if (c.gameObject.scene == gameObject.scene) { _cam = c; break; }
        }
        if (_cam == null) _cam = Camera.main;
    }

    /// <summary>Called by KintsugiPuzzleGenerator once all pieces are created.</summary>
    public void SetPieces(List<KintsugiPiece> pieces)
    {
        _pieces      = pieces;
        _totalPieces = pieces.Count;
        _snappedCount = 0;
    }

    /// <summary>Called by KintsugiPuzzleGenerator once all seams are created.</summary>
    public void SetSeams(List<SeamTraceState> seams)
    {
        _allSeams = seams;
    }

    /// <summary>Retrieve a piece by index (used by KintsugiPiece for seam activation).</summary>
    public KintsugiPiece GetPiece(int index)
    {
        if (index < 0 || index >= _pieces.Count) return null;
        return _pieces[index];
    }

    // -------------------------------------------------------------------------
    // Update — platform-split identical to GameController.cs
    // -------------------------------------------------------------------------

    void Update()
    {
        if (clickBlocked) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
    }

    // -------------------------------------------------------------------------
    // Mouse input
    // -------------------------------------------------------------------------

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 world = ScreenToWorld(Input.mousePosition);
            if (!TryPickUp(world))
                TryStartTrace(world);
        }
        else if (Input.GetMouseButton(0))
        {
            Vector3 world = ScreenToWorld(Input.mousePosition);
            if (_draggedPiece != null)
                _draggedPiece.UpdateDragPosition(world);
            else if (_activeSeam != null)
                UpdateTrace(world);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (_draggedPiece != null)
                TryRelease();
            EndTrace();
        }
    }

    // -------------------------------------------------------------------------
    // Touch input
    // -------------------------------------------------------------------------

    void HandleTouchInput()
    {
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);
        Vector3 world = ScreenToWorld(touch.position);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                if (!TryPickUp(world))
                    TryStartTrace(world);
                break;

            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                if (_draggedPiece != null)
                    _draggedPiece.UpdateDragPosition(world);
                else if (_activeSeam != null)
                    UpdateTrace(world);
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                if (_draggedPiece != null) TryRelease();
                EndTrace();
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Pick-up / release
    // -------------------------------------------------------------------------

    bool TryPickUp(Vector3 worldPos)
    {
        Collider2D hit = Physics2D.OverlapPoint(worldPos, PieceLayerMask);
        if (hit == null) return false;

        KintsugiPiece piece = hit.GetComponent<KintsugiPiece>();
        if (piece == null || piece.isSnapped) return false;

        _draggedPiece = piece;
        _draggedPiece.isDragging = true;

        // Offset so the piece doesn't jump to cursor centre
        Vector3 offset = piece.transform.position - worldPos;
        offset.z = 0f;
        _draggedPiece.dragOffset = offset;
        return true;
    }

    void TryRelease()
    {
        if (_draggedPiece == null) return;

        KintsugiPiece releasing = _draggedPiece;
        _draggedPiece = null;

        // TrySnap handles clickBlocked internally via coroutine
        if (!releasing.TrySnap())
            releasing.isDragging = false;
    }

    // -------------------------------------------------------------------------
    // Seam tracing
    // -------------------------------------------------------------------------

    void TryStartTrace(Vector3 world)
    {
        SeamTraceState best        = null;
        float          bestDist    = traceProximity * 2f;  // wider pickup radius
        int            bestNearIdx = 0;

        foreach (var seam in _allSeams)
        {
            if (!seam.isActive || seam.IsFullyTraced) continue;

            // For partial traces search from the current tip so the player
            // clicks near where they left off; for fresh seams search all points.
            int startIdx = seam.progress > 0 ? Mathf.Max(0, seam.progress - 1) : 0;

            for (int i = startIdx; i < seam.points.Length; i++)
            {
                float d = Vector2.Distance(world, seam.points[i]);  // ignore Z (-1 vs 0 gap)
                if (d < bestDist)
                {
                    bestDist    = d;
                    best        = seam;
                    bestNearIdx = i;
                }
            }
        }

        if (best == null) return;

        if (best.progress == 0)
        {
            // Fresh seam: allow either direction, reverse if click is on the far half
            bool reverse = bestNearIdx > best.points.Length / 2;
            if (reverse)
            {
                System.Array.Reverse(best.points);
            }
            best.goldLR.enabled       = true;
            best.goldLR.positionCount = 1;
            best.goldLR.SetPosition(0, best.points[0]);
            best.progress = 1;
        }
        else
        {
            // Resumed partial seam — re-bake visible portion so the LR shows the
            // correct tear shape (positionCount may have been truncated on last release)
            best.goldLR.positionCount = best.progress;
            for (int i = 0; i < best.progress; i++)
                best.goldLR.SetPosition(i, best.points[i]);
        }

        _activeSeam = best;
    }

    void UpdateTrace(Vector3 world)
    {
        if (_activeSeam == null) return;

        // Advance using segment projection so the user only needs to drag
        // generally along the seam — no need to land exactly on each point.
        int n = _activeSeam.points.Length;
        while (_activeSeam.progress < n)
        {
            Vector3 prevPt = _activeSeam.points[_activeSeam.progress - 1];
            Vector3 nextPt = _activeSeam.points[_activeSeam.progress];
            Vector3 seg    = nextPt - prevPt;
            float   segLen = seg.magnitude;

            bool advance;
            if (segLen < 0.001f)
            {
                advance = true;
            }
            else
            {
                // Advance once the cursor's projection along this segment
                // reaches its midpoint (generous threshold, natural drag feel)
                float proj = Vector3.Dot(world - prevPt, seg / segLen);
                advance = proj >= segLen * 0.5f;
            }

            if (advance) _activeSeam.progress++;
            else break;
        }

        // Always re-write positions from points[] — growing positionCount would
        // otherwise zero-init new slots instead of using the tear path.
        _activeSeam.goldLR.positionCount = _activeSeam.progress;
        for (int i = 0; i < _activeSeam.progress; i++)
            _activeSeam.goldLR.SetPosition(i, _activeSeam.points[i]);

        if (_activeSeam.IsFullyTraced)
        {
            _activeSeam.guideLR.enabled = false;  // hide guide, gold seam now complete
            _activeSeam = null;
            TryCheckCompletion();
        }
    }

    void EndTrace()
    {
        _activeSeam = null;
    }

    // -------------------------------------------------------------------------
    // Completion
    // -------------------------------------------------------------------------

    public void OnPieceSnapped(KintsugiPiece piece)
    {
        _snappedCount++;
        if (_snapSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_snapSound);

        // Notify neighbours to activate seams that are now ready
        foreach (var kvp in piece.adjacentSeams)
        {
            KintsugiPiece neighbor = GetPiece(kvp.Key);
            neighbor?.TryActivateSeamWith(piece.pieceIndex);
        }

        TryCheckCompletion();
    }

    void TryCheckCompletion()
    {
        if (_snappedCount < _totalPieces) return;
        foreach (var seam in _allSeams)
            if (!seam.IsFullyTraced) return;
        CheckCompletion();
    }

    void CheckCompletion()
    {
        if (onComplete != null)
            onComplete.Invoke();
        else if (isEmbedded)
            GameEvents.MinigameWon();
        else
            SceneManager.LoadScene(nextSceneName);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    Vector3 ScreenToWorld(Vector3 screenPos)
    {
        if (_cam == null)
        {
            foreach (Camera c in FindObjectsOfType<Camera>())
            {
                if (c.gameObject.scene == gameObject.scene) { _cam = c; break; }
            }
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return Vector3.zero;
        }
        Vector3 world = _cam.ScreenToWorldPoint(screenPos);
        world.z = 0f;
        return world;
    }
}
