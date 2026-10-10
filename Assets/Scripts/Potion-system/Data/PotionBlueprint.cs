using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// สูตรผสมยา: "Use Material" ต้องมีวัตถุดิบ 3 ชนิด (Monster Parts, Ore, Herb อย่างละ 1 ชิ้น)
    /// </summary>
    [CreateAssetMenu(fileName = "NewPotionBlueprint", menuName = "Potion System/Potion Blueprint", order = 2)]
    public class PotionBlueprint : ScriptableObject
    {
        public const int RequiredMaterialCount = 3;

        [Header("Use Material (Monster Parts + Ore + Herb อย่างละ 1)")]
        [SerializeField] private List<RawMaterialData> useMaterials = new List<RawMaterialData>();

        [Header("Result")]
        [SerializeField] private PotionData resultPotion;

        public IReadOnlyList<RawMaterialData> UseMaterials => useMaterials;
        public PotionData ResultPotion => resultPotion;

        /// <summary>ของในหม้อตรงกับสูตรนี้เป๊ะๆ หรือไม่ (ไม่สนลำดับ)</summary>
        public bool Matches(IReadOnlyList<RawMaterialData> potContents)
        {
            if (potContents == null || potContents.Count != useMaterials.Count)
                return false;

            foreach (var required in useMaterials)
            {
                if (required == null || !Contains(potContents, required))
                    return false;
            }
            return true;
        }

        private static bool Contains(IReadOnlyList<RawMaterialData> list, RawMaterialData item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == item) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (useMaterials.Count != RequiredMaterialCount)
            {
                Debug.LogWarning($"[{name}] Blueprint ต้องมีวัตถุดิบ {RequiredMaterialCount} ชนิด", this);
                return;
            }

            var seen = new HashSet<MaterialType>();
            foreach (var m in useMaterials)
            {
                if (m == null)
                {
                    Debug.LogWarning($"[{name}] มีช่อง Use Material ที่ยังว่างอยู่", this);
                    return;
                }
                if (!seen.Add(m.MaterialType))
                {
                    Debug.LogWarning($"[{name}] Blueprint ห้ามมีประเภทซ้ำ ({m.MaterialType})", this);
                    return;
                }
            }
        }
#endif
    }
}
