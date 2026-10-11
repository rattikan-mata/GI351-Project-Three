using UnityEngine;

namespace PotionSystem
{
    /// <summary>ข้อมูลพื้นฐานของไอเทมทุกชนิด (ใช้เป็น base ของ RawMaterial / Potion)</summary>
    public abstract class ItemData : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [TextArea, SerializeField] private string description;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public string Description => description;
    }
}
