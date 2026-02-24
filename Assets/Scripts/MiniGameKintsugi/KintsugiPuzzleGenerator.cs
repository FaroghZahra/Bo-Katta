using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedurally generates irregular tear edges, per-piece meshes,
/// PolygonCollider2Ds, UV mapping, and gold seam LineRenderers.
/// Call Initialize(levelIndex) explicitly (done by MiniGameSequenceController at +100).
/// </summary>
public class KintsugiPuzzleGenerator : MonoBehaviour
{
    [Header("References")]
    public KintsugiGameController  gameController;
    public Material                pieceMaterial;   // Sprites/Default with puzzle texture
    public Transform               puzzleCenter;    // Empty GO at world origin
    public Transform               goldSeamsParent; // Empty child GO named "GoldSeams"

    [Header("Level Textures")]
    [Tooltip("Index 0 = Level 1, index 1 = Level 2, index 2 = Level 3.")]
    public Texture2D[]     levelTextures;

    [Header("Seam & Snap")]
    public Material        goldSeamMaterial;
    public AnimationCurve  snapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Background")]
    [Range(0f, 1f)]
    public float backgroundAlpha = 0.1f;

    // Runtime data loaded from JSON
    private KintsugiLevelData    _data;
    private Texture2D            _texture;
    private List<Rect>           _pieceRects;

    // "minIdx_maxIdx" → normalized tear points (A's view)
    private Dictionary<string, List<Vector2>> _tearEdges = new Dictionary<string, List<Vector2>>();
    private List<KintsugiPiece> _pieces = new List<KintsugiPiece>();

    private const int KintsugiLayer = 8; // Physics Layer "KintsugiPieces"

    // =========================================================================
    // Entry Point
    // =========================================================================

    public void Initialize(int levelIndex)
    {
        TextAsset json = Resources.Load<TextAsset>($"Kintsugi/Level{levelIndex}");
        if (json == null)
        {
            Debug.LogError($"[KintsugiPuzzleGenerator] Could not load Resources/Kintsugi/Level{levelIndex}.json. " +
                           "Make sure the file exists in Assets/Resources/Kintsugi/.");
            return;
        }

        _data = JsonUtility.FromJson<KintsugiLevelData>(json.text);
        if (_data == null || _data.pieceRects == null || _data.pieceRects.Count == 0)
        {
            Debug.LogError($"[KintsugiPuzzleGenerator] Level{levelIndex}.json parsed but has no pieceRects.");
            return;
        }

        // Convert KintsugiRectData → UnityEngine.Rect
        _pieceRects = _data.pieceRects.ConvertAll(d => new Rect(d.x, d.y, d.w, d.h));

        // Pick texture by level index (0-based array)
        int texIdx = levelIndex - 1;
        _texture = (levelTextures != null && texIdx >= 0 && texIdx < levelTextures.Length)
                   ? levelTextures[texIdx]
                   : null;

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
        int n = _pieceRects.Count;
        for (int a = 0; a < n; a++)
        {
            for (int b = a + 1; b < n; b++)
            {
                Rect rA = _pieceRects[a];
                Rect rB = _pieceRects[b];

                if (TryGetSharedEdge(rA, rB, out Vector2 start, out Vector2 end, out bool isHorizontal))
                {
                    int edgeHash = a * 1000 + b;
                    List<Vector2> profile = GenerateTearProfile(start, end, isHorizontal, edgeHash);

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
        var rng = new System.Random(_data.randomSeed + edgeHash);
        int subdivs = _data.tearSubdivisions;

        List<Vector2> points = new List<Vector2> { start };

        Vector2 dir  = (end - start).normalized;
        Vector2 perp = isHorizontal ? new Vector2(0f, 1f) : new Vector2(1f, 0f);

        for (int i = 1; i < subdivs; i++)
        {
            float t = (float)i / subdivs;
            Vector2 basePoint = Vector2.Lerp(start, end, t);
            float offset = ((float)rng.NextDouble() * 2f - 1f) * _data.tearAmplitude;
            Vector2 displaced = basePoint + perp * offset;
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
        int n = _pieceRects.Count;
        for (int i = 0; i < n; i++)
        {
            Rect rect = _pieceRects[i];

            float cx = rect.x + rect.width  * 0.5f;
            float cy = rect.y + rect.height * 0.5f;
            Vector3 pieceWorldCenter = NormalizedToWorld(cx, cy);

            List<Vector2> outline = BuildPieceOutline(i);
            Mesh mesh = BuildMesh(outline, pieceWorldCenter);

            GameObject go = new GameObject($"KintsugiPiece_{i}");
            go.layer = KintsugiLayer;
            go.transform.SetParent(puzzleCenter, worldPositionStays: false);
            go.transform.position = pieceWorldCenter;

            MeshFilter   mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mf.mesh = mesh;

            if (pieceMaterial != null)
            {
                Material mat = new Material(pieceMaterial);
                if (_texture != null)
                    mat.mainTexture = _texture;
                mr.material = mat;
            }

            PolygonCollider2D poly = go.AddComponent<PolygonCollider2D>();
            Vector2[] localPath = new Vector2[outline.Count];
            for (int k = 0; k < outline.Count; k++)
            {
                Vector3 worldPt = NormalizedToWorld(outline[k].x, outline[k].y);
                localPath[k] = (Vector2)(worldPt - pieceWorldCenter);
            }
            poly.SetPath(0, localPath);

            KintsugiPiece piece = go.AddComponent<KintsugiPiece>();
            piece.Initialize(gameController,
                             _data.snapThreshold, _data.snapDuration, snapCurve,
                             pieceWorldCenter, i, pieceWorldCenter);

            _pieces.Add(piece);
        }
    }

    List<Vector2> BuildPieceOutline(int i)
    {
        Rect r = _pieceRects[i];

        Vector2 BL = new Vector2(r.xMin, r.yMin);
        Vector2 BR = new Vector2(r.xMax, r.yMin);
        Vector2 TR = new Vector2(r.xMax, r.yMax);
        Vector2 TL = new Vector2(r.xMin, r.yMax);

        var sides = new (Vector2 from, Vector2 to, bool isHorizontal)[]
        {
            (BL, BR, true),
            (BR, TR, false),
            (TR, TL, true),
            (TL, BL, false),
        };

        List<Vector2> outline = new List<Vector2>();

        foreach (var side in sides)
        {
            var segments = FindAllNeighborsOnEdge(i, side.from, side.to, side.isHorizontal);

            if (segments.Count == 0)
            {
                // Outer edge — side.from is already the last outline point
                outline.Add(side.to);
            }
            else
            {
                // Concatenate all sub-segment tear profiles in order.
                // Skip index 0 of each segment: it's the junction already added by the previous entry.
                foreach (var (_, orientedTear) in segments)
                {
                    for (int k = 1; k < orientedTear.Count; k++)
                        outline.Add(orientedTear[k]);
                }
            }
        }

        return outline;
    }

    // Returns all tear sub-segments along the given side, each oriented from→to, sorted in that direction.
    // Handles the case where a single side is shared with multiple neighbours (T-junction layouts).
    List<(int, List<Vector2>)> FindAllNeighborsOnEdge(int i,
                                                       Vector2 edgeFrom, Vector2 edgeTo,
                                                       bool isHorizontal)
    {
        float eps = 0.001f;
        var candidates = new List<(int neighbor, List<Vector2> orientedTear, float sortKey)>();
        int n = _pieceRects.Count;

        for (int j = 0; j < n; j++)
        {
            if (j == i) continue;
            string key = $"{i}_{j}";
            if (!_tearEdges.TryGetValue(key, out List<Vector2> tear)) continue;
            if (tear.Count < 2) continue;

            Vector2 tA = tear[0];
            Vector2 tB = tear[tear.Count - 1];

            // Both endpoints must sit on this side's constant coordinate (y for horizontal, x for vertical)
            float edgeConst = isHorizontal ? edgeFrom.y : edgeFrom.x;
            float tAConst   = isHorizontal ? tA.y       : tA.x;
            float tBConst   = isHorizontal ? tB.y       : tB.x;
            if (Mathf.Abs(tAConst - edgeConst) > eps || Mathf.Abs(tBConst - edgeConst) > eps)
                continue;

            // Both endpoints must fall within the side's span
            float sideMin = isHorizontal ? Mathf.Min(edgeFrom.x, edgeTo.x) : Mathf.Min(edgeFrom.y, edgeTo.y);
            float sideMax = isHorizontal ? Mathf.Max(edgeFrom.x, edgeTo.x) : Mathf.Max(edgeFrom.y, edgeTo.y);
            float tSpanA  = isHorizontal ? tA.x : tA.y;
            float tSpanB  = isHorizontal ? tB.x : tB.y;
            if (tSpanA < sideMin - eps || tSpanA > sideMax + eps) continue;
            if (tSpanB < sideMin - eps || tSpanB > sideMax + eps) continue;

            // Orient the tear to go in the same direction as edgeFrom→edgeTo
            bool sidePositive = isHorizontal ? (edgeTo.x > edgeFrom.x) : (edgeTo.y > edgeFrom.y);
            bool tearPositive = isHorizontal ? (tB.x    > tA.x)        : (tB.y    > tA.y);
            List<Vector2> oriented;
            if (sidePositive != tearPositive)
            {
                oriented = new List<Vector2>(tear);
                oriented.Reverse();
            }
            else
            {
                oriented = tear;
            }

            float sortKey = isHorizontal ? oriented[0].x : oriented[0].y;
            candidates.Add((j, oriented, sortKey));
        }

        // Sort sub-segments in the direction of edgeFrom→edgeTo
        bool ascending = isHorizontal ? (edgeTo.x > edgeFrom.x) : (edgeTo.y > edgeFrom.y);
        if (ascending)
            candidates.Sort((a, b) => a.sortKey.CompareTo(b.sortKey));
        else
            candidates.Sort((a, b) => b.sortKey.CompareTo(a.sortKey));

        return candidates.ConvertAll(c => (c.neighbor, c.orientedTear));
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
            vertices[k] = worldPt - pieceWorldCenter;
            uvs[k]      = n;
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
        List<SeamTraceState> allSeams = new List<SeamTraceState>();
        Transform seamParent = goldSeamsParent ?? puzzleCenter;

        int n = _pieceRects.Count;
        for (int a = 0; a < n; a++)
        {
            for (int b = a + 1; b < n; b++)
            {
                string key = $"{a}_{b}";
                if (!_tearEdges.TryGetValue(key, out List<Vector2> profile)) continue;

                // --- Gold LineRenderer (progress indicator, starts hidden) ---
                GameObject seamGo = new GameObject($"Seam_{a}_{b}");
                seamGo.transform.SetParent(seamParent, false);

                LineRenderer lr = seamGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = profile.Count;
                lr.startWidth    = _data.goldSeamWidth;
                lr.endWidth      = _data.goldSeamWidth;
                lr.material      = goldSeamMaterial;
                lr.sortingOrder  = 10;
                lr.enabled       = false;

                // Bake world positions and collect them for the state
                Vector3[] pts = new Vector3[profile.Count];
                for (int k = 0; k < profile.Count; k++)
                {
                    Vector3 wp = NormalizedToWorld(profile[k].x, profile[k].y);
                    wp.z = -1f;
                    lr.SetPosition(k, wp);
                    pts[k] = wp;
                }

                // --- Guide LineRenderer (faint hint, starts hidden) ---
                GameObject guideGo = new GameObject($"SeamGuide_{a}_{b}");
                guideGo.transform.SetParent(seamParent, false);

                LineRenderer guideLr = guideGo.AddComponent<LineRenderer>();
                guideLr.useWorldSpace = true;
                guideLr.positionCount = profile.Count;
                guideLr.startWidth    = _data.goldSeamWidth;
                guideLr.endWidth      = _data.goldSeamWidth;
                guideLr.material      = new Material(Shader.Find("Sprites/Default"));
                guideLr.startColor    = new Color(1f, 0.9f, 0.6f, 0.3f);
                guideLr.endColor      = new Color(1f, 0.9f, 0.6f, 0.3f);
                guideLr.sortingOrder  = 9;
                guideLr.enabled       = false;

                for (int k = 0; k < pts.Length; k++)
                    guideLr.SetPosition(k, pts[k]);

                // --- Build state ---
                SeamTraceState state = new SeamTraceState
                {
                    goldLR  = lr,
                    guideLR = guideLr,
                    points  = pts
                };

                if (a < _pieces.Count) _pieces[a].adjacentSeams[b] = state;
                if (b < _pieces.Count) _pieces[b].adjacentSeams[a] = state;

                allSeams.Add(state);
            }
        }

        gameController.SetSeams(allSeams);
    }

    // =========================================================================
    // Scatter
    // =========================================================================

    void ScatterPieces()
    {
        Debug.Log($"[Kintsugi] ScatterPieces — radius={_data.scatterRadius}, pieces={_pieces.Count}");
        var rng = new System.Random(_data.randomSeed + 999);
        float zBase = -0.1f;

        for (int i = 0; i < _pieces.Count; i++)
        {
            double angle  = rng.NextDouble() * Mathf.PI * 2.0;
            double radius = rng.NextDouble() * _data.scatterRadius;

            Vector3 offset = new Vector3(
                (float)(Mathf.Cos((float)angle) * radius),
                (float)(Mathf.Sin((float)angle) * radius),
                zBase + i * 0.01f
            );

            Vector3 scatterPos = puzzleCenter.position + offset;
            _pieces[i].transform.position = scatterPos;

            _pieces[i].Initialize(gameController,
                                  _data.snapThreshold, _data.snapDuration, snapCurve,
                                  _pieces[i].targetWorldPosition,
                                  i, scatterPos);
        }
    }

    // =========================================================================
    // Background Ghost Image
    // =========================================================================

    void CreateBackground()
    {
        if (_texture == null) return;

        GameObject bg = new GameObject("PuzzleBackground");
        bg.transform.SetParent(puzzleCenter, worldPositionStays: false);
        bg.transform.localPosition = new Vector3(0f, 0f, 0.5f);

        Sprite sprite = Sprite.Create(
            _texture,
            new Rect(0, 0, _texture.width, _texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        SpriteRenderer sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.color        = new Color(1f, 1f, 1f, backgroundAlpha);
        sr.sortingOrder = -1;

        float spriteWorldW = _texture.width  / 100f;
        float spriteWorldH = _texture.height / 100f;
        bg.transform.localScale = new Vector3(
            _data.puzzleWorldWidth  / spriteWorldW,
            _data.puzzleWorldHeight / spriteWorldH,
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
            + Vector3.right * (nx - 0.5f) * _data.puzzleWorldWidth
            + Vector3.up    * (ny - 0.5f) * _data.puzzleWorldHeight;
    }
}
