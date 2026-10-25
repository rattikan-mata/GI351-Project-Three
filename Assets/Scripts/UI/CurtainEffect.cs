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
    [Tooltip("ความช้าตอนเลื่อนลงมาปิด (วินาที)")]
    [SerializeField] private float slideDownDuration = 1.0f;

    [Tooltip("ระยะเวลาที่ม่านค้างปิดสนิท (วินาที)")]
    [SerializeField] private float pauseDuration = 0.3f;

    [Tooltip("ความช้าตอนเลื่อนขึ้นเปิด (วินาที)")]
    [SerializeField] private float slideUpDuration = 1.0f;

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

    public void PlayFullResetSequence(Action onCompleteCallback)
    {
        StartCoroutine(ResetSequenceRoutine(onCompleteCallback));
    }

    private IEnumerator ResetSequenceRoutine(Action onCompleteCallback)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        yield return StartCoroutine(SlideRoutine(hiddenPosY, closedPosY, slideDownDuration));

        yield return new WaitForSecondsRealtime(pauseDuration);

        yield return StartCoroutine(SlideRoutine(closedPosY, hiddenPosY, slideUpDuration));

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        onCompleteCallback?.Invoke();
    }

    private IEnumerator SlideRoutine(float fromY, float toY, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float currentY = Mathf.Lerp(fromY, toY, smoothT);

            curtainRect.anchoredPosition = new Vector2(curtainRect.anchoredPosition.x, currentY);
            yield return null;
        }

        curtainRect.anchoredPosition = new Vector2(curtainRect.anchoredPosition.x, toY);
    }
}