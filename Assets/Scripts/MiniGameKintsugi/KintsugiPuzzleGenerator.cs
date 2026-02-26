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
    private KintsugiLevelData         _data;
    private Texture2D                 _texture;
    private List<List<Vector2>>       _polyPieces = new List<List<Vector2>>();

    // "i_j" → tear profile from piece i's winding perspective
    private Dictionary<string, List<Vector2>> _tearEdges = new Dictionary<string, List<Vector2>>();
    private List<KintsugiPiece> _pieces = new List<KintsugiPiece>();

    private const int KintsugiLayer  = 8; // Physics Layer "KintsugiPieces" — used for Physics2D overlap queries
    private const int MiniGameLayer  = 9; // Rendering layer "MiniGame" — seen only by the minigame camera

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
        if (_data == null)
        {
            Debug.LogError($"[KintsugiPuzzleGenerator] Level{levelIndex}.json failed to parse.");
            return;
        }

        // Build unified polygon list from either pieces[] (polygon) or pieceRects[] (legacy rect)
        _polyPieces.Clear();
        _tearEdges.Clear();
        _pieces.Clear();

        if (_data.pieces != null && _data.pieces.Count > 0)
        {
            foreach (var pd in _data.pieces)
            {
                var verts = new List<Vector2>();
                foreach (var v in pd.vertices)
                    verts.Add(new Vector2(v.x, v.y));
                _polyPieces.Add(verts);
            }
        }
        else if (_data.pieceRects != null && _data.pieceRects.Count > 0)
        {
            foreach (var d in _data.pieceRects)
            {
                // CCW quad: BL → BR → TR → TL
                _polyPieces.Add(new List<Vector2>
                {
                    new Vector2(d.x,       d.y),
                    new Vector2(d.x + d.w, d.y),
                    new Vector2(d.x + d.w, d.y + d.h),
                    new Vector2(d.x,       d.y + d.h),
                });
            }
        }
        else
        {
            Debug.LogError($"[KintsugiPuzzleGenerator] Level{levelIndex}.json has neither pieces nor pieceRects.");
            return;
        }

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
    // Tear Edge Generation (works for any polygon layout)
    // =========================================================================

    void GenerateTearEdges()
    {
        int n = _polyPieces.Count;
        for (int a = 0; a < n; a++)
        {
            for (int b = a + 1; b < n; b++)
            {
                if (TryGetSharedPolyEdge(a, b, out Vector2 start, out Vector2 end))
                {
                    int edgeHash = a * 1000 + b;
                    List<Vector2> profile = GenerateTearProfile(start, end, edgeHash);

                    _tearEdges[$"{a}_{b}"] = profile;
                    List<Vector2> reversed = new List<Vector2>(profile);
                    reversed.Reverse();
                    _tearEdges[$"{b}_{a}"] = reversed;
                }
            }
        }
    }

    // Finds the shared edge between polygons a and b (opposite winding).
    // Returns the edge direction as seen from piece a's winding.
    bool TryGetSharedPolyEdge(int a, int b, out Vector2 start, out Vector2 end)
    {
        float eps = 0.001f;
        List<Vector2> polyA = _polyPieces[a];
        List<Vector2> polyB = _polyPieces[b];
        int nA = polyA.Count, nB = polyB.Count;

        for (int i = 0; i < nA; i++)
        {
            Vector2 a0 = polyA[i];
            Vector2 a1 = polyA[(i + 1) % nA];

            for (int j = 0; j < nB; j++)
            {
                Vector2 b0 = polyB[j];
                Vector2 b1 = polyB[(j + 1) % nB];

                // Adjacent pieces wind the shared edge in opposite directions: a0=b1, a1=b0
                if (Vector2.Distance(a0, b1) < eps && Vector2.Distance(a1, b0) < eps)
                {
                    start = a0; end = a1; return true;
                }
            }
        }

        start = end = Vector2.zero; return false;
    }

    List<Vector2> GenerateTearProfile(Vector2 start, Vector2 end, int edgeHash)
    {
        var rng = new System.Random(_data.randomSeed + edgeHash);
        int subdivs = _data.tearSubdivisions;

        List<Vector2> points = new List<Vector2> { start };

        Vector2 dir  = (end - start).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x); // perpendicular to the edge

        for (int i = 1; i < subdivs; i++)
        {
            float t = (float)i / subdivs;
            Vector2 basePoint = Vector2.Lerp(start, end, t);
            float offset = ((float)rng.NextDouble() * 2f - 1f) * _data.tearAmplitude;
            points.Add(basePoint + perp * offset);
        }

        points.Add(end);
        return points;
    }

    // =========================================================================
    // Piece Creation
    // =========================================================================

    void CreatePieces()
    {
        int n = _polyPieces.Count;
        for (int i = 0; i < n; i++)
        {
            List<Vector2> poly = _polyPieces[i];

            // Centroid of the polygon vertices
            Vector2 centroid = Vector2.zero;
            foreach (var v in poly) centroid += v;
            centroid /= poly.Count;
            Vector3 pieceWorldCenter = NormalizedToWorld(centroid.x, centroid.y);

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

            PolygonCollider2D poly2d = go.AddComponent<PolygonCollider2D>();
            Vector2[] localPath = new Vector2[outline.Count];
            for (int k = 0; k < outline.Count; k++)
            {
                Vector3 worldPt = NormalizedToWorld(outline[k].x, outline[k].y);
                localPath[k] = (Vector2)(worldPt - pieceWorldCenter);
            }
            poly2d.SetPath(0, localPath);

            KintsugiPiece piece = go.AddComponent<KintsugiPiece>();
            piece.Initialize(gameController,
                             _data.snapThreshold, _data.snapDuration, snapCurve,
                             pieceWorldCenter, i, pieceWorldCenter);

            _pieces.Add(piece);
        }
    }

    // Builds the outline for piece i: walks each polygon edge, replacing shared edges
    // with their stored tear profile (from piece i's winding perspective).
    List<Vector2> BuildPieceOutline(int i)
    {
        List<Vector2> poly = _polyPieces[i];
        int n = poly.Count;
        List<Vector2> outline = new List<Vector2>();
        float eps = 0.001f;

        for (int k = 0; k < n; k++)
        {
            Vector2 from = poly[k];
            Vector2 to   = poly[(k + 1) % n];

            bool foundTear = false;
            for (int j = 0; j < _polyPieces.Count; j++)
            {
                if (j == i) continue;
                string key = $"{i}_{j}";
                if (!_tearEdges.TryGetValue(key, out List<Vector2> tear)) continue;

                // "i_j" is already oriented in piece i's winding direction
                if (Vector2.Distance(tear[0], from) < eps &&
                    Vector2.Distance(tear[tear.Count - 1], to) < eps)
                {
                    // Skip tear[0] (== from, implicit from previous edge); add rest
                    for (int m = 1; m < tear.Count; m++)
                        outline.Add(tear[m]);
                    foundTear = true;
                    break;
                }
            }

            if (!foundTear)
                outline.Add(to); // outer edge — just add the endpoint
        }

        return outline;
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

        int n = _polyPieces.Count;
        for (int a = 0; a < n; a++)
        {
            for (int b = a + 1; b < n; b++)
            {
                string key = $"{a}_{b}";
                if (!_tearEdges.TryGetValue(key, out List<Vector2> profile)) continue;

                // --- Gold LineRenderer (progress indicator, starts hidden) ---
                GameObject seamGo = new GameObject($"Seam_{a}_{b}");
                seamGo.layer = MiniGameLayer;
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
                guideGo.layer = MiniGameLayer;
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
        bg.layer = MiniGameLayer;
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
