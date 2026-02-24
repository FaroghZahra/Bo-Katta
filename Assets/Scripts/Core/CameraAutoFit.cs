using UnityEngine;

[ExecuteAlways]
public class CameraAutoFit : MonoBehaviour
{
    public SpriteRenderer background;

    void Start()
    {
        Fit();
    }

    void Fit()
    {
        float screenRatio = (float)Screen.width / Screen.height;
        float targetRatio = background.bounds.size.x / background.bounds.size.y;

        Camera cam = GetComponent<Camera>();

        if (screenRatio >= targetRatio)
        {
            cam.orthographicSize = background.bounds.size.y / 2f;
        }
        else
        {
            float difference = targetRatio / screenRatio;
            cam.orthographicSize = background.bounds.size.y / 2f * difference;
        }
    }
}