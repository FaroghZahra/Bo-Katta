using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs at Start(). Procedurally generates irregular tear edges, per-piece meshes,
/// PolygonCollider2Ds, UV mapping, and gold seam LineRenderers.
/// </summary>
public class KintsugiPuzzleGenerator : MonoBehaviour
{
    [Header("Config & References")]
    public KintsugiPuzzleConfig    config;
    public KintsugiGameController  gameController;
    public Material                pieceMaterial;   // Sprites/Default with puzzle texture
    public Transform               puzzleCenter;    // Empty GO at world origin
    public Transform               goldSeamsParent; // Empty child GO named "GoldSeams"

    [Header("Background")]
    [Range(0f, 1f)]
    public float backgroundAlpha = 0.1f;          // Opacity of the ghost image

    // "minIdx_maxIdx" → normalized tear points (A's view)
    // "maxIdx_minIdx" → reversed (B's view)
    private Dictionary<string, List<Vector2>> _tearEdges = new Dictionary<string, List<Vector2>>();
    private List<KintsugiPiece> _pieces = new List<KintsugiPiece>();

    private const int KintsugiLayer = 8; // Physics Layer "KintsugiPieces"

    void Start()
    {
        if (config == null || config.pieceRects == null || config.pieceRects.Count == 0)
        {
            Debug.LogError("[KintsugiPuzzleGenerator] No config or pieceRects assigned.");
            return;
        }

        GenerateTearEdges();
        CreateBackground();
        CreatePieces();
        CreateGoldSeams();
        ScatterPieces();

        gameController.SetPieces(_pieces);
    }

    // =========================================================================
    // Tear Edge Generation
    // =========================================================================

    void GenerateTearEdges()
    {
        int n = config.pieceRects.Count;
        for (int a = 0; a < n; a++)
        {
            for (int b = a + 1; b < n; b++)
            {
                Rect rA = config.pieceRects[a];
                Rect rB = config.pieceRects[b];

                if (TryGetSharedEdge(rA, rB, out Vector2 start, out Vector2 end, out bool isHorizontal))
                {
                    int edgeHash = a * 1000 + b;
                    List<Vector2> profile = GenerateTearProfile(start, end, isHorizontal, edgeHash);

                    // Store forward (A→B) and reversed (B→A)
                    _tearEdges[$"{a}_{b}"] = profile;
                    List<Vector2> reversed = new List<Vector2>(profile);
                    reversed.Reverse();
                    _tearEdges[$"{b}_{a}"] = reversed;
                }
            }
        }
    }

    bool TryGetSharedEdge(Rect rA, Rect rB,
                          out Vector2 start, out Vector2 end, out bool isHorizontal)
    {
        start = end = Vector2.zero;
        isHorizontal = false;

        float eps = 0.001f;

        // Horizontal shared edge: rA bottom = rB top (or flipped)
        if (Mathf.Abs(rA.yMin - rB.yMax) < eps || Mathf.Abs(rB.yMin - rA.yMax) < eps)
        {
            float xOverlapMin = Mathf.Max(rA.xMin, rB.xMin);
            float xOverlapMax = Mathf.Min(rA.xMax, rB.xMax);
            if (xOverlapMax > xOverlapMin + eps)
            {
                float y = Mathf.Abs(rA.yMin - rB.yMax) < eps ? rA.yMin : rA.yMax;
                start = new Vector2(xOverlapMin, y);
                end   = new Vector2(xOverlapMax, y);
                isHorizontal = true;
                return true;
            }
        }

        // Vertical shared edge: rA right = rB left (or flipped)
        if (Mathf.Abs(rA.xMax - rB.xMin) < eps || Mathf.Abs(rB.xMax - rA.xMin) < eps)
        {
            float yOverlapMin = Mathf.Max(rA.yMin, rB.yMin);
            float yOverlapMax = Mathf.Min(rA.yMax, rB.yMax);
            if (yOverlapMax > yOverlapMin + eps)
            {
                float x = Mathf.Abs(rA.xMax - rB.xMin) < eps ? rA.xMax : rA.xMin;
                start = new Vector2(x, yOverlapMin);
                end   = new Vector2(x, yOverlapMax);
                isHorizontal = false;
                return true;
            }
        }

        return false;
    }

    List<Vector2> GenerateTearProfile(Vector2 start, Vector2 end,
                                       bool isHorizontal, int edgeHash)
    {
        var rng = new System.Random(config.randomSeed + edgeHash);
        int subdivs = config.tearSubdivisions;

        List<Vector2> points = new List<Vector2> { start };

        Vector2 dir  = (end - start).normalized;
        // Perpendicular: for horizontal edges displace in Y, for vertical in X
        Vector2 perp = isHorizontal ? new Vector2(0f, 1f) : new Vector2(1f, 0f);

        for (int i = 1; i < subdivs; i++)
        {
            float t = (float)i / subdivs;
            Vector2 basePoint = Vector2.Lerp(start, end, t);
            float offset = ((float)rng.NextDouble() * 2f - 1f) * config.tearAmplitude;
            Vector2 displaced = basePoint + perp * offset;
            // Clamp to [0,1] normalized space
            displaced.x = Mathf.Clamp01(displaced.x);
            displaced.y = Mathf.Clamp01(displaced.y);
            points.Add(displaced);
        }

        points.Add(end);
        return points;
    }

    // =========================================================================
    // Piece Creation
    // =========================================================================

    void CreatePieces()
    {
        int n = config.pieceRects.Count;
        for (int i = 0; i < n; i++)
        {
            Rect rect = config.pieceRects[i];

            // Piece world-space center
            float cx = rect.x + rect.width  * 0.5f;
            float cy = rect.y + rect.height * 0.5f;
            Vector3 pieceWorldCenter = NormalizedToWorld(cx, cy);

            // Build outline in normalized space, then convert to world
            List<Vector2> outline = BuildPieceOutline(i);

            Mesh mesh = BuildMesh(outline, pieceWorldCenter);

            // Create GO
            GameObject go = new GameObject($"KintsugiPiece_{i}");
            go.layer = KintsugiLayer;
            go.transform.SetParent(puzzleCenter, worldPositionStays: false);
            go.transform.position = pieceWorldCenter;

            // MeshFilter + MeshRenderer
            MeshFilter   mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mf.mesh = mesh;

            // Assign material with puzzle texture
            if (pieceMaterial != null)
            {
                Material mat = new Material(pieceMaterial);
                if (config.puzzleTexture != null)
                    mat.mainTexture = config.puzzleTexture;
                mr.material = mat;
            }

            // PolygonCollider2D in local space
            PolygonCollider2D poly = go.AddComponent<PolygonCollider2D>();
            Vector2[] localPath = new Vector2[outline.Count];
            for (int k = 0; k < outline.Count; k++)
            {
                Vector3 worldPt = NormalizedToWorld(outline[k].x, outline[k].y);
                localPath[k] = (Vector2)(worldPt - pieceWorldCenter);
            }
            poly.SetPath(0, localPath);

            // KintsugiPiece component
            KintsugiPiece piece = go.AddComponent<KintsugiPiece>();
            piece.Initialize(gameController, config, pieceWorldCenter, i, pieceWorldCenter);

            _pieces.Add(piece);
        }
    }

    /// <summary>
    /// Builds the CCW normalized-space outline for piece i.
    /// Walks Bottom→Right→Top→Left sides; uses tear profiles on shared edges.
    /// </summary>
    List<Vector2> BuildPieceOutline(int i)
    {
        Rect r = config.pieceRects[i];
        int n = config.pieceRects.Count;

        // Corners in normalized space
        Vector2 BL = new Vector2(r.xMin, r.yMin);
        Vector2 BR = new Vector2(r.xMax, r.yMin);
        Vector2 TR = new Vector2(r.xMax, r.yMax);
        Vector2 TL = new Vector2(r.xMin, r.yMax);

        // Four sides: (start, end) in the CCW walk direction
        // CCW for standard math = Bottom (BL→BR), Right (BR→TR), Top (TR→TL), Left (TL→BL)
        var sides = new (Vector2 from, Vector2 to, bool isHorizontal)[]
        {
            (BL, BR, true),   // bottom
            (BR, TR, false),  // right
            (TR, TL, true),   // top
            (TL, BL, false),  // left
        };

        List<Vector2> outline = new List<Vector2>();

        foreach (var side in sides)
        {
            // Find neighbour sharing this edge
            int neighbor = FindNeighborOnEdge(i, side.from, side.to, side.isHorizontal);

            List<Vector2> edgePoints;
            if (neighbor >= 0 && _tearEdges.TryGetValue($"{i}_{neighbor}", out List<Vector2> tear))
            {
                // Ensure the tear runs from 'side.from' to 'side.to'
                edgePoints = OrientTearToSide(tear, side.from, side.to);
            }
            else
            {
                edgePoints = new List<Vector2> { side.from, side.to };
            }

            // Skip first point of every segment to avoid duplicates
            for (int k = 1; k < edgePoints.Count; k++)
                outline.Add(edgePoints[k]);
        }

        return outline;
    }

    int FindNeighborOnEdge(int i, Vector2 edgeFrom, Vector2 edgeTo, bool isHorizontal)
    {
        int n = config.pieceRects.Count;
        for (int j = 0; j < n; j++)
        {
            if (j == i) continue;
            string key = $"{i}_{j}";
            if (!_tearEdges.ContainsKey(key)) continue;

            // The tear profile starts at one edge endpoint and ends at the other
            List<Vector2> tear = _tearEdges[key];
            if (tear.Count < 2) continue;

            Vector2 tStart = tear[0];
            Vector2 tEnd   = tear[tear.Count - 1];
            float eps = 0.001f;

            bool startsMatch = (Vector2.Distance(tStart, edgeFrom) < eps &&
                                Vector2.Distance(tEnd,   edgeTo)   < eps);
            bool endsMatch   = (Vector2.Distance(tStart, edgeTo)   < eps &&
                                Vector2.Distance(tEnd,   edgeFrom) < eps);

            if (startsMatch || endsMatch)
                return j;
        }
        return -1;
    }

    List<Vector2> OrientTearToSide(List<Vector2> tear, Vector2 wantFrom, Vector2 wantTo)
    {
        if (tear.Count < 2) return tear;
        float eps = 0.001f;
        bool reversed = Vector2.Distance(tear[0], wantFrom) > eps;
        if (reversed)
        {
            List<Vector2> r = new List<Vector2>(tear);
            r.Reverse();
            return r;
        }
        return tear;
    }

    Mesh BuildMesh(List<Vector2> normalizedOutline, Vector3 pieceWorldCenter)
    {
        int vCount = normalizedOutline.Count;
        Vector3[] vertices = new Vector3[vCount];
        Vector2[] uvs      = new Vector2[vCount];

        for (int k = 0; k < vCount; k++)
        {
            Vector2 n = normalizedOutline[k];
            Vector3 worldPt = NormalizedToWorld(n.x, n.y);
            vertices[k] = worldPt - pieceWorldCenter; // local space
            uvs[k]      = n;                          // UV = normalized coords
        }

        int[] tris = Triangulator.Triangulate(normalizedOutline);

        Mesh mesh = new Mesh();
        mesh.vertices  = vertices;
        mesh.uv        = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // =========================================================================
    // Gold Seam Creation
    // =========================================================================

    void CreateGoldSeams()
    {
        int n = config.pieceRects.Count;
        for (int a = 0; a < n; a++)
        {
            for (int b = a + 1; b < n; b++)
            {
                string key = $"{a}_{b}";
                if (!_tearEdges.TryGetValue(key, out List<Vector2> profile)) continue;

                // Create LineRenderer child under GoldSeams
                GameObject seamGo = new GameObject($"Seam_{a}_{b}");
                seamGo.transform.SetParent(goldSeamsParent ?? puzzleCenter, false);

                LineRenderer lr = seamGo.AddComponent<LineRenderer>();
                lr.useWorldSpace   = true;
                lr.positionCount   = profile.Count;
                lr.startWidth      = config.goldSeamWidth;
                lr.endWidth        = config.goldSeamWidth;
                lr.material        = config.goldSeamMaterial;
                lr.sortingOrder    = 10; // in front of pieces
                lr.enabled         = false; // hidden until snap

                // Convert normalized → world
                for (int k = 0; k < profile.Count; k++)
                {
                    Vector3 wp = NormalizedToWorld(profile[k].x, profile[k].y);
                    wp.z = -1f; // seam layer
                    lr.SetPosition(k, wp);
                }

                // Register on both pieces
                if (a < _pieces.Count) _pieces[a].adjacentSeams[b] = lr;
                if (b < _pieces.Count) _pieces[b].adjacentSeams[a] = lr;
            }
        }
    }

    // =========================================================================
    // Scatter
    // =========================================================================

    void ScatterPieces()
    {
        Debug.Log($"[Kintsugi] ScatterPieces — radius={config.scatterRadius}, pieces={_pieces.Count}");
        var rng = new System.Random(config.randomSeed + 999);
        float zBase = -0.1f;

        for (int i = 0; i < _pieces.Count; i++)
        {
            double angle  = rng.NextDouble() * Mathf.PI * 2.0;
            double radius = rng.NextDouble() * config.scatterRadius;

            Vector3 offset = new Vector3(
                (float)(Mathf.Cos((float)angle) * radius),
                (float)(Mathf.Sin((float)angle) * radius),
                zBase + i * 0.01f
            );

            Vector3 scatterPos = puzzleCenter.position + offset;
            _pieces[i].transform.position = scatterPos;

            // Re-initialize with the actual scattered position for reference
            _pieces[i].Initialize(gameController, config,
                                  _pieces[i].targetWorldPosition,
                                  i, scatterPos);
        }
    }

    // =========================================================================
    // Background Ghost Image
    // =========================================================================

    void CreateBackground()
    {
        if (config.puzzleTexture == null) return;

        GameObject bg = new GameObject("PuzzleBackground");
        bg.transform.SetParent(puzzleCenter, worldPositionStays: false);
        bg.transform.localPosition = new Vector3(0f, 0f, 0.5f); // behind pieces

        // Create a sprite from the texture at 100 PPU
        Sprite sprite = Sprite.Create(
            config.puzzleTexture,
            new Rect(0, 0, config.puzzleTexture.width, config.puzzleTexture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        SpriteRenderer sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.color        = new Color(1f, 1f, 1f, backgroundAlpha);
        sr.sortingOrder = -1; // always behind pieces

        // Scale GO so sprite fills exactly puzzleWorldWidth x puzzleWorldHeight
        float spriteWorldW = config.puzzleTexture.width  / 100f;
        float spriteWorldH = config.puzzleTexture.height / 100f;
        bg.transform.localScale = new Vector3(
            config.puzzleWorldWidth  / spriteWorldW,
            config.puzzleWorldHeight / spriteWorldH,
            1f
        );
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    Vector3 NormalizedToWorld(float nx, float ny)
    {
        Vector3 center = puzzleCenter.position;
        return center
            + Vector3.right * (nx - 0.5f) * config.puzzleWorldWidth
            + Vector3.up    * (ny - 0.5f) * config.puzzleWorldHeight;
    }
}
