using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PotionSystem
{
    /// <summary>Helper สำหรับ 2D Crafting Scene: ตำแหน่งเมาส์ใน World Space + Sprite สำรอง</summary>
    public static class CraftingSprites2D
    {
        private static Sprite square;

        /// <summary>ความสูงของตัวขวดที่มองเห็นเทียบกับกรอบ (ขวดทุกแบบสูงเท่ากัน ยิ่งน้อยยิ่งเล็ก)</summary>
        private const float HeightFactor = 0.55f;
        /// <summary>ความกว้างของตัวขวดที่มองเห็นสูงสุดเทียบกับกรอบ (กันขวดแบนกว้างเกิน)</summary>
        private const float WidthFactor = 0.95f;

        private static readonly System.Collections.Generic.Dictionary<Sprite, Vector2> visibleSizes =
            new System.Collections.Generic.Dictionary<Sprite, Vector2>();

        /// <summary>
        /// ขนาด (หน่วยโลก ที่สเกล 1) ของส่วนที่มองเห็นจริงของสไปรต์ ไม่รวมขอบโปร่งใส
        /// ต้องเปิด Read/Write ที่ Texture ของสไปรต์ ถ้าไม่เปิดจะใช้ขนาดทั้งภาพแทน
        /// </summary>
        private static Vector2 VisibleSize(Sprite sprite)
        {
            if (visibleSizes.TryGetValue(sprite, out var cached)) return cached;

            Vector2 result = sprite.bounds.size;
            var tex = sprite.texture;
            if (tex != null && tex.isReadable)
            {
                Rect r = sprite.rect;
                int w = (int)r.width, h = (int)r.height;
                var px = tex.GetPixels32(0);
                int minX = w, maxX = -1, minY = h, maxY = -1;
                for (int y = 0; y < h; y++)
                {
                    int row = ((int)r.y + y) * tex.width + (int)r.x;
                    for (int x = 0; x < w; x++)
                    {
                        if (px[row + x].a <= 25) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
                if (maxX >= minX && maxY >= minY)
                {
                    result = new Vector2((maxX - minX + 1) / sprite.pixelsPerUnit, (maxY - minY + 1) / sprite.pixelsPerUnit);
                    visibleBottoms[sprite] = (minY - sprite.pivot.y) / sprite.pixelsPerUnit;
                }
            }

            visibleSizes[sprite] = result;
            return result;
        }

        private static readonly System.Collections.Generic.Dictionary<Sprite, float> visibleBottoms =
            new System.Collections.Generic.Dictionary<Sprite, float>();

        /// <summary>ตำแหน่งขอบล่างของส่วนที่มองเห็นจริง เทียบกับจุดหมุนของสไปรต์ (หน่วยโลก ที่สเกล 1) ใช้จัดให้ขวดยืนบนพื้นเมื่อปรับสเกล</summary>
        public static float VisibleBottom(Sprite sprite)
        {
            VisibleSize(sprite); // คำนวณและเก็บค่าไว้ก่อน
            return visibleBottoms.TryGetValue(sprite, out var b) ? b : sprite.bounds.min.y;
        }

        public static Vector3 MouseWorldPosition()
        {
            Vector3 screen;
#if ENABLE_INPUT_SYSTEM
            screen = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
            screen = Input.mousePosition;
#endif
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;
            screen.z = Mathf.Abs(cam.transform.position.z);
            var world = cam.ScreenToWorldPoint(screen);
            world.z = 0f;
            return world;
        }

        /// <summary>Sprite สี่เหลี่ยมขาว 1x1 unit ไว้ใช้เมื่อไอเทมไม่มี Icon</summary>
        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var tex = new Texture2D(4, 4);
                    var pixels = new Color[16];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                    tex.SetPixels(pixels);
                    tex.Apply();
                    tex.filterMode = FilterMode.Point;
                    square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                    square.name = "FallbackSquare";
                }
                return square;
            }
        }

        public static void Apply(SpriteRenderer sr, ItemData item, Color fallbackColor, float fallbackSize)
        {
            if (item != null && item.Icon != null)
            {
                sr.sprite = item.Icon;
                sr.color = Color.white;
                sr.transform.localScale = Vector3.one;
            }
            else
            {
                sr.sprite = Square;
                sr.color = fallbackColor;
                sr.transform.localScale = Vector3.one * fallbackSize;
            }
        }

        /// <summary>
        /// ใส่รูปยาให้พอดีกับกรอบสี่เหลี่ยมจัตุรัสด้าน box (หน่วยโลก) คงสัดส่วนภาพ ทุกขวดจึงกินพื้นที่ใกล้เคียงกัน
        /// ใช้กับชั้นวาง ช่องเตรียมยิง และขวดที่ลาก เพื่อให้ขนาดยาเป็นมาตรฐานเดียวกัน
        /// </summary>
        public static void FitPotion(SpriteRenderer sr, ItemData item, Color fallbackColor, float box)
        {
            if (item != null && item.Icon != null)
            {
                sr.sprite = item.Icon;
                sr.color = Color.white;
                // วัดจากส่วนที่มองเห็นจริง (ไม่รวมขอบโปร่งใส) ขวดทุกแบบจึงสูงเท่ากัน แต่ไม่ให้กว้างเกินสำหรับขวดแบน
                Vector2 size = VisibleSize(item.Icon);
                float scale = size.y > 0.0001f ? HeightFactor * box / size.y : 1f;
                if (size.x > 0.0001f) scale = Mathf.Min(scale, WidthFactor * box / size.x);
                sr.transform.localScale = Vector3.one * scale;
            }
            else
            {
                sr.sprite = Square;
                sr.color = fallbackColor;
                sr.transform.localScale = Vector3.one * box * 0.6f;
            }
        }

        public static Color ColorFor(PotionData p)
        {
            if (p == null) return Color.white;
            switch (p.Kind)
            {
                case PotionKind.Red: return Color.red;
                case PotionKind.Blue: return Color.blue;
                case PotionKind.Green: return Color.green;
                case PotionKind.Skyward: return Color.cyan;
                case PotionKind.Havoc: return new Color(0.6f, 0.8f, 0.2f);
                case PotionKind.Surge: return new Color(0.9f, 0.6f, 0.3f);
                default: return Color.white;
            }
        }
    }
}
