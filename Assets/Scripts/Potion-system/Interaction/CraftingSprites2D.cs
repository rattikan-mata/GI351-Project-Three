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
