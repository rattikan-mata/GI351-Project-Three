using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class CurtainEffect : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform curtainRect;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Movement Settings")]
    [SerializeField] private float slideDownDuration = 0.8f;
    [SerializeField] private float pauseDuration = 0.2f;
    [SerializeField] private float slideUpDuration = 0.8f;

    private float hiddenPosY = 1080f;
    private float closedPosY = 0f;

    private void Awake()
    {
        if (curtainRect == null) curtainRect = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        if (curtainRect != null && curtainRect.rect.height > 0)
        {
            hiddenPosY = curtainRect.rect.height;
        }

        curtainRect.anchoredPosition = new Vector2(0f, hiddenPosY);
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
    }

    public void PlayCurtainTransition(Action onTransitionComplete)
    {
        StartCoroutine(CurtainSequenceRoutine(onTransitionComplete));
    }

    private IEnumerator CurtainSequenceRoutine(Action onTransitionComplete)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        if (AudioUI.Instance != null)
        {
            AudioUI.Instance.PlaySFX(AudioUI.Instance.sfxCurtain);
        }

        yield return StartCoroutine(MoveCurtainY(hiddenPosY, closedPosY, slideDownDuration));

        yield return new WaitForSecondsRealtime(pauseDuration);

        yield return StartCoroutine(MoveCurtainY(closedPosY, hiddenPosY, slideUpDuration));

        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

        onTransitionComplete?.Invoke();
    }

    private IEnumerator MoveCurtainY(float fromY, float toY, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            curtainRect.anchoredPosition = new Vector2(curtainRect.anchoredPosition.x, Mathf.Lerp(fromY, toY, smoothT));
            yield return null;
        }

        curtainRect.anchoredPosition = new Vector2(curtainRect.anchoredPosition.x, toY);
    }
}