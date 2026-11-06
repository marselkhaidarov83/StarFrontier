using System.Collections;
using UnityEngine;

public sealed class ArrivalFxView2 : CustomMonoBehaviour
{
    [SerializeField] private SpriteRenderer image;

    [Header("Animation")]
    [SerializeField] private float duration = 0.45f;
    [SerializeField] private float startScale = 1f;
    [SerializeField] private float endScale = 20f;

    private Coroutine _animationCoroutine;

    private void Reset()
    {
        image = GetComponent<SpriteRenderer>();
    }

    public void Play(Vector3 position)
    {
        StopAnimation();
        transform.position = position;
        ResetVisual();
        gameObject.SetActive(true);

        // SetActive on a child cannot activate a hidden system-map parent.
        // Do not reopen the map just to display a transient arrival effect.
        if (!isActiveAndEnabled)
        {
            gameObject.SetActive(false);
            AppLog.Info(DebugLogChannel.Travel, typeof(ArrivalFxView2),
                "Arrival FX skipped | Object=" + name + " | Reason=InactiveHierarchyOrComponent");
            return;
        }
        if (duration <= 0f)
        {
            gameObject.SetActive(false);
            return;
        }

        _animationCoroutine = StartCoroutine(PlayRoutine());
    }

    private void OnDisable()
    {
        StopAnimation();
        ResetVisual();
        // A parent may hide the map during landing. Keep the stopped flash hidden
        // when the map is shown again, until the next explicit Play request.
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void StopAnimation()
    {
        if (_animationCoroutine == null) return;
        StopCoroutine(_animationCoroutine);
        _animationCoroutine = null;
    }

    private void ResetVisual()
    {
        transform.localScale = Vector3.one;
        if (image != null)
        {
            Color color = image.color;
            color.a = 1f;
            image.color = color;
        }
    }

    private IEnumerator PlayRoutine()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(startScale, endScale, t);
            transform.localScale = new Vector3(scale, scale, 1f);
            if (image != null)
            {
                Color color = image.color;
                color.a = 1f - t;
                image.color = color;
            }
            yield return null;
        }

        // Clear the handle before deactivation invokes OnDisable.
        _animationCoroutine = null;
        ResetVisual();
        gameObject.SetActive(false);
    }
}
