using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// วงโหลด 2 ชั้น (สร้างด้วยโค้ด ไม่ต้องใช้รูป ไม่หมุน)
    /// - ชั้นล่าง: วงแหวนเต็มวง (ราง)
    /// - ชั้นบน: วงแหวนที่ค่อย ๆ เติมสีตามเข็มนาฬิกาจากด้านบน ตาม SetProgress(0..1)
    /// ใช้กับถาดตอนคูลดาวน์ และหม้อตอนปรุง
    /// </summary>
    public class RadialFillRing : MonoBehaviour
    {
        private const int Size = 64;
        private const int Steps = 120; // อัปเดตภาพไม่เกิน 120 ครั้งต่อรอบ

        // ข้อมูลพิกเซลที่ใช้ร่วมกัน: ความทึบของวงแหวน และตำแหน่งมุม (0 = ด้านบน, 1 = ครบรอบ ตามเข็มนาฬิกา)
        private static float[] ringAlpha;
        private static float[] angleFrac;
        private static Sprite trackSprite;

        private SpriteRenderer track;
        private SpriteRenderer fill;
        private Texture2D fillTexture;
        private Sprite fillSprite;
        private Color32[] fillPixels;
        private int lastStep = -1;

        public static RadialFillRing Create(Transform parent, Vector3 localPosition, float diameter,
                                            int sortingOrder, Color trackColor, Color fillColor)
        {
            EnsureData();

            var go = new GameObject("FillRing");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * diameter;

            var ring = go.AddComponent<RadialFillRing>();
            ring.Build(sortingOrder, trackColor, fillColor);
            go.SetActive(false);
            return ring;
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetSortingOrder(int order)
        {
            if (track != null) track.sortingOrder = order;
            if (fill != null) fill.sortingOrder = order + 1;
        }

        /// <summary>0 = ว่างเปล่า, 1 = เต็มวง</summary>
        public void SetProgress(float progress)
        {
            int step = Mathf.RoundToInt(Mathf.Clamp01(progress) * Steps);
            if (step == lastStep) return;
            lastStep = step;

            float limit = step / (float)Steps;
            for (int i = 0; i < fillPixels.Length; i++)
            {
                float a = ringAlpha[i];
                bool filled = a > 0f && (step >= Steps || angleFrac[i] <= limit) && step > 0;
                fillPixels[i] = new Color32(255, 255, 255, filled ? (byte)(a * 255f) : (byte)0);
            }
            fillTexture.SetPixels32(fillPixels);
            fillTexture.Apply(false);
        }

        private void Build(int sortingOrder, Color trackColor, Color fillColor)
        {
            track = gameObject.AddComponent<SpriteRenderer>();
            track.sprite = TrackSprite;
            track.color = trackColor;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(transform, false);
            fill = fillGo.AddComponent<SpriteRenderer>();

            fillTexture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            fillPixels = new Color32[Size * Size];
            fillSprite = Sprite.Create(fillTexture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            fill.sprite = fillSprite;
            fill.color = fillColor;

            SetSortingOrder(sortingOrder);
            SetProgress(0f);
        }

        private void OnDestroy()
        {
            if (fillSprite != null) Destroy(fillSprite);
            if (fillTexture != null) Destroy(fillTexture);
        }

        // ---------- ข้อมูลวงแหวนที่ใช้ร่วมกัน ----------

        private static void EnsureData()
        {
            if (ringAlpha != null) return;

            ringAlpha = new float[Size * Size];
            angleFrac = new float[Size * Size];

            float c = (Size - 1) * 0.5f;
            float outer = Size * 0.5f - 1f;
            float inner = outer * 0.72f;
            const float twoPi = Mathf.PI * 2f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    // ขอบนุ่มเล็กน้อย
                    float a = Mathf.Clamp01(outer + 0.5f - d) * Mathf.Clamp01(d - inner + 0.5f);
                    ringAlpha[i] = a;

                    float ang = Mathf.Atan2(dx, dy); // 0 = ด้านบน เพิ่มตามเข็มนาฬิกา
                    if (ang < 0f) ang += twoPi;
                    angleFrac[i] = ang / twoPi;
                }
            }
        }

        private static Sprite TrackSprite
        {
            get
            {
                if (trackSprite != null) return trackSprite;

                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var px = new Color32[Size * Size];
                for (int i = 0; i < px.Length; i++)
                    px[i] = new Color32(255, 255, 255, (byte)(ringAlpha[i] * 255f));
                tex.SetPixels32(px);
                tex.Apply();

                trackSprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
                trackSprite.name = "RingTrack";
                return trackSprite;
            }
        }
    }
}
