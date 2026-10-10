using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// Source Inventory: คลังวัตถุดิบของผู้เล่น
    /// - ผู้เล่นดึงของใส่หม้อ -> จำนวนลดลงชั่วคราว (TryTake)
    /// - เทยาทิ้ง / เอาของออกจากหม้อ -> ของถูกคืนและจำนวนเพิ่มกลับ (Add)
    /// </summary>
    public class SourceInventory : MonoBehaviour
    {
        [Serializable]
        public class StartingStock
        {
            public RawMaterialData material;
            [Min(0)] public int quantity = 1;
        }

        [SerializeField] private List<StartingStock> startingStock = new List<StartingStock>();

        private readonly Dictionary<RawMaterialData, int> quantities = new Dictionary<RawMaterialData, int>();

        /// <summary>(วัตถุดิบ, จำนวนใหม่) ใช้ให้ UI อัปเดต Quantity</summary>
        public event Action<RawMaterialData, int> OnQuantityChanged;

        public IEnumerable<KeyValuePair<RawMaterialData, int>> All => quantities;

        private void Awake()
        {
            quantities.Clear();
            foreach (var stock in startingStock)
            {
                if (stock.material == null) continue;
                quantities.TryGetValue(stock.material, out int current);
                quantities[stock.material] = current + stock.quantity;
            }
        }

        private void Start()
        {
            // แจ้ง UI ให้แสดงจำนวนเริ่มต้น
            foreach (var pair in quantities)
                OnQuantityChanged?.Invoke(pair.Key, pair.Value);
        }

        public int GetQuantity(RawMaterialData material)
        {
            return material != null && quantities.TryGetValue(material, out int q) ? q : 0;
        }

        public bool HasStock(RawMaterialData material, int amount = 1)
        {
            return GetQuantity(material) >= amount;
        }

        /// <summary>ดึงวัตถุดิบออกจากคลัง (เช่น ใส่ลงหม้อ)</summary>
        public bool TryTake(RawMaterialData material, int amount = 1)
        {
            if (material == null || amount <= 0 || !HasStock(material, amount))
                return false;

            quantities[material] -= amount;
            OnQuantityChanged?.Invoke(material, quantities[material]);
            return true;
        }

        /// <summary>เพิ่มวัตถุดิบเข้าคลัง (เช่น คืนของจากหม้อ หรือเก็บของใหม่)</summary>
        public void Add(RawMaterialData material, int amount = 1)
        {
            if (material == null || amount <= 0) return;

            quantities.TryGetValue(material, out int current);
            quantities[material] = current + amount;
            OnQuantityChanged?.Invoke(material, quantities[material]);
        }
    }
}
