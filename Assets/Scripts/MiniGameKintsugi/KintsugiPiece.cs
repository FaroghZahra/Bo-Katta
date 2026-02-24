using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-piece behaviour for the Kintsugi puzzle.
/// Receives position commands from KintsugiGameController; never reads input itself.
/// </summary>
[RequireComponent(typeof(PolygonCollider2D))]
public class KintsugiPiece : MonoBehaviour
{
    // Set by KintsugiPuzzleGenerator via Initialize()
    public int pieceIndex;
    public Vector3 targetWorldPosition;
    public bool isSnapped   = false;
    public bool isDragging  = false;
    public Vector3 dragOffset;

    // neighborPieceIndex → SeamTraceState for the shared seam
    public Dictionary<int, SeamTraceState> adjacentSeams = new Dictionary<int, SeamTraceState>();

    private KintsugiGameController _gc;
    private float          _snapThreshold;
    private float          _snapDuration;
    private AnimationCurve _snapCurve;
    private Vector3 _scatteredPosition;

    // -------------------------------------------------------------------------

    public void Initialize(KintsugiGameController gc,
                           float snapThreshold,
                           float snapDuration,
                           AnimationCurve snapCurve,
                           Vector3 targetPos,
                           int     index,
                           Vector3 scatteredPos)
    {
        _gc               = gc;
        _snapThreshold    = snapThreshold;
        _snapDuration     = snapDuration;
        _snapCurve        = snapCurve;
        targetWorldPosition = targetPos;
        pieceIndex        = index;
        _scatteredPosition = scatteredPos;
    }

    // Called every frame by KintsugiGameController while this piece is being dragged.
    public void UpdateDragPosition(Vector3 cursorWorld)
    {
        Vector3 newPos = cursorWorld + dragOffset;
        newPos.z = -0.5f; // drag layer — in front of everything
        transform.position = newPos;
    }

    // Called on mouse-up. Returns true if a snap was triggered.
    public bool TrySnap()
    {
        if (isSnapped) return false;

        float dist = Vector3.Distance(transform.position, targetWorldPosition);
        if (dist <= _snapThreshold)
        {
            StartCoroutine(SnapToTarget());
            return true;
        }
        return false;
    }

    private IEnumerator SnapToTarget()
    {
        _gc.clickBlocked = true;
        isDragging = false;

        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < _snapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _snapDuration);
            float curved = _snapCurve.Evaluate(t);
            transform.position = Vector3.Lerp(startPos, targetWorldPosition, curved);
            yield return null;
        }

        transform.position = targetWorldPosition;
        isSnapped = true;

        ActivateAdjacentSeams();

        _gc.OnPieceSnapped(this);
        _gc.clickBlocked = false;
    }

    // Activate guide lines on seams shared with already-snapped neighbours.
    private void ActivateAdjacentSeams()
    {
        foreach (var kvp in adjacentSeams)
        {
            KintsugiPiece neighbor = _gc.GetPiece(kvp.Key);
            if (neighbor != null && neighbor.isSnapped)
            {
                kvp.Value.isActive = true;
                kvp.Value.guideLR.enabled = true;
            }
        }
    }

    // Called by a neighbour when it snaps after we do.
    public void TryActivateSeamWith(int neighborIdx)
    {
        if (!isSnapped) return;
        if (adjacentSeams.TryGetValue(neighborIdx, out SeamTraceState state) && state != null)
        {
            state.isActive = true;
            state.guideLR.enabled = true;
        }
    }
}
