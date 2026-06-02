using UnityEngine;

public struct GlyphTransform
{
    public Vector2 offset;
    public Vector2 scale;

    public Vector2 TransformPoint(Vector2 point)
    {
        return new Vector2(
            offset.x + point.x * scale.x,
            offset.y + point.y * scale.y
        );
    }
}