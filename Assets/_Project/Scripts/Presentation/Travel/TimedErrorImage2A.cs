using UnityEngine;

/// <summary>Hides the visible error even if the screen that requested it is disabled.</summary>
public sealed class TimedErrorImage2A : MonoBehaviour
{
    private float _hideAt;
    private bool _visible;

    public void ShowFor(float duration)
    {
        _hideAt = Time.unscaledTime + Mathf.Max(0.01f, duration);
        _visible = true;
    }

    private void Update()
    {
        if (!_visible || Time.unscaledTime < _hideAt) return;
        _visible = false;
        gameObject.SetActive(false);
    }
}
