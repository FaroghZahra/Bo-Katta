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

    // Update is called once per frame
    void Update()
    {
        if(Input.GetMouseButtonDown(0) && clickBlocked == false)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100))
            {
                if (hit.transform.GetComponent<ClickDetector>().paperFold.rotated == false)
                {
                    hit.transform.GetComponent<ClickDetector>().paperFold.RotatePaper(false);
                }
                else
                {
                    if (hit.transform.GetComponent<ClickDetector>().paperFold.orderIndex < currentOrderIndex - 1)
                    {
                        StartCoroutine(RotateBackToIndex(hit.transform.GetComponent<ClickDetector>().paperFold.orderIndex));
                    }
                    else
                    {
                        hit.transform.GetComponent<ClickDetector>().paperFold.RotatePaper(true);
                    }
                }
            }
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
            if (isEmbedded)
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
