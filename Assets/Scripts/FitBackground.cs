using UnityEngine;

public class FitBackground : MonoBehaviour
{
    void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        float worldHeight = Camera.main.orthographicSize * 2f;
        float worldWidth = worldHeight * Camera.main.aspect;

        float spriteHeight = sr.sprite.bounds.size.y;
        float spriteWidth = sr.sprite.bounds.size.x;

        transform.localScale = new Vector3(
            worldWidth / spriteWidth,
            worldHeight / spriteHeight,
            1f
        );

        transform.position = new Vector3(0, 0, 0);
    }
}