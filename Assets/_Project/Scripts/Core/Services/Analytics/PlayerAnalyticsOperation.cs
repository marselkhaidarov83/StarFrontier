using System;

public sealed class PlayerAnalyticsOperation : IDisposable
{
    private readonly IPlayerAnalytics _service;
    private readonly PlayerAnalyticsAction _action;
    private readonly PlayerAnalyticsSnapshot _before;
    private readonly string _contentId;
    private readonly int _quantity;
    private bool _completed;

    public PlayerAnalyticsOperation(IPlayerAnalytics service, PlayerAnalyticsAction action, string contentId, int quantity)
    {
        _service = service; _action = action; _contentId = contentId; _quantity = quantity;
        try { _before = service?.Capture(); } catch { }
    }

    public void Complete(bool success, string reason = "None", int cost = 0)
    {
        if (_completed) return;
        _completed = true;
        try { _service?.Track(_action, success, reason, _before, _contentId, _quantity, cost); } catch { }
    }

    public void Dispose() { if (!_completed) Complete(false, "UnexpectedException"); }
}
