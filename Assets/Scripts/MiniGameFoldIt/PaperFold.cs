using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaperFold : MonoBehaviour
{
    public int orderIndex = -1;

    public List<GameObject> corners;
    public GameController gameController;

    public Vector3 rotAxis;
    public float rotDuration;
    public float angleChange;

    private Vector3 _restPosition;

    void Awake()
    {
        _restPosition = transform.position;
    }

    public bool rotated = false;
    public GameObject mainLine;
    public List<LineRenderer> lines;
    public List<int> extraLineIndices;
    public List<float> extraLinesStartPos;
    public List<float> extraLinesEndPos;

    public List<int> rotationOrderIndices;
    public bool rightOrder = false;

    [Header("Swipe Input")]
    [Tooltip("Screen-space direction the player swipes to fold this piece. E.g. (1,0) = right, (0,1) = up, (-1,0) = left, (0,-1) = down. Swiping the opposite direction unfolds.")]
    public Vector2 swipeDirection;

    IEnumerator FoldAroundAxis(Transform paperTransform, Vector3 axis, float angle, Vector3 endPosition, float duration, bool back)
    {
        gameController.clickBlocked = true;

        Quaternion startRot = paperTransform.rotation;
        Vector3 startPos = paperTransform.position;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float rotAngle = gameController.rotationCurve.Evaluate(t / duration) * angle;
            paperTransform.rotation = Quaternion.AngleAxis(rotAngle, axis) * startRot;
            paperTransform.position = Vector3.Lerp(startPos, endPosition, t / duration);
            yield return null;
        }
        paperTransform.rotation = Quaternion.AngleAxis(angle, axis) * startRot;
        paperTransform.position = endPosition;

        if (back)
        {
            gameController.currentOrderIndex--;
            mainLine.SetActive(true);
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i].SetPosition(extraLineIndices[i], new Vector3(0, 0, extraLinesStartPos[i]));
            }
        }
        else
        {
            gameController.currentOrderIndex++;
            gameController.PlayFoldSound();
            if(gameController.maxRotations == gameController.currentOrderIndex)
            {
                gameController.CheckIfRotatedCorrectly();
            }
        }
        gameController.clickBlocked = false;
    }

    public void RotatePaper(bool back)
    {
        foreach (GameObject corner in corners)
        {
            corner.transform.parent = gameObject.transform;
        }

        if (back == false)
        {
            orderIndex = gameController.currentOrderIndex;
            gameController.paperFolds.Add(this);
            Vector3 depthDir = Camera.main != null ? -Camera.main.transform.forward : Vector3.up;
            StartCoroutine(FoldAroundAxis(gameObject.transform, rotAxis, angleChange,
                transform.position + depthDir * (orderIndex * 0.001f + 0.001f), rotDuration, back));
            rotated = true;

            mainLine.SetActive(false);
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i].SetPosition(extraLineIndices[i], new Vector3(0, 0, extraLinesEndPos[i]));
            }

            foreach(int orderLineIndex in rotationOrderIndices)
            {
                if (orderLineIndex == gameController.currentOrderIndex)
                {
                    rightOrder = true;
                }

            }
        }
        else
        {
            orderIndex = -1;
            StartCoroutine(FoldAroundAxis(gameObject.transform, rotAxis, -angleChange,
                 _restPosition, rotDuration, back));
            gameController.paperFolds.Remove(this);
            rotated = false;
            rightOrder = false;
        }
    }
}
