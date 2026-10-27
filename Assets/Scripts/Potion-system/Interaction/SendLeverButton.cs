using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PotionSystem
{
    /// <summary>
    /// ปุ่มส่งยาแบบคันโยก (ใส่คู่กับ Button ที่เรียก PotionStagingManager.SendPotionsToDropper)
    /// - ส่งได้: โยกลง (เสียงโยกมาจาก SendPotion ของ PotionStagingManager) ค้างไว้จนส่งเสร็จ แล้วดีดกลับขึ้น (เสียง LeverReturn)
    /// - ส่งไม่ได้ (ยาไม่ครบช่อง): คันโยกสั่นเบาๆ ไม่โยกลง (เสียงพลาดมาจาก PotionStagingManager)
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SendLeverButton : MonoBehaviour
    {
        [SerializeField] private PotionStagingManager manager;
        [SerializeField] private Image image;

        [Header("Sprites")]
        [SerializeField] private Sprite upSprite;
        [SerializeField] private Sprite downSprite;

        [Header("Motion")]
        [Tooltip("ค้างตำแหน่งลงอย่างน้อยกี่วินาที")]
        [SerializeField, Min(0f)] private float minDownSeconds = 0.45f;
        [Tooltip("ติ๊กไว้ = ถ้ากำลังส่งยาตามลำดับสวิตช์อยู่ คันโยกจะค้างลงจนส่งเสร็จ ไม่ติ๊ก = ดีดกลับหลังครบเวลาขั้นต่ำเลย")]
        [SerializeField] private bool holdWhileBusy = true;
        [SerializeField] private float shakeDistance = 5f;
        [SerializeField] private float shakeSeconds = 0.25f;

        private RectTransform rect;
        private Vector2 restPosition;
        private Coroutine motion;

        private void Awake()
        {
            rect = (RectTransform)transform;
            if (image == null) image = GetComponent<Image>();
            if (manager == null) manager = FindAnyObjectByType<PotionStagingManager>();

            // ไม่มีการตั้ง onClick ในซีน (เช่นอยู่ใน prefab ชุดควบคุม) ให้ผูกส่งยาให้เอง
            var button = GetComponent<Button>();
            if (button != null && manager != null && button.onClick.GetPersistentEventCount() == 0)
                button.onClick.AddListener(manager.SendPotionsToDropper);
        }

        private void OnEnable()
        {
            restPosition = rect.anchoredPosition;
            ShowUp();
            if (manager == null) return;
            manager.SendAccepted += OnAccepted;
            manager.SendRejected += OnRejected;
            manager.BatchEnded += OnBatchEnded;
        }

        private void OnDisable()
        {
            if (manager != null)
            {
                manager.SendAccepted -= OnAccepted;
                manager.SendRejected -= OnRejected;
                manager.BatchEnded -= OnBatchEnded;
            }
            motion = null;
            rect.anchoredPosition = restPosition;
            ShowUp();
        }

        private void ShowUp()
        {
            if (image != null && upSprite != null) image.sprite = upSprite;
        }

        private void Play(IEnumerator routine)
        {
            if (motion != null) StopCoroutine(motion);
            rect.anchoredPosition = restPosition;
            motion = StartCoroutine(routine);
        }

        private void OnAccepted()
        {
            // โหมดทีละขวด: ค้างลงตลอดชุดยิง (ปุ่ม ▶ ยิงต่อ) จนชุดจบ ถ้าชุดมีขวดเดียวก็ดีดกลับตามเวลา
            if (manager.StepByStep && manager.IsBatchActive)
            {
                if (motion != null) StopCoroutine(motion);
                motion = null;
                rect.anchoredPosition = restPosition;
                if (downSprite != null) image.sprite = downSprite;
                return;
            }
            Play(PullRoutine());
        }

        private void OnRejected() => Play(ShakeRoutine());

        private void OnBatchEnded()
        {
            if (motion != null) StopCoroutine(motion);
            motion = null;
            rect.anchoredPosition = restPosition;
            ShowUp();
            CraftingSfx.Play(CraftingSfx.LeverReturn);
        }

        private IEnumerator PullRoutine()
        {
            if (downSprite != null) image.sprite = downSprite;
            yield return new WaitForSeconds(minDownSeconds);
            while (holdWhileBusy && manager != null && manager.IsBusy) yield return null;

            ShowUp();
            CraftingSfx.Play(CraftingSfx.LeverReturn);
            motion = null;
        }

        private IEnumerator ShakeRoutine()
        {
            float t = 0f;
            while (t < shakeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float fade = 1f - t / shakeSeconds;
                rect.anchoredPosition = restPosition + new Vector2(Mathf.Sin(t * 70f) * shakeDistance * fade, 0f);
                yield return null;
            }
            rect.anchoredPosition = restPosition;
            motion = null;
        }
    }
}
