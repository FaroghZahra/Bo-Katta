using System;
using System.Collections.Generic;

[Serializable]
public class KintsugiRectData
{
    public float x, y, w, h;
}

[Serializable]
public class KintsugiVec2Data
{
    public float x, y;
}

[Serializable]
public class KintsugiPolyData
{
    public List<KintsugiVec2Data> vertices;
}

[Serializable]
public class KintsugiLevelData
{
    public int                    levelIndex;
    public List<KintsugiRectData> pieceRects;   // legacy rect-based layout
    public List<KintsugiPolyData> pieces;        // polygon layout (overrides pieceRects when present)
    public float                  puzzleWorldWidth;
    public float                  puzzleWorldHeight;
    public int                    tearSubdivisions;
    public float                  tearAmplitude;
    public int                    randomSeed;
    public float                  snapThreshold;
    public float                  snapDuration;
    public float                  scatterRadius;
    public float                  goldSeamWidth;
}
