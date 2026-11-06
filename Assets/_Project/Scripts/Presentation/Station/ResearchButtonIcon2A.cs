using UnityEngine;
using UnityEngine.UI;

/// <summary>Resolution-independent atom icon: blue orbital rings, central nucleus and outer button trim.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ResearchButtonIcon2A : MaskableGraphic
{
    public bool FilledBackground;
    protected override void OnEnable()
    {
        // Also repair instances created before CanvasRenderer became a required dependency.
        if (GetComponent<CanvasRenderer>() == null) gameObject.AddComponent<CanvasRenderer>();
        base.OnEnable();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); var rect = GetPixelAdjustedRect(); var center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * .46f;
        if (FilledBackground)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, new Color32(6, 30, 51, 245), Vector2.zero);
            for (int i = 0; i <= 48; i++)
            {
                float angle = i * 2 * Mathf.PI / 48;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    new Color32(6, 30, 51, 245), Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
        Ellipse(mesh, center, radius, radius, 0, Mathf.Max(1, radius * .055f));
        for (int orbit = 0; orbit < 3; orbit++)
            Ellipse(mesh, center, radius * .72f, radius * .25f, orbit * Mathf.PI / 3f, Mathf.Max(1, radius * .07f));
        Ellipse(mesh, center, radius * .09f, radius * .09f, 0, radius * .15f);
    }
    private void Ellipse(VertexHelper mesh, Vector2 center, float rx, float ry, float angle, float thickness)
    {
        const int steps = 48;
        float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
        for (int i = 0; i < steps; i++)
        {
            float a = 2 * Mathf.PI * i / steps, b = 2 * Mathf.PI * (i + 1) / steps;
            Vector2 p = new Vector2(rx * Mathf.Cos(a), ry * Mathf.Sin(a));
            Vector2 q = new Vector2(rx * Mathf.Cos(b), ry * Mathf.Sin(b));
            p = center + new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
            q = center + new Vector2(c * q.x - s * q.y, s * q.x + c * q.y);
            Vector2 normal = (q - p).normalized; normal = new Vector2(-normal.y, normal.x) * thickness * .5f;
            int start = mesh.currentVertCount;
            mesh.AddVert(p - normal, color, Vector2.zero); mesh.AddVert(p + normal, color, Vector2.zero);
            mesh.AddVert(q + normal, color, Vector2.zero); mesh.AddVert(q - normal, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
