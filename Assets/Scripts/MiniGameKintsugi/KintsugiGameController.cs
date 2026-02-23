using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central input controller for the Kintsugi puzzle.
/// Mirrors GameController.cs (FoldIt): isEmbedded flag, clickBlocked, GameEvents.MinigameWon().
/// </summary>
public class KintsugiGameController : MonoBehaviour
{
    [Header("Embed / Standalone")]
    public bool   isEmbedded    = false;
    public string nextSceneName = "MainScene";

    [HideInInspector] public System.Action onComplete;
    [HideInInspector] public bool clickBlocked = false;

    private List<KintsugiPiece> _pieces    = new List<KintsugiPiece>();
    private int _snappedCount = 0;
    private int _totalPieces  = 0;

    private KintsugiPiece _draggedPiece;
    private Camera        _cam;

    // Layer mask for Physics2D overlap — layer 8 "KintsugiPieces"
    private static readonly int PieceLayer     = 8;
    private static readonly int PieceLayerMask = 1 << 8;

    // -------------------------------------------------------------------------

    void Awake()
    {
        _cam = Camera.main;
    }

    /// <summary>Called by KintsugiPuzzleGenerator once all pieces are created.</summary>
    public void SetPieces(List<KintsugiPiece> pieces)
    {
        _pieces      = pieces;
        _totalPieces = pieces.Count;
        _snappedCount = 0;
    }

    /// <summary>Retrieve a piece by index (used by KintsugiPiece for seam reveal).</summary>
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
            TryPickUp(world);
        }
        else if (Input.GetMouseButton(0) && _draggedPiece != null)
        {
            Vector3 world = ScreenToWorld(Input.mousePosition);
            _draggedPiece.UpdateDragPosition(world);
        }
        else if (Input.GetMouseButtonUp(0) && _draggedPiece != null)
        {
            TryRelease();
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
                TryPickUp(world);
                break;

            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                _draggedPiece?.UpdateDragPosition(world);
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                if (_draggedPiece != null) TryRelease();
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Pick-up / release
    // -------------------------------------------------------------------------

    void TryPickUp(Vector3 worldPos)
    {
        Collider2D hit = Physics2D.OverlapPoint(worldPos, PieceLayerMask);
        if (hit == null) return;

        KintsugiPiece piece = hit.GetComponent<KintsugiPiece>();
        if (piece == null || piece.isSnapped) return;

        _draggedPiece = piece;
        _draggedPiece.isDragging = true;

        // Offset so the piece doesn't jump to cursor centre
        Vector3 offset = piece.transform.position - worldPos;
        offset.z = 0f;
        _draggedPiece.dragOffset = offset;
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
    // Completion
    // -------------------------------------------------------------------------

    public void OnPieceSnapped(KintsugiPiece piece)
    {
        _snappedCount++;

        // Notify neighbours to reveal seams that are now ready
        foreach (var kvp in piece.adjacentSeams)
        {
            KintsugiPiece neighbor = GetPiece(kvp.Key);
            neighbor?.TryRevealSeamWith(piece.pieceIndex);
        }

        if (_snappedCount >= _totalPieces)
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
        if (_cam == null) _cam = Camera.main;
        Vector3 world = _cam.ScreenToWorldPoint(screenPos);
        world.z = 0f;
        return world;
    }
}
