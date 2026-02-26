using System;
using System.Collections;
using UnityEngine;

[System.Serializable]
public class CatPathPoint
{
    public Transform point;
    public bool rotateHere;
}

public class CatSequenceController : MonoBehaviour
{
    [Header("Sprites")]
    public SpriteRenderer catRenderer;
    public Sprite catHealedSittingSprite;
    public Sprite[] walkSprites;          // [0] = walk back, [1] = walk front (etc.)
    public Sprite catArrivedSprite;

    [Header("Path")]
    public CatPathPoint[] path;
    public float moveSpeed = 2f;
    public float reachDistance = 0.3f;
    public float rotatePauseDuration = 0.5f;  // seconds to pause at rotateHere waypoints

    [Header("Sitting Pause")]
    public float sittingPauseDuration = 0f;  // optional delay before walking starts

    [Header("Destination Effects")]
    public GameObject afterKintsugiObject;

    private int _spriteIndex;

    public void PlaySequence(Action onComplete) =>
        StartCoroutine(RunSequence(onComplete));

    private IEnumerator RunSequence(Action onComplete)
    {
        // State 2: healed sitting
        if (catRenderer != null && catHealedSittingSprite != null)
            catRenderer.sprite = catHealedSittingSprite;

        if (sittingPauseDuration > 0f)
            yield return new WaitForSeconds(sittingPauseDuration);

        // State 3: walk along path
        _spriteIndex = 0;
        if (catRenderer != null && walkSprites != null && walkSprites.Length > 0)
            catRenderer.sprite = walkSprites[0];

        for (int i = 0; i < path.Length; i++)
        {
            var wp = path[i];

            if (wp.point != null)
            {
                while (Vector2.Distance(transform.position, wp.point.position) > reachDistance)
                {
                    Vector2 dir = (Vector2)wp.point.position - (Vector2)transform.position;
                    if (catRenderer != null && Mathf.Abs(dir.x) > 0.01f)
                        catRenderer.flipX = dir.x < 0f;

                    transform.position = Vector2.MoveTowards(
                        transform.position,
                        wp.point.position,
                        moveSpeed * Time.deltaTime);
                    yield return null;
                }
            }

            if (wp.rotateHere)
            {
                _spriteIndex = Mathf.Min(_spriteIndex + 1, walkSprites.Length - 1);
                if (catRenderer != null && walkSprites != null && _spriteIndex < walkSprites.Length)
                    catRenderer.sprite = walkSprites[_spriteIndex];
                Debug.Log($"[Cat] Rotated to sprite index {_spriteIndex} at waypoint {i}");
                if (rotatePauseDuration > 0f)
                    yield return new WaitForSeconds(rotatePauseDuration);
            }
        }

        // State 4: arrived
        if (catRenderer != null && catArrivedSprite != null)
            catRenderer.sprite = catArrivedSprite;
        if (afterKintsugiObject != null)
            afterKintsugiObject.SetActive(true);

        // Resume player
        onComplete?.Invoke();
    }
}
