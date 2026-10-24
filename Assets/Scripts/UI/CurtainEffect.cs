using System.Collections;
using UnityEngine;

public class CurtainEffect : MonoBehaviour
{
    [SerializeField] private Animator curtainAnimator;
    [SerializeField] private CanvasGroup canvasGroup;

    [SerializeField] private string triggerCloseName = "Close";
    [SerializeField] private string triggerOpenName = "Open";

    private void Awake()
    {
        if (curtainAnimator == null) curtainAnimator = GetComponent<Animator>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        PlayOpen();
    }

    public void PlayOpen()
    {
        if (curtainAnimator != null) curtainAnimator.SetTrigger(triggerOpenName);
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
    }

    public void PlayClose(System.Action onClosedCallback, float delay = 0.8f)
    {
        StartCoroutine(CloseRoutine(onClosedCallback, delay));
    }

    private IEnumerator CloseRoutine(System.Action onClosedCallback, float delay)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        if (curtainAnimator != null) curtainAnimator.SetTrigger(triggerCloseName);

        yield return new WaitForSecondsRealtime(delay);
        onClosedCallback?.Invoke();
    }
}