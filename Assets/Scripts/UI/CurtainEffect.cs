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

    [Header("Position (ตำแหน่งบานพับ หน่วย UI ตามแกน Y ของ Anchored Position)")]
    [Tooltip("ตำแหน่งตอนปิดสนิท ปิดไม่มิด = ลดค่าลง (เลื่อนบานพับลงมา) / เลยลงมามากไป = เพิ่มค่า\nคลิกขวาที่หัวคอมโพเนนต์ > Preview: ปิด เพื่อดูตำแหน่งในฉากได้เลย")]
    [SerializeField] private float closedPosY = 0f;

    [Tooltip("ตำแหน่งตอนซ่อน (เปิด) 0 = ใช้ความสูงของบานพับเอง (ค่าเดิม)")]
    [SerializeField] private float hiddenPosYOverride = 0f;

    private float hiddenPosY = 1080f;

    private void Awake()
    {
        if (curtainRect == null) curtainRect = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        if (hiddenPosYOverride != 0f)
        {
            hiddenPosY = hiddenPosYOverride;
        }
        else if (curtainRect != null && curtainRect.rect.height > 0)
        {
            hiddenPosY = curtainRect.rect.height;
        }

        curtainRect.anchoredPosition = new Vector2(0f, hiddenPosY);
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
    }

    public bool IsPlaying { get; private set; }

    public void PlayCurtainTransition(Action onTransitionComplete)
    {
        StartCoroutine(CurtainSequenceRoutine(onTransitionComplete));
    }

    /// <summary>
    /// ปิดบานพับ -> เรียก onClosed (เช่น รีเซ็ตพัซเซิล) ตอนปิดสนิท -> รอจน isReady เป็นจริง (ไม่เกิน maxWait) และพักตาม Pause Duration
    /// -> เปิดบานพับ ผู้เล่นจึงไม่เห็นตอนของถูกสร้างใหม่
    /// </summary>
    public void PlayCloseThenOpen(Action onClosed, Func<bool> isReady = null, float maxWait = 3f)
    {
        if (IsPlaying) return;
        StartCoroutine(CloseThenOpenRoutine(onClosed, isReady, maxWait));
    }

    private IEnumerator CloseThenOpenRoutine(Action onClosed, Func<bool> isReady, float maxWait)
    {
        IsPlaying = true;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        if (AudioUI.Instance != null)
        {
            AudioUI.Instance.PlaySFX(AudioUI.Instance.sfxCurtain);
        }

        yield return StartCoroutine(MoveCurtainY(hiddenPosY, closedPosY, slideDownDuration));

        onClosed?.Invoke();

        float waited = 0f;
        while (isReady != null && !isReady() && waited < maxWait)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(pauseDuration);

        yield return StartCoroutine(MoveCurtainY(closedPosY, hiddenPosY, slideUpDuration));

        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        IsPlaying = false;
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

    // ดูตำแหน่งในโหมดแก้ไข: คลิกขวาที่หัวคอมโพเนนต์ แล้วเลือกเมนูนี้ (กด Ctrl+Z เพื่อย้อน)
    [ContextMenu("Preview: ปิด (Closed)")]
    private void PreviewClosed() => PreviewAt(closedPosY);

    [ContextMenu("Preview: ซ่อน (Hidden)")]
    private void PreviewHidden()
    {
        var rt = curtainRect != null ? curtainRect : GetComponent<RectTransform>();
        PreviewAt(hiddenPosYOverride != 0f ? hiddenPosYOverride : rt.rect.height);
    }

    private void PreviewAt(float y)
    {
        var rt = curtainRect != null ? curtainRect : GetComponent<RectTransform>();
        if (rt == null || Application.isPlaying) return;
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(rt, "Curtain Preview");
#endif
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
    }
}