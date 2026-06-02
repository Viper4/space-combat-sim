using LibTessDotNet;
using UnityEngine;

public static class PolygonTriangulator
{
    public static int[] Triangulate(
        GlyphShape shape,
        out Vector2[] outVertices
    )
    {
        Tess tess = new Tess();

        foreach (GlyphContour contour in shape.contours)
        {
            ContourVertex[] verts = new ContourVertex[contour.points.Count];

            for (int i = 0; i < contour.points.Count; i++)
            {
                Vector2 point = contour.points[i];

                verts[i].Position = new Vec3(
                    point.x,
                    point.y,
                    0
                );
            }

            tess.AddContour(
                verts,
                contour.isHole
                    ? ContourOrientation.Clockwise
                    : ContourOrientation.CounterClockwise
            );
        }

        tess.Tessellate(
            WindingRule.EvenOdd,
            ElementType.Polygons,
            3
        );

        outVertices = new Vector2[tess.VertexCount];

        for (int i = 0; i < tess.VertexCount; i++)
        {
            outVertices[i] = new Vector2(
                tess.Vertices[i].Position.X,
                tess.Vertices[i].Position.Y
            );
        }

        return tess.Elements;
    }
}