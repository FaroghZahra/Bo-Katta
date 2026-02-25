using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterMovement : MonoBehaviour
{
    public float moveSpeed = 3f;

    [Tooltip("The Collectible the character walks toward. " +
             "The movement direction is calculated from the character's " +
             "start position to this target, matching the bridge slope.")]
    public Transform destination;

    [Tooltip("Rotates the movement direction in degrees. " +
             "Positive = counter-clockwise (left), Negative = clockwise (right).")]
    public float directionAngleOffset = 0f;

    [Tooltip("Flip the movement direction 180°. Enable if the character walks backward.")]
    public bool reverseDirection = false;

    [Header("Break Point")]
    [Tooltip("Assign the BreakPoint Transform in the Inspector. No collider needed.")]
    public Transform breakPoint;

    [Tooltip("How close (world units) the character must be to the break point to stop.")]
    public float breakPointStopDistance = 0.3f;

    private bool isMoving = false;
    private bool _breakPointTriggered = false;
    private Rigidbody2D rb;
    private Vector2 moveDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()  => GameEvents.OnMinigameWon += OnMinigameWonHandler;
    private void OnDisable() => GameEvents.OnMinigameWon -= OnMinigameWonHandler;

    private void Start()
    {
        if (destination != null)
        {
            Vector2 toTarget = (Vector2)destination.position - (Vector2)transform.position;
            float angle = Mathf.Atan2(toTarget.y, toTarget.x) + directionAngleOffset * Mathf.Deg2Rad;
            moveDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (reverseDirection) moveDirection = -moveDirection;
        }
        else
        {
            moveDirection = Vector2.right;
            Debug.LogWarning("CharacterMovement: no destination assigned.");
        }

        StartMoving();
    }

    private void Update()
    {
        if (isMoving && !_breakPointTriggered && breakPoint != null)
        {
            float dist = Vector2.Distance(transform.position, breakPoint.position);
            if (dist <= breakPointStopDistance)
            {
                _breakPointTriggered = true;
                StopMoving();
                GameEvents.BreakpointReached();
            }
        }
    }

    private void FixedUpdate()
    {
        if (isMoving)
            rb.velocity = moveDirection * moveSpeed;
    }

    private void OnMinigameWonHandler()
    {
        if (breakPoint != null)
            breakPoint.gameObject.SetActive(false);
    }

    /// <summary>Can still be wired to a button if manual control is ever needed.</summary>
    public void StartMoving()
    {
        if (isMoving) return;
        isMoving = true;
    }

    public void StopMoving()
    {
        isMoving = false;
        rb.velocity = Vector2.zero;
    }
}
