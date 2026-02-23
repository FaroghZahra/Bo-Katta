using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public int currentOrderIndex = 0;
    public List<PaperFold> paperFolds;
    public AnimationCurve rotationCurve;
    public bool clickBlocked = false;

    public int maxRotations;
    public string nextSceneName;
    public bool isEmbedded = false;  // Set to true by MinigameMenuController after additive load
    [HideInInspector] public System.Action onComplete;

    [Header("Swipe Settings")]
    [Tooltip("Minimum pixels a finger must travel to count as a swipe")]
    public float swipeThreshold = 50f;
    [Tooltip("How closely the swipe must align with a piece's swipe direction (0=any, 1=exact). 0.5 = within 60 degrees.")]
    public float swipeDirectionDotThreshold = 0.5f;

    private Vector2 _touchStartPos;
    private bool _isTouching;
    private PaperFold[] _allFolds;

    void Start()
    {
        _allFolds = FindObjectsOfType<PaperFold>();
    }

    void Update()
    {
        if (clickBlocked) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseSwipe();
#else
        HandleTouchSwipe();
#endif
    }

    // --- Input handlers ---

    void HandleMouseSwipe()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _touchStartPos = Input.mousePosition;
            _isTouching = true;
        }
        else if (_isTouching && Input.GetMouseButton(0))
        {
            Vector2 delta = (Vector2)Input.mousePosition - _touchStartPos;
            if (delta.magnitude >= swipeThreshold)
            {
                _isTouching = false;
                ProcessSwipe(delta);
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _isTouching = false;
        }
    }

    void HandleTouchSwipe()
    {
        if (Input.touchCount != 1)
        {
            _isTouching = false;
            return;
        }

        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                _touchStartPos = touch.position;
                _isTouching = true;
                break;

            case TouchPhase.Moved:
                if (_isTouching)
                {
                    Vector2 delta = touch.position - _touchStartPos;
                    if (delta.magnitude >= swipeThreshold)
                    {
                        _isTouching = false;
                        ProcessSwipe(delta);
                    }
                }
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                _isTouching = false;
                break;
        }
    }

    // --- Core logic ---

    // Matches the swipe direction against every piece's declared swipeDirection.
    // Swiping WITH a piece's direction folds it; swiping AGAINST it unfolds it.
    // Folding takes priority over unfolding when both would match.
    void ProcessSwipe(Vector2 swipeDelta)
    {
        if (swipeDelta.magnitude < swipeThreshold) return;

        Vector2 swipeDir = swipeDelta.normalized;

        PaperFold foldTarget   = null;
        PaperFold unfoldTarget = null;
        float bestFoldScore   = swipeDirectionDotThreshold;
        float bestUnfoldScore = swipeDirectionDotThreshold;

        foreach (PaperFold pf in _allFolds)
        {
            Vector2 pieceDir = pf.swipeDirection.normalized;
            float dot = Vector2.Dot(swipeDir, pieceDir);

            // Swipe aligns with piece's fold direction → candidate to fold
            if (!pf.rotated && dot > bestFoldScore)
            {
                bestFoldScore = dot;
                foldTarget = pf;
            }

            // Swipe opposes piece's fold direction → candidate to unfold
            if (pf.rotated && dot < -bestUnfoldScore)
            {
                bestUnfoldScore = -dot;
                unfoldTarget = pf;
            }
        }

        if (foldTarget != null)
        {
            foldTarget.RotatePaper(false);
        }
        else if (unfoldTarget != null)
        {
            if (unfoldTarget.orderIndex < currentOrderIndex - 1)
                StartCoroutine(RotateBackToIndex(unfoldTarget.orderIndex));
            else
                unfoldTarget.RotatePaper(true);
        }
    }

    IEnumerator RotateBackToIndex(int index)
    {
        while (currentOrderIndex != index)
        {
            int prevIndex = currentOrderIndex;
            paperFolds[currentOrderIndex - 1].RotatePaper(true);
            yield return new WaitUntil(() => currentOrderIndex < prevIndex);
        }
    }

    public void CheckIfRotatedCorrectly()
    {
        bool everythingCorrect = true;
        foreach (PaperFold pf in paperFolds)
        {
            if (pf.rightOrder == false)
            {
                everythingCorrect = false;
            }
        }

        if (everythingCorrect)
        {
            if (onComplete != null)
                onComplete.Invoke();
            else if (isEmbedded)
                GameEvents.MinigameWon();
            else
                SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            StartCoroutine(RotateBackToIndex(0));
            Debug.Log("fail");
        }
    }
}
