using UnityEngine;

public class FitBackground : MonoBehaviour
{
    [Range(1f, 3f)]
    public float zoomFactor = 1f;

    void Update()
    {
        transform.localScale = new Vector3(zoomFactor, zoomFactor, 1f);
    }
}