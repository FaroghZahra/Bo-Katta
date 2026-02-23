using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure ear-clipping triangulation. No Unity lifecycle — all static methods.
/// Returns indices in Unity's left-handed CW winding order.
/// </summary>
public static class Triangulator
{
    /// <summary>
    /// Triangulates a simple (non-self-intersecting) polygon.
    /// Input vertices should be in CCW order; the method corrects CW automatically.
    /// Returns flat triangle index list: [t0v0, t0v1, t0v2, t1v0, ...]
    /// </summary>
    public static int[] Triangulate(List<Vector2> vertices)
    {
        List<Vector2> verts = new List<Vector2>(vertices);

        // Ensure CCW winding via signed area (shoelace)
        if (SignedArea(verts) < 0f)
            verts.Reverse();

        // Working index list (indices into original verts list)
        List<int> indices = new List<int>(verts.Count);
        for (int i = 0; i < verts.Count; i++)
            indices.Add(i);

        List<int> result = new List<int>();

        int maxIter = verts.Count * verts.Count + 10; // safety cap
        int iter = 0;

        while (indices.Count > 3 && iter++ < maxIter)
        {
            bool earFound = false;

            for (int i = 0; i < indices.Count; i++)
            {
                int prev = (i - 1 + indices.Count) % indices.Count;
                int next = (i + 1) % indices.Count;

                int iPrev = indices[prev];
                int iCurr = indices[i];
                int iNext = indices[next];

                Vector2 a = verts[iPrev];
                Vector2 b = verts[iCurr];
                Vector2 c = verts[iNext];

                // Must be convex (cross product > 0 for CCW)
                if (Cross(a, b, c) <= 0f) continue;

                // No other vertex inside this ear triangle
                bool hasVertexInside = false;
                for (int j = 0; j < indices.Count; j++)
                {
                    if (j == prev || j == i || j == next) continue;
                    if (PointInTriangle(verts[indices[j]], a, b, c))
                    {
                        hasVertexInside = true;
                        break;
                    }
                }

                if (hasVertexInside) continue;

                // Emit ear in CW winding (Unity left-handed): reverse triple
                result.Add(iNext);
                result.Add(iCurr);
                result.Add(iPrev);

                indices.RemoveAt(i);
                earFound = true;
                break;
            }

            // Degenerate polygon — cannot clip any ear
            if (!earFound) break;
        }

        // Append final triangle (also reversed for CW)
        if (indices.Count == 3)
        {
            result.Add(indices[2]);
            result.Add(indices[1]);
            result.Add(indices[0]);
        }

        return result.ToArray();
    }

    // --- Helpers ---

    static float SignedArea(List<Vector2> v)
    {
        float area = 0f;
        int n = v.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = v[i];
            Vector2 b = v[(i + 1) % n];
            area += (a.x * b.y) - (b.x * a.y);
        }
        return area * 0.5f;
    }

    // 2D cross product of vectors AB and AC — positive = CCW turn at B
    static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(a, b, p);
        float d2 = Cross(b, c, p);
        float d3 = Cross(c, a, p);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }
}
