using UnityEngine;

[System.Serializable]
public class PathPoint
{
    public Transform point;
    [Tooltip("Character stops here and fires BreakpointReached (used to start collectible animations).")]
    public bool stopHere;
    [Tooltip("Swap to the next sprite in the sprites[] array and continue moving.")]
    public bool rotateHere;
}

public class CharacterMovement : MonoBehaviour
{
    public float moveSpeed = 3f;

    [Header("Path")]
    [Tooltip("Ordered list of waypoints the character walks through. Mark stopHere on points where it should wait.")]
    public PathPoint[] path;

    [Tooltip("How close (world units) the character must be to a waypoint to count as reached.")]
    public float reachDistance = 0.3f;

    [Header("Visual")]
    [Tooltip("The SpriteRenderer on the character (or its child).")]
    public SpriteRenderer characterSprite;

    [Tooltip("sprites[0] = initial facing. sprites[1] = after first rotateHere waypoint. Add more as needed.")]
    public Sprite[] sprites;

    private int _spriteIndex = 0;
    private int _nextIndex = 0;
    private bool _isMoving = false;

    private void Start()
    {
        if (path == null || path.Length == 0)
        {
            Debug.LogWarning("CharacterMovement: 'Path' array is empty. Assign waypoints in the Inspector.", this);
            return;
        }
        AdvanceToWaypoint();
    }

    private void Update()
    {
        if (!_isMoving || path == null || _nextIndex >= path.Length) return;

        PathPoint current = path[_nextIndex];
        if (current.point == null)
        {
            Debug.LogWarning($"CharacterMovement: waypoint at index {_nextIndex} has no Transform assigned.", this);
            return;
        }

        Vector2 currentXY = transform.position;
        Vector2 targetXY  = current.point.position;

        Vector2 newXY = Vector2.MoveTowards(currentXY, targetXY, moveSpeed * Time.deltaTime);

        // Write back XY only — Z is never touched
        transform.position = new Vector3(newXY.x, newXY.y, transform.position.z);

        if (Vector2.Distance(newXY, targetXY) <= reachDistance)
        {
            bool stop   = current.stopHere;
            bool rotate = current.rotateHere;
            Debug.Log($"[CharacterMovement] Reached path[{_nextIndex}] stopHere={stop} point={current.point?.name}");
            _nextIndex++;

            if (rotate) SwapSprite();

            if (stop)
            {
                StopMoving();
                GameEvents.BreakpointReached();
            }
            else
            {
                AdvanceToWaypoint();
            }
        }
    }

    private void SwapSprite()
    {
        if (characterSprite == null || sprites == null || sprites.Length == 0) return;
        _spriteIndex = (_spriteIndex + 1) % sprites.Length;
        characterSprite.sprite = sprites[_spriteIndex];
    }

    private void AdvanceToWaypoint()
    {
        if (path == null || _nextIndex >= path.Length)
        {
            Debug.Log($"[CharacterMovement] AdvanceToWaypoint: _nextIndex={_nextIndex} >= path.Length={path?.Length} → StopMoving");
            StopMoving();
            return;
        }

        Transform target = path[_nextIndex].point;
        if (target == null)
        {
            Debug.LogWarning($"[CharacterMovement] AdvanceToWaypoint: path[{_nextIndex}].point is NULL → frozen", this);
            StopMoving();
            return;
        }

        Debug.Log($"[CharacterMovement] Advancing → path[{_nextIndex}] '{target.name}'");
        _isMoving = true;
    }

    /// <summary>Resumes walking after a stop point (called by LevelProgressionController after a minigame win).</summary>
    public void ResumeMoving() => AdvanceToWaypoint();

    public void StopMoving()
    {
        _isMoving = false;
    }

    // Draws the path in the Scene view so you can see it without pressing Play
    private void OnDrawGizmos()
    {
        if (path == null || path.Length == 0) return;

        for (int i = 0; i < path.Length; i++)
        {
            if (path[i].point == null) continue;

            if (path[i].stopHere)
                Gizmos.color = Color.red;
            else if (path[i].rotateHere)
                Gizmos.color = Color.magenta;
            else
                Gizmos.color = Color.cyan;

            Gizmos.DrawSphere(path[i].point.position, 0.15f);

            if (i > 0 && path[i - 1].point != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(path[i - 1].point.position, path[i].point.position);
            }
        }
    }
}
