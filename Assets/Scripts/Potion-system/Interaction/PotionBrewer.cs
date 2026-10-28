using System.Collections;
using TMPro;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// น้องนักปรุงยา: คลิกที่ตัวน้อง -> น้องคนหม้อ (Brew Seconds) -> ได้ยาสุ่ม 1 ขวดเข้าชั้นวาง
    /// ปรุงได้ตามจำนวนครั้งที่ชั้นวางกำหนด (Brews Per Stage) ระหว่างคนอยู่คลิกซ้ำไม่ได้
    /// ภาพ: ถ้ามี Animator จะยิง Trigger ชื่อ Stir Trigger ให้เล่นท่าคนยา ถ้าไม่มี จะโยกตัวไปมาแทน (ใช้ระหว่างยังไม่มีแอนิเมชันจริง)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PotionBrewer : MonoBehaviour
    {
        [SerializeField] private PotionShelf shelf;
        [Tooltip("เวลาคนยาจนได้ขวด (วินาที)")]
        [SerializeField, Min(0f)] private float brewSeconds = 1f;

        [Header("Visual")]
        [Tooltip("(ไม่บังคับ) Animator ของน้อง ว่างไว้ = หาในตัวและลูก")]
        [SerializeField] private Animator animator;
        [SerializeField] private string stirTrigger = "Stir";
        [Tooltip("ตัวที่โยกตอนคน เมื่อไม่มี Animator (ว่างไว้ = ตัวเอง)")]
        [SerializeField] private Transform wobbleTarget;
        [SerializeField] private float wobbleAngle = 8f;
        [SerializeField] private float wobbleSpeed = 18f;
        [Tooltip("(ไม่บังคับ) ข้อความแสดงจำนวนครั้งที่ปรุงได้อีก ว่างไว้ = สร้างตัวหนังสือเหนือหัวน้องให้เอง")]
        [SerializeField] private TMP_Text brewsLabel;
        [SerializeField] private Vector2 autoLabelOffset = new Vector2(0f, 1.15f);
        [SerializeField] private int autoLabelSortingOrder = 7;

        [Header("Label Style (ตัวเลขแกะสลักบนหม้อ)")]
        [Tooltip("(ไม่บังคับ) ฟอนต์ของตัวเลข ว่างไว้ = ฟอนต์ตั้งต้นของ TextMesh Pro")]
        [SerializeField] private TMP_FontAsset labelFont;
        [Tooltip("ติ๊กไว้ = ตัวเลขดูเหมือนแกะลงในหม้อ (สีกลืนกับหม้อ ขอบล่างมีไฮไลต์) ไม่ติ๊ก = ตัวขาวขอบดำแบบเดิม")]
        [SerializeField] private bool engravedLabel = true;
        [SerializeField] private Color engravedFaceColor = new Color32(176, 156, 206, 235);
        [SerializeField] private Color engravedOutlineColor = new Color32(22, 12, 30, 255);
        [Tooltip("แสงที่ขอบล่างของรอยแกะ")]
        [SerializeField] private Color engravedHighlightColor = new Color32(255, 235, 255, 90);
        [Tooltip("ความโค้งตามทรงหม้อ: ขอบตัวเลขยกขึ้นเท่านี้ (0 = ตรง)")]
        [SerializeField] private float engravedBend = 0.07f;
        [Tooltip("รัศมีทรงหม้อ ตัวเลขที่ขอบจะถูกบีบตามผิวโค้ง (0 = ไม่บีบ)")]
        [SerializeField, Min(0f)] private float engravedCylinderRadius = 2.4f;

        private bool brewing;

        public bool IsBrewing => brewing;

        private void Awake()
        {
            if (shelf == null) shelf = FindAnyObjectByType<PotionShelf>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (wobbleTarget == null) wobbleTarget = transform;

            if (brewsLabel == null)
            {
                var go = new GameObject("BrewsLeft");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = autoLabelOffset;
                var label = go.AddComponent<TextMeshPro>();
                if (labelFont != null) label.font = labelFont;
                label.fontSize = 4f;
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
                label.sortingOrder = autoLabelSortingOrder;
                if (engravedLabel) ApplyEngravedStyle(label);
                else
                {
                    label.color = Color.white;
                    label.outlineWidth = 0.2f;
                    label.outlineColor = Color.black;
                }
                // ตัวเลขติดกับหม้อ ไม่โยกตามตัวน้องตอนคนยา
                if (transform.parent != null) go.transform.SetParent(transform.parent, true);
                brewsLabel = label;
            }
        }

        /// <summary>ตัวเลขแบบแกะสลัก: สีกลืนกับหม้อ ขอบเข้ม และไฮไลต์บางๆ ที่ขอบล่างเหมือนรอยบาก</summary>
        private void ApplyEngravedStyle(TextMeshPro label)
        {
            label.fontStyle = FontStyles.Bold;
            label.color = engravedFaceColor;
            label.outlineWidth = 0.18f;
            label.outlineColor = engravedOutlineColor;

            Material mat = label.fontMaterial; // สำเนาเฉพาะตัวนี้
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, engravedHighlightColor);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.7f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);

            // โค้งตามทรงหม้อ
            var curve = label.gameObject.AddComponent<TextCurve>();
            curve.Bend = engravedBend;
            curve.CylinderRadius = engravedCylinderRadius;
        }

        private void OnEnable()
        {
            if (shelf != null) shelf.OnChanged += RefreshLabel;
            RefreshLabel();
        }

        private void OnDisable()
        {
            if (shelf != null) shelf.OnChanged -= RefreshLabel;
            brewing = false;
        }

        private void OnMouseDown()
        {
            if (brewing || shelf == null) return;
            if (!shelf.TryConsumeBrew())
            {
                CraftingSfx.Play(CraftingSfx.PotionPlaceFail); // ปรุงครบแล้ว ต้องรีเซ็ตก่อน
                return;
            }
            StartCoroutine(BrewRoutine());
        }

        private IEnumerator BrewRoutine()
        {
            brewing = true;
            int version = shelf.ResetVersion;
            CraftingSfx.Play(CraftingSfx.PotBrew);
            if (animator != null) animator.SetTrigger(stirTrigger);

            Quaternion rest = wobbleTarget.localRotation;
            float t = 0f;
            while (t < brewSeconds)
            {
                t += Time.deltaTime;
                if (animator == null)
                    wobbleTarget.localRotation = rest * Quaternion.Euler(0f, 0f, Mathf.Sin(t * wobbleSpeed) * wobbleAngle);
                yield return null;
            }
            wobbleTarget.localRotation = rest;

            // รีเซ็ตพัซเซิลระหว่างคนอยู่ = ยกเลิกขวดนี้ (ไม่ให้ยาของรอบก่อนโผล่บนชั้นใหม่)
            if (shelf.ResetVersion != version)
            {
                brewing = false;
                yield break;
            }

            var potion = shelf.RandomPotion();
            if (potion != null && shelf.Add(potion))
            {
                bool isCombo = potion.Kind == PotionKind.Skyward || potion.Kind == PotionKind.Havoc || potion.Kind == PotionKind.Surge;
                CraftingSfx.Play(isCombo ? CraftingSfx.PotionCraftedCombo : CraftingSfx.PotionCrafted);
                if (revealPotion) StartCoroutine(RevealRoutine(potion, isCombo));
            }
            brewing = false;
        }

        [Header("Reveal (โชว์ยาที่ได้)")]
        [Tooltip("ติ๊กไว้ = ปรุงเสร็จแล้วขวดยาที่ได้เด้งขึ้นเหนือหม้อพร้อมชื่อ แล้วลอยไปที่ชั้นวาง")]
        [SerializeField] private bool revealPotion = true;
        [SerializeField, Min(0.1f)] private float revealBox = 1.3f;
        [Tooltip("ติ๊กไว้ = โชว์ชื่อยาใต้ภาพด้วย (ปกติโชว์แค่ภาพ)")]
        [SerializeField] private bool revealShowName = false;
        [SerializeField] private Vector2 revealOffset = new Vector2(0f, 0.3f);
        [SerializeField, Min(0f)] private float revealHoldSeconds = 0.9f;
        [SerializeField, Min(0.05f)] private float revealFlySeconds = 0.45f;
        [SerializeField] private int revealSortingOrder = 120;

        private IEnumerator RevealRoutine(PotionData potion, bool isCombo)
        {
            var root = new GameObject("PotionReveal");
            Vector3 start = transform.TransformPoint(revealOffset);
            start.z = 0f;
            root.transform.position = start;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(root.transform, false);
            var sr = iconGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = revealSortingOrder;
            CraftingSprites2D.FitPotion(sr, potion, CraftingSprites2D.ColorFor(potion), revealBox);
            Vector3 iconScale = iconGo.transform.localScale;

            TextMeshPro label = null;
            if (revealShowName)
            {
            var labelGo = new GameObject("Name");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, -0.7f, 0f);
            label = labelGo.AddComponent<TextMeshPro>();
            if (labelFont != null) label.font = labelFont;
            label.text = potion.DisplayName;
            label.fontSize = 2.6f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = isCombo ? new Color(1f, 0.92f, 0.45f) : Color.white;
            label.outlineWidth = 0.25f;
            label.outlineColor = new Color32(40, 20, 60, 255);
            label.rectTransform.sizeDelta = new Vector2(5f, 0.8f);
            label.sortingOrder = revealSortingOrder + 1;
            }

            // 1) เด้งขึ้น (pop)
            float t = 0f, popTime = 0.28f;
            while (t < popTime)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / popTime);
                float s = k < 0.7f ? Mathf.Lerp(0f, 1.25f, k / 0.7f) : Mathf.Lerp(1.25f, 1f, (k - 0.7f) / 0.3f);
                root.transform.localScale = Vector3.one * s;
                yield return null;
            }
            root.transform.localScale = Vector3.one;

            // 2) ค้างให้เห็นชัดๆ
            float hold = 0f;
            while (hold < revealHoldSeconds) { hold += Time.unscaledDeltaTime; yield return null; }

            // 3) จางหายไปตรงนั้น (ลอยขึ้นนิดหน่อย)
            float t2 = 0f;
            while (t2 < revealFlySeconds)
            {
                t2 += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t2 / revealFlySeconds);
                root.transform.position = start + Vector3.up * (0.25f * k);
                var c = sr.color; c.a = 1f - k; sr.color = c;
                if (label != null) label.alpha = 1f - k;
                yield return null;
            }
            Destroy(root);
        }

        private void RefreshLabel()
        {
            if (brewsLabel != null && shelf != null) brewsLabel.text = shelf.BrewsLeft + "/" + shelf.BrewsPerStage;
        }
    }
}
