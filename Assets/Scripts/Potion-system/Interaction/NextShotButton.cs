using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PotionSystem
{
    /// <summary>
    /// ปุ่ม ▶ ยิงปืนตัวถัดไป (ใช้หลังดึงคันโยกเริ่มชุดยิงแล้ว)
    /// - อยู่กลางชุดยิง: ปุ่มสว่าง มีเลขยาที่เหลือ กดแล้วยิงขวดถัดไป
    /// - ไม่อยู่กลางชุดยิง / ปืนยังไม่ว่าง: ปุ่มจาง กดแล้วสั่นพร้อมเสียงพลาด
    /// ผูกคลิกกับ PotionStagingManager.FireNextPotion ให้เองตอนเริ่ม ไม่ต้องตั้ง onClick
    /// </summary>
    [RequireComponent(typeof(Button), typeof(Image))]
    public class NextShotButton : MonoBehaviour
    {
        [SerializeField] private PotionStagingManager manager;
        [SerializeField] private Image image;
        [Tooltip("ติ๊กไว้ = แสดงเลขจำนวนยาที่เหลือที่มุมปุ่ม (ปิดไว้เป็นค่าเริ่มต้น)")]
        [SerializeField] private bool showCount = false;
        [Tooltip("(ไม่บังคับ) ข้อความจำนวนยาที่เหลือ ใช้เมื่อเปิด Show Count ว่างไว้ = สร้างที่มุมปุ่มให้เอง")]
        [SerializeField] private TMP_Text countLabel;

        [Header("Look")]
        [SerializeField] private Color armedColor = Color.white;
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private float shakeDistance = 4f;
        [SerializeField] private float shakeSeconds = 0.25f;

        private RectTransform rect;
        private Vector2 restPosition;
        private Coroutine shake;

        private void Awake()
        {
            rect = (RectTransform)transform;
            if (image == null) image = GetComponent<Image>();
            if (manager == null) manager = FindAnyObjectByType<PotionStagingManager>();
            GetComponent<Button>().onClick.AddListener(OnClick);

            if (showCount && countLabel == null)
            {
                var go = new GameObject("Count", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
                r.pivot = new Vector2(1f, 0f);
                r.anchoredPosition = new Vector2(2f, -2f);
                r.sizeDelta = new Vector2(40f, 40f);
                var label = go.AddComponent<TextMeshProUGUI>();
                label.fontSize = 30f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.BottomRight;
                label.color = Color.white;
                label.outlineWidth = 0.25f;
                label.outlineColor = Color.black;
                label.raycastTarget = false;
                countLabel = label;
            }
        }

        private void OnEnable()
        {
            restPosition = rect.anchoredPosition;
            if (manager != null) manager.NextRejected += OnRejected;
        }

        private void OnDisable()
        {
            if (manager != null) manager.NextRejected -= OnRejected;
            shake = null;
            rect.anchoredPosition = restPosition;
        }

        private void Update()
        {
            bool armed = manager != null && manager.IsBatchActive;
            image.color = armed ? armedColor : idleColor;
            if (countLabel != null) countLabel.text = armed ? manager.RemainingPotions.ToString() : string.Empty;
        }

        private void OnClick()
        {
            if (manager != null) manager.FireNextPotion();
        }

        private void OnRejected()
        {
            if (shake != null) StopCoroutine(shake);
            rect.anchoredPosition = restPosition;
            shake = StartCoroutine(ShakeRoutine());
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
            shake = null;
        }
    }
}
