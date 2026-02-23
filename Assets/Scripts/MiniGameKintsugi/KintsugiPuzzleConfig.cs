using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Kintsugi Puzzle Config", fileName = "KintsugiPuzzle_Default")]
public class KintsugiPuzzleConfig : ScriptableObject
{
    [Header("Texture")]
    [Tooltip("Full puzzle image. Must have Read/Write enabled in Import Settings.")]
    public Texture2D puzzleTexture;

    [Header("Piece Layout")]
    [Tooltip("Normalized (0-1) Rects defining each piece's region. Use presets below as reference.")]
    public List<Rect> pieceRects = new List<Rect>();

    [Header("World Size")]
    public float puzzleWorldWidth  = 5f;
    public float puzzleWorldHeight = 4f;

    [Header("Tear Generation")]
    [Tooltip("Number of interior subdivision points along each torn edge.")]
    public int tearSubdivisions = 12;
    [Tooltip("Max perpendicular displacement of tear points, in normalized (0-1) space.")]
    public float tearAmplitude = 0.15f;
    public int randomSeed = 42;

    [Header("Snap Behaviour")]
    [Tooltip("World-unit distance within which a released piece snaps to target.")]
    public float snapThreshold = 0.4f;
    public float snapDuration  = 0.25f;
    public AnimationCurve snapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Scatter")]
    [Tooltip("Radius (world units) within which pieces are randomly scattered at start.")]
    public float scatterRadius = 2.5f;

    [Header("Gold Seam")]
    public Material goldSeamMaterial;
    public float goldSeamWidth = 0.04f;

    // -------------------------------------------------------------------------
    // Designer reference presets (call from editor button or test code)
    // -------------------------------------------------------------------------

    /// <summary>4-piece 2x2 grid.</summary>
    public static List<Rect> Preset4Pieces() => new List<Rect>
    {
        new Rect(0f,  0.5f, 0.5f, 0.5f),   // top-left
        new Rect(0.5f,0.5f, 0.5f, 0.5f),   // top-right
        new Rect(0f,  0f,   0.5f, 0.5f),   // bottom-left
        new Rect(0.5f,0f,   0.5f, 0.5f),   // bottom-right
    };

    /// <summary>5-piece layout: 2 wide top + 2 wide middle + 1 full-width bottom.</summary>
    public static List<Rect> Preset5Pieces() => new List<Rect>
    {
        new Rect(0f,  0.4f, 0.5f, 0.6f),   // top-left
        new Rect(0.5f,0.4f, 0.5f, 0.6f),   // top-right
        new Rect(0f,  0f,   0.5f, 0.4f),   // bottom-left
        new Rect(0.5f,0f,   0.5f, 0.4f),   // bottom-right
        new Rect(0f,  0f,   1f,   0.25f),  // spanning bottom strip
    };

    /// <summary>6-piece 2x3 grid.</summary>
    public static List<Rect> Preset6Pieces()
    {
        float h = 1f / 3f;
        return new List<Rect>
        {
            new Rect(0f,  2*h, 0.5f, h),
            new Rect(0.5f,2*h, 0.5f, h),
            new Rect(0f,  h,   0.5f, h),
            new Rect(0.5f,h,   0.5f, h),
            new Rect(0f,  0f,  0.5f, h),
            new Rect(0.5f,0f,  0.5f, h),
        };
    }
}
