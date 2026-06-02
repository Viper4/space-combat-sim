using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GlyphContour
{
    public List<Vector2> points = new List<Vector2>();
    public bool isHole;

    public GlyphContour()
    {
    }

    public GlyphContour(List<Vector2> points, bool isHole)
    {
        this.points = points;
        this.isHole = isHole;
    }
}