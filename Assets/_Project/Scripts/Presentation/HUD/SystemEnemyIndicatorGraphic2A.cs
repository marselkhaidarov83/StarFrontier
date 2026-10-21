using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// All markers share one UI mesh; only ship marker hit areas intercept taps.
public sealed class SystemEnemyIndicatorGraphic2A : MaskableGraphic,
    IPointerDownHandler, IPointerClickHandler
{
    private struct Indicator
    {
        public Vector2 Center;
        public Vector2 Direction;
        public Color32 Tint;
        public string RuntimeNpcId;
    }

    private readonly List<Indicator> _indicators = new List<Indicator>();
    private Vector2 _size;
    private string _pressedNpcId;
    private int _pressedPointerId;

    public event Action<string> IndicatorClicked;

    public void BeginFrame(Vector2 size)
    {
        _indicators.Clear();
        _size = size;
        color = Color.white;
    }

    public void AddIndicator(Vector2 center, Vector2 direction, Color tint, string runtimeNpcId = null)
    {
        // Stay below the UI mesh's 16-bit vertex limit.
        if (_indicators.Count >= 21000)
            return;
        _indicators.Add(new Indicator
        {
            Center = center, Direction = direction, Tint = tint, RuntimeNpcId = runtimeNpcId
        });
    }

    public void EndFrame()
    {
        enabled = _indicators.Count > 0;
        SetVerticesDirty();
    }

    public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
    {
        return isActiveAndEnabled && base.Raycast(screenPoint, eventCamera) &&
            FindIndicator(screenPoint, eventCamera) >= 0;
    }

    private int FindIndicator(Vector2 screenPoint, Camera eventCamera)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, eventCamera, out Vector2 local))
            return -1;

        // A small extra touch area helps on mobile without blocking the rest of the map.
        float radius = Mathf.Max(24f, _size.magnitude * 0.5f + 4f);
        float nearestDistance = radius * radius;
        int nearestIndex = -1;
        for (int i = 0; i < _indicators.Count; i++)
        {
            Indicator indicator = _indicators[i];
            if (string.IsNullOrWhiteSpace(indicator.RuntimeNpcId))
                continue;

            float distance = (local - indicator.Center).sqrMagnitude;
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }
        return nearestIndex;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressedNpcId = null;
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        int index = FindIndicator(eventData.position, eventData.pressEventCamera);
        if (index < 0)
            return;

        _pressedNpcId = _indicators[index].RuntimeNpcId;
        _pressedPointerId = eventData.pointerId;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            eventData.pointerId != _pressedPointerId || eventData.dragging ||
            (eventData.position - eventData.pressPosition).sqrMagnitude > 625f)
            return;

        string npcId = _pressedNpcId;
        _pressedNpcId = null;
        if (!string.IsNullOrWhiteSpace(npcId))
            IndicatorClicked?.Invoke(npcId);
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
