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

    // neighborPieceIndex → LineRenderer on the shared seam
    public Dictionary<int, LineRenderer> adjacentSeams = new Dictionary<int, LineRenderer>();

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

        RevealAdjacentSeams();

        _gc.OnPieceSnapped(this);
        _gc.clickBlocked = false;
    }

    // Show gold seams shared with already-snapped neighbours.
    private void RevealAdjacentSeams()
    {
        foreach (var kvp in adjacentSeams)
        {
            int neighborIdx = kvp.Key;
            LineRenderer lr  = kvp.Value;
            if (lr == null) continue;

            KintsugiPiece neighbor = _gc.GetPiece(neighborIdx);
            if (neighbor != null && neighbor.isSnapped)
                lr.enabled = true;
        }
    }

    // Called by a neighbour's RevealAdjacentSeams when it snaps after we do.
    public void TryRevealSeamWith(int neighborIdx)
    {
        if (!isSnapped) return;
        if (adjacentSeams.TryGetValue(neighborIdx, out LineRenderer lr) && lr != null)
            lr.enabled = true;
    }
}
