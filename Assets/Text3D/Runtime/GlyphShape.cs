using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GlyphShape
{
    public List<GlyphContour> contours = new List<GlyphContour>();

    public float xMin;
    public float yMin;
    public float xMax;
    public float yMax;

    public GlyphShape TransformTo(GlyphTransform transform)
    {
        GlyphShape result = new GlyphShape
        {
            xMin = xMin,
            yMin = yMin,
            xMax = xMax,
            yMax = yMax
        };

        foreach (GlyphContour contour in contours)
        {
            GlyphContour newContour = new GlyphContour
            {
                isHole = contour.isHole
            };

            foreach (Vector2 point in contour.points)
            {
                newContour.points.Add(transform.TransformPoint(point));
            }

            result.contours.Add(newContour);
        }

        return result;
    }
}