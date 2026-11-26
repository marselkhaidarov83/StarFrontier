#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

// Editor-only vector icon used by the simulator camera buttons.
public sealed class SystemEditorZoomGlyph2A : MaskableGraphic
{
    public bool ZoomIn { get; set; }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float scale = Mathf.Min(r.width, r.height);
        Vector2 center = r.center + new Vector2(-.10f, .10f) * scale;
        float radius = scale * .29f;
        float thickness = scale * .065f;
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            float b = (i + 1) * Mathf.PI * 2f / segments;
            Vector2 va = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 vb = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            Quad(vh, center + va * (radius - thickness), center + va * radius,
                center + vb * radius, center + vb * (radius - thickness));
        }
        Vector2 diagonal = new Vector2(1f, -1f).normalized;
        Line(vh, center + diagonal * (radius - thickness * .5f),
            center + diagonal * scale * .62f, thickness * 1.25f);
        float arm = scale * .13f;
        Line(vh, center - Vector2.right * arm, center + Vector2.right * arm, thickness);
        if (ZoomIn)
            Line(vh, center - Vector2.up * arm, center + Vector2.up * arm, thickness);
    }

    private void Line(VertexHelper vh, Vector2 a, Vector2 b, float width)
    {
        Vector2 delta = (b - a).normalized;
        Vector2 n = new Vector2(-delta.y, delta.x) * (width * .5f);
        Quad(vh, a - n, a + n, b + n, b - n);
    }

    private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero);
        vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero);
        vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }
}
#endif
