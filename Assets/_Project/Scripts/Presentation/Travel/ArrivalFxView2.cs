using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
        transform.position = position;

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