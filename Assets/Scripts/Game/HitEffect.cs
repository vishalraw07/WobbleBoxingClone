using UnityEngine;
using System.Collections;

public class HitEffect : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float displayDuration = 0.2f;
    private float fadeDuration = 0.1f;
    private float pulseSpeed = 8f;
    private float pulseAmplitude = 0.3f;
    private float rotationSpeed = 90f;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        StartCoroutine(AnimateAndFade());
    }

    IEnumerator AnimateAndFade()
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < displayDuration)
        {
            elapsed += Time.deltaTime;
            float scaleFactor = 1f + pulseAmplitude * Mathf.Sin(elapsed * pulseSpeed);
            transform.localScale = startScale * scaleFactor;
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
            yield return null;
        }

        elapsed = 0f;
        Color startColor = spriteRenderer.color;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeDuration);
            spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            float scaleFactor = 1f - 0.1f * (elapsed / fadeDuration);
            transform.localScale = startScale * scaleFactor;
            yield return null;
        }

        Debug.Log("Destroying hit effect");
        Destroy(gameObject);
    }
}