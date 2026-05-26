using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Fader : MonoBehaviour
{
    public static Fader instance;

    [SerializeField] private Image fadeImage;
    public bool isFading = false;
    private Coroutine fadeCoroutine;

    private void Start()
    {
        if(instance == null)
        {
            instance = this;
            StartFade(1f, 0f, 1f);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartFade(float startAlpha, float endAlpha, float duration)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(Fade(startAlpha, endAlpha, duration));
    }

    private IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        isFading = true;
        float timer = 0;
        while (timer < duration)
        {
            fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, Mathf.Lerp(startAlpha, endAlpha, timer / duration));
            timer += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, endAlpha);

        isFading = false;
    }
}
