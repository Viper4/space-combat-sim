using System.Collections.Generic;
using UnityEngine;

public static class BezierFlattener
{
    public static void FlattenQuadratic(
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        List<Vector2> output,
        int resolution
    )
    {
        for (int i = 1; i <= resolution; i++)
        {
            float t = i / (float)resolution;

            float omt = 1f - t;

            Vector2 point =
                omt * omt * p0 +
                2f * omt * t * p1 +
                t * t * p2;

            output.Add(point);
        }
    }
}