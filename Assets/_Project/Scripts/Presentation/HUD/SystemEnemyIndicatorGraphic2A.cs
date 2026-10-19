using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// All markers share one UI mesh. They do not intercept taps or need texture assets.
public sealed class SystemEnemyIndicatorGraphic2A : MaskableGraphic
{
    private struct Indicator
    {
        public Vector2 Center;
        public Vector2 Direction;
        public Color32 Tint;
    }

    private readonly List<Indicator> _indicators = new List<Indicator>();
    private Vector2 _size;

    public void BeginFrame(Vector2 size)
    {
        _indicators.Clear();
        _size = size;
        color = Color.white;
    }

    public void AddIndicator(Vector2 center, Vector2 direction, Color tint)
    {
        // Stay below the UI mesh's 16-bit vertex limit.
        if (_indicators.Count >= 21000)
            return;
        _indicators.Add(new Indicator { Center = center, Direction = direction, Tint = tint });
    }

    public void EndFrame()
    {
        enabled = _indicators.Count > 0;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        for (int i = 0; i < _indicators.Count; i++)
        {
            Indicator indicator = _indicators[i];
            Color32 tint = indicator.Tint;
            Vector2 forward = indicator.Direction * (_size.y * 0.5f);
            Vector2 side = new Vector2(-indicator.Direction.y, indicator.Direction.x) * (_size.x * 0.5f);
            int index = vh.currentVertCount;
            vh.AddVert(indicator.Center + forward, tint, Vector2.zero);
            vh.AddVert(indicator.Center - forward - side, tint, Vector2.zero);
            vh.AddVert(indicator.Center - forward + side, tint, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
        }
    }
}
