using UnityEngine;

public static class SpriteRendererSizeUtility
{
    public static void SetWorldSize(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Vector2 spriteWorldSize = renderer.sprite.bounds.size;

        float maxSide = Mathf.Max(spriteWorldSize.x, spriteWorldSize.y);

        if (maxSide <= 0f)
            return;

        float scale = targetSize / maxSide;

        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }
}