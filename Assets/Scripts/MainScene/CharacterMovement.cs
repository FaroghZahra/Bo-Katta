using UnityEngine;

[System.Serializable]
public class PathPoint
{
    public Transform point;
    [Tooltip("Character stops here and fires BreakpointReached (used to start collectible animations).")]
    public bool stopHere;
    [Tooltip("Swap to the next sprite in the sprites[] array and continue moving.")]
    public bool rotateHere;
    [Tooltip("Stop here and open the minigame instead of waiting for collectibles.")]
    public bool playMinigameHere;
    [Tooltip("Which level minigame to open (only used when playMinigameHere is true).")]
    public int playMinigameLevel = 3;
}

[System.Serializable]
public class WalkCycle
{
    [Tooltip("Walk frames for the matching entry in sprites[] (same facing), played in order while moving.")]
    public Sprite[] frames;
}

public class CharacterMovement : MonoBehaviour
{
    public float moveSpeed = 3f;

    [Tooltip("When true, the character waits at the start until ResumeMoving() is called (StoryTeller sets this while the intro plays).")]
    public bool waitForStoryIntro = false;

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

    [Header("Walk Cycle")]
    [Tooltip("walkCycles[i] holds the walk frames for sprites[i]. Leave empty to only use the procedural bob.")]
    public WalkCycle[] walkCycles;
    [Tooltip("Footsteps per world unit travelled (each footstep = half the frame cycle).")]
    public float stepsPerUnit = 2.2f;
    [Tooltip("How high the body bobs on each step (world units).")]
    public float bobHeight = 0.02f;
    [Tooltip("Side-to-side tilt on each step (degrees).")]
    public float swayDegrees = 0f;
    [Tooltip("Squash/stretch amount on each step (fraction of scale).")]
    public float squashAmount = 0f;

    private int _spriteIndex = 0;
    private int _nextIndex = 0;
    private bool _isMoving = false;

    private float   _stepPhase;
    private Vector2 _walkOffset;
    private Vector3 _baseScale;
    private Quaternion _baseRotation;

    private void Awake()
    {
        _baseScale    = transform.localScale;
        _baseRotation = transform.localRotation;
    }

    private void Start()
    {
        if (path == null || path.Length == 0)
        {
            Debug.LogWarning("CharacterMovement: 'Path' array is empty. Assign waypoints in the Inspector.", this);
            return;
        }
        if (waitForStoryIntro) return;
        AdvanceToWaypoint();
    }

    private void Update()
    {
        if (!_isMoving || path == null || _nextIndex >= path.Length)
        {
            ClearWalkPose();
            return;
        }

        PathPoint current = path[_nextIndex];
        if (current.point == null)
        {
            Debug.LogWarning($"CharacterMovement: waypoint at index {_nextIndex} has no Transform assigned.", this);
            return;
        }

        // Path-follow on the "ground" position; the walk-cycle offset is purely visual.
        Vector2 currentXY = (Vector2)transform.position - _walkOffset;
        Vector2 targetXY  = current.point.position;

        Vector2 newXY = Vector2.MoveTowards(currentXY, targetXY, moveSpeed * Time.deltaTime);
        ApplyWalkPose(Vector2.Distance(currentXY, newXY));

        // Write back XY only — Z is never touched
        transform.position = new Vector3(newXY.x + _walkOffset.x, newXY.y + _walkOffset.y, transform.position.z);

        if (Vector2.Distance(newXY, targetXY) <= reachDistance)
        {
            if (current.playMinigameHere)
            {
                _nextIndex++;
                StopMoving();
                MinigameMenuController.Instance?.OpenMenu(current.playMinigameLevel);
                return;
            }

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

    // One footstep = half a sine period: the body rises and falls once, tilts toward the stepping
    // foot and stretches slightly at the top of the step / squashes on landing.
    private void ApplyWalkPose(float distanceMoved)
    {
        _stepPhase += distanceMoved * stepsPerUnit * Mathf.PI;
        float lift = Mathf.Abs(Mathf.Sin(_stepPhase));
        _walkOffset = new Vector2(0f, lift * bobHeight);
        transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(_stepPhase) * swayDegrees);
        float stretch = (lift - 0.5f) * 2f * squashAmount;
        transform.localScale = new Vector3(_baseScale.x * (1f - stretch * 0.5f), _baseScale.y * (1f + stretch), _baseScale.z);

        // Flipbook: one full frame cycle = two footsteps = one 2π of _stepPhase.
        Sprite[] frames = CurrentWalkFrames();
        if (frames != null && characterSprite != null)
        {
            float cycle = Mathf.Repeat(_stepPhase / (2f * Mathf.PI), 1f);
            characterSprite.sprite = frames[Mathf.Min((int)(cycle * frames.Length), frames.Length - 1)];
        }
    }

    private Sprite[] CurrentWalkFrames()
    {
        if (walkCycles == null || _spriteIndex >= walkCycles.Length) return null;
        var f = walkCycles[_spriteIndex].frames;
        return f != null && f.Length > 0 ? f : null;
    }

    private void ClearWalkPose()
    {
        if (_stepPhase == 0f && _walkOffset == Vector2.zero) return;
        transform.position -= (Vector3)_walkOffset;
        _walkOffset = Vector2.zero;
        _stepPhase = 0f;
        transform.localRotation = _baseRotation;
        transform.localScale = _baseScale;
        // Back to the idle pose for the current facing.
        if (characterSprite != null && sprites != null && _spriteIndex < sprites.Length)
            characterSprite.sprite = sprites[_spriteIndex];
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
        ClearWalkPose();
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
