using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class ArrivalFxView : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image image;

    [Header("Animation")]
    [SerializeField] private float duration = 0.45f;
    [SerializeField] private float startScale = 0.4f;
    [SerializeField] private float endScale = 1.4f;

    private Coroutine _animationCoroutine;

    private void Reset()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
    }

    public void Play(Vector2 position)
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        rectTransform.anchoredPosition = position;

        gameObject.SetActive(true);

        if (_animationCoroutine != null)
            StopCoroutine(_animationCoroutine);

        _animationCoroutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(startScale, endScale, t);
            float alpha = 1f - t;

            transform.localScale = new Vector3(scale, scale, 1f);

            if (image != null)
            {
                Color color = image.color;
                color.a = alpha;
                image.color = color;
            }

            yield return null;
        }

        gameObject.SetActive(false);
        transform.localScale = Vector3.one;

        if (image != null)
        {
            Color color = image.color;
            color.a = 1f;
            image.color = color;
        }

        _animationCoroutine = null;
    }
}