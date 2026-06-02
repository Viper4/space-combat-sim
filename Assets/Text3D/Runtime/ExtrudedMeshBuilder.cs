using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public static class ExtrudedMeshBuilder
{
    public static Mesh Build(
        TMP_Text tmpText,
        float depth,
        FontOutlineExtractor extractor
    )
    {
        tmpText.ForceMeshUpdate(true, true);

        TMP_TextInfo textInfo = tmpText.textInfo;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        List<int> frontBackTriangles = new List<int>();
        List<int> sideTriangles = new List<int>();

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = textInfo.characterInfo[i];

            if (!character.isVisible)
            {
                continue;
            }
            Debug.Log(
                $"Char: {character.character}"
            );

            TMP_MeshInfo meshInfo =
                textInfo.meshInfo[character.materialReferenceIndex];

            GlyphShape glyph = extractor.GetGlyphShape(character.character);


            if (glyph == null || glyph.contours.Count == 0)
            {
                Debug.Log("GlyphShape is NULL");
                continue;
            }
            Debug.Log($"Contour count: {glyph.contours.Count}");

            Vector3 tmpBL =
                meshInfo.vertices[character.vertexIndex + 0];

            Vector3 tmpTR =
                meshInfo.vertices[character.vertexIndex + 2];

            GlyphTransform transform = new GlyphTransform();

            transform.scale = new Vector2(
                (tmpTR.x - tmpBL.x) / (glyph.xMax - glyph.xMin),
                (tmpTR.y - tmpBL.y) / (glyph.yMax - glyph.yMin)
            );

            transform.offset = new Vector2(
                tmpBL.x - glyph.xMin * transform.scale.x,
                tmpBL.y - glyph.yMin * transform.scale.y
            );

            GlyphShape localShape = glyph.TransformTo(transform);

            foreach (var contour in glyph.contours)
            {
                Debug.Log($"Contour points: {contour.points.Count}");
            }

            Vector2[] triVerts;

            int[] triIndices =
                PolygonTriangulator.Triangulate(
                    localShape,
                    out triVerts
                );
            Debug.Log($"Tri vertices: {triVerts.Length}");
            Debug.Log($"Tri indices: {triIndices.Length}");

            AddFace(
                triVerts,
                triIndices,
                vertices,
                normals,
                uvs,
                frontBackTriangles,
                0f,
                false
            );

            AddFace(
                triVerts,
                triIndices,
                vertices,
                normals,
                uvs,
                frontBackTriangles,
                -depth,
                true
            );

            foreach (GlyphContour contour in localShape.contours)
            {
                AddSideStrip(
                    contour,
                    depth,
                    vertices,
                    normals,
                    uvs,
                    sideTriangles
                );
            }
        }

        Debug.Log($"FINAL verts: {vertices.Count}");
        Debug.Log($"FINAL tris: {frontBackTriangles.Count + sideTriangles.Count}");

        Mesh mesh = new Mesh
        {
            indexFormat = IndexFormat.UInt32
        };

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);

        mesh.subMeshCount = 2;

        mesh.SetTriangles(frontBackTriangles, 0);
        mesh.SetTriangles(sideTriangles, 1);

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        return mesh;
    }

    private static void AddFace(
        Vector2[] faceVertices,
        int[] faceTriangles,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<int> triangles,
        float z,
        bool flip
    )
    {
        int baseIndex = vertices.Count;

        for (int i = 0; i < faceVertices.Length; i++)
        {
            vertices.Add(new Vector3(
                faceVertices[i].x,
                faceVertices[i].y,
                z
            ));

            normals.Add(flip ? Vector3.back : Vector3.forward);

            uvs.Add(faceVertices[i]);
        }

        for (int i = 0; i < faceTriangles.Length; i += 3)
        {
            if (flip)
            {
                triangles.Add(baseIndex + faceTriangles[i]);
                triangles.Add(baseIndex + faceTriangles[i + 2]);
                triangles.Add(baseIndex + faceTriangles[i + 1]);
            }
            else
            {
                triangles.Add(baseIndex + faceTriangles[i]);
                triangles.Add(baseIndex + faceTriangles[i + 1]);
                triangles.Add(baseIndex + faceTriangles[i + 2]);
            }
        }
    }

    private static void AddSideStrip(
        GlyphContour contour,
        float depth,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<int> triangles
    )
    {
        List<Vector2> points = contour.points;

        for (int i = 0; i < points.Count; i++)
        {
            int next = (i + 1) % points.Count;

            Vector2 p0 = points[i];
            Vector2 p1 = points[next];

            Vector3 a = new Vector3(p0.x, p0.y, 0f);
            Vector3 b = new Vector3(p1.x, p1.y, 0f);
            Vector3 c = b + Vector3.back * depth;
            Vector3 d = a + Vector3.back * depth;

            Vector2 edge = (p1 - p0).normalized;

            Vector3 normal = contour.isHole
                ? new Vector3(-edge.y, edge.x, 0f)
                : new Vector3(edge.y, -edge.x, 0f);

            int start = vertices.Count;

            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            triangles.Add(start + 0);
            triangles.Add(start + 1);
            triangles.Add(start + 2);

            triangles.Add(start + 0);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }
}