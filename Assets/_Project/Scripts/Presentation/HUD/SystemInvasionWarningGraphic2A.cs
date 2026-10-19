using UnityEngine;
using UnityEngine.UI;

// Gradient edges and a warning sign in one non-interactive mesh; no texture required.
public sealed class SystemInvasionWarningGraphic2A : MaskableGraphic
{
    private Rect _bounds;
    private Color _tint;
    private float _edgeAlpha;
    private float _signAlpha;
    private float _edgeWidth;
    private float _signSize;
    private bool _showSign;

    public void Show(Rect bounds, Color tint, float edgeAlpha, float signAlpha,
        float edgeWidth, float signSize, bool showSign = true)
    {
        _bounds = bounds;
        _tint = tint;
        _edgeAlpha = edgeAlpha;
        _signAlpha = signAlpha;
        _edgeWidth = edgeWidth;
        _signSize = signSize;
        _showSign = showSign;
        color = Color.white;
        enabled = true;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float width = Mathf.Min(_bounds.width, _bounds.height) * _edgeWidth;
        const int rings = 12;
        for (int i = 0; i < rings; i++)
        {
            float outerT = (float)i / rings;
            float innerT = (float)(i + 1) / rings;
            Rect outer = Inset(_bounds, width * outerT);
            Rect inner = Inset(_bounds, width * innerT);
            Color outerColor = Tint(_edgeAlpha * Mathf.SmoothStep(1f, 0f, outerT));
            Color innerColor = Tint(_edgeAlpha * Mathf.SmoothStep(1f, 0f, innerT));
            Quad(vh, new Vector2(outer.xMin, outer.yMin), new Vector2(outer.xMin, outer.yMax),
                new Vector2(inner.xMin, inner.yMax), new Vector2(inner.xMin, inner.yMin), outerColor, innerColor);
            Quad(vh, new Vector2(outer.xMin, outer.yMax), new Vector2(outer.xMax, outer.yMax),
                new Vector2(inner.xMax, inner.yMax), new Vector2(inner.xMin, inner.yMax), outerColor, innerColor);
            Quad(vh, new Vector2(outer.xMax, outer.yMax), new Vector2(outer.xMax, outer.yMin),
                new Vector2(inner.xMax, inner.yMin), new Vector2(inner.xMax, inner.yMax), outerColor, innerColor);
            Quad(vh, new Vector2(outer.xMax, outer.yMin), new Vector2(outer.xMin, outer.yMin),
                new Vector2(inner.xMin, inner.yMin), new Vector2(inner.xMax, inner.yMin), outerColor, innerColor);
        }

        if (!_showSign)
            return;

        float size = Mathf.Min(_signSize, Mathf.Min(_bounds.width, _bounds.height) * 0.6f);
        Vector2 center = _bounds.center;
        Vector2 a = center + new Vector2(0f, size * 0.5f);
        Vector2 b = center + new Vector2(size * 0.5f, -size * 0.37f);
        Vector2 c = center + new Vector2(-size * 0.5f, -size * 0.37f);
        Color sign = Tint(_signAlpha);
        Stroke(vh, a, b, size * 0.045f, sign);
        Stroke(vh, b, c, size * 0.045f, sign);
        Stroke(vh, c, a, size * 0.045f, sign);
        Box(vh, new Rect(center.x - size * 0.035f, center.y - size * 0.08f,
            size * 0.07f, size * 0.3f), sign);
        Box(vh, new Rect(center.x - size * 0.035f, center.y - size * 0.23f,
            size * 0.07f, size * 0.07f), sign);
    }

    private Color Tint(float alpha)
    {
        Color result = _tint;
        result.a *= Mathf.Clamp01(alpha);
        return result;
    }

    private static Rect Inset(Rect rect, float amount) => Rect.MinMaxRect(
        rect.xMin + amount, rect.yMin + amount, rect.xMax - amount, rect.yMax - amount);

    private static void Stroke(VertexHelper vh, Vector2 start, Vector2 end, float width, Color tint)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 side = new Vector2(-direction.y, direction.x) * width * 0.5f;
        Quad(vh, start - side, end - side, end + side, start + side, tint, tint);
    }

    private static void Box(VertexHelper vh, Rect rect, Color tint) => Quad(vh,
        new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
        new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin), tint, tint);

    private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
        Color outer, Color inner)
    {
        int index = vh.currentVertCount;
        vh.AddVert(a, outer, Vector2.zero);
        vh.AddVert(b, outer, Vector2.zero);
        vh.AddVert(c, inner, Vector2.zero);
        vh.AddVert(d, inner, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}
