using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterMovement : MonoBehaviour
{
    public float moveSpeed = 3f;

    [Tooltip("The Collectible the character walks toward. " +
             "The movement direction is calculated from the character's " +
             "start position to this target, matching the bridge slope.")]
    public Transform destination;   // Assign the Collectible in the Inspector

    [Tooltip("Rotates the movement direction in degrees. " +
             "Positive = counter-clockwise (left), Negative = clockwise (right).")]
    public float directionAngleOffset = 0f;

    private bool isMoving = false;
    private Rigidbody2D rb;
    private Vector2 moveDirection;  // Normalized direction along the bridge

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // Calculate the bridge's slope direction once at startup.
        // This way the character follows the exact path from its spawn
        // position to the collectible, matching the bridge angle visually.
        if (destination != null)
        {
            Vector2 toTarget = (Vector2)destination.position - (Vector2)transform.position;
            float angle = Mathf.Atan2(toTarget.y, toTarget.x) + directionAngleOffset * Mathf.Deg2Rad;
            moveDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        else
        {
            // Fallback: pure horizontal if no destination assigned
            moveDirection = Vector2.right;
            Debug.LogWarning("CharacterMovement: no destination assigned. " +
                             "Assign the Collectible Transform in the Inspector.");
        }
    }

    private void FixedUpdate()
    {
        if (isMoving)
            rb.velocity = moveDirection * moveSpeed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Collectible"))
        {
            StopMoving();
            CollectibleLevel cl = other.GetComponent<CollectibleLevel>();
            int level = cl != null ? cl.levelIndex : 1;
            MinigameMenuController.Instance?.OpenMenu(level);
        }
    }

    /// <summary>Wire this to the Forward button's OnClick in the Inspector.</summary>
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
