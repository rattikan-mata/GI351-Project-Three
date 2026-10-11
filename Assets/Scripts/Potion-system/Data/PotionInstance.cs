using System.Collections.Generic;

namespace PotionSystem
{
    /// <summary>
    /// ขวดยาที่ถูกปรุงขึ้นจริง 1 ขวด: จำ PotionData และวัตถุดิบที่ใช้ผสมจริง
    /// เป็น class (reference type) เพื่อให้เทียบตัวตนของแต่ละขวดได้
    /// </summary>
    public sealed class PotionInstance
    {
        private readonly List<RawMaterialData> usedMaterials;

        public PotionData Data { get; }
        public IReadOnlyList<RawMaterialData> UsedMaterials => usedMaterials;

        public string DisplayName => Data != null ? Data.DisplayName : string.Empty;

        public PotionInstance(PotionData data, IEnumerable<RawMaterialData> materials)
        {
            Data = data;
            usedMaterials = new List<RawMaterialData>();
            if (materials == null) return;
            foreach (var m in materials)
                if (m != null) usedMaterials.Add(m);
        }
    }
}
