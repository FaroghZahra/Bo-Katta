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

    public bool rotated = false;
    public GameObject mainLine;
    public List<LineRenderer> lines;
    public List<int> extraLineIndices;
    public List<float> extraLinesStartPos;
    public List<float> extraLinesEndPos;

    public List<int> rotationOrderIndices;
    public bool rightOrder = false;

    IEnumerator FoldAroundAxis(Transform paperTransform, Vector3 axis, float angle,Vector3 endPosition, float duration, bool back)
    {
        gameController.clickBlocked = true;
        Quaternion startRot = paperTransform.rotation;
        Vector3 startPos = paperTransform.position;

        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float rotAngle = gameController.rotationCurve.Evaluate(t / duration) * angle;
            paperTransform.rotation = startRot * Quaternion.AngleAxis(rotAngle, axis);
            paperTransform.position = Vector3.Lerp(startPos, endPosition, t / duration);
            yield return null;
        }
        paperTransform.rotation = startRot * Quaternion.AngleAxis(angle, axis);
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
            StartCoroutine(FoldAroundAxis(gameObject.transform, rotAxis, angleChange,
                transform.position + new Vector3(0, orderIndex * 0.001f + 0.001f, 0), rotDuration, back));
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
                 new Vector3(gameObject.transform.position.x, 0.003f, gameObject.transform.position.z), rotDuration, back));
            gameController.paperFolds.Remove(this);
            rotated = false;
            rightOrder = false;
        }
    }
}
