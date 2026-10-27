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
            }
            brewing = false;
        }

        private void RefreshLabel()
        {
            if (brewsLabel != null && shelf != null) brewsLabel.text = shelf.BrewsLeft + "/" + shelf.BrewsPerStage;
        }
    }
}
