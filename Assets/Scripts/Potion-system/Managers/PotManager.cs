using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// Pot Manager: ระบบหม้อต้ม
    /// - Pot Capacity: สูงสุด 3 ชิ้น
    /// - Core Rule: 1 Raw material per type (Monster Parts / Ore / Herb ซ้ำไม่ได้)
    /// Flow: Input Validation -> Recipe Evaluation -> Confirm (Craft) / Refund (PourOut)
    /// </summary>
    public class PotManager : MonoBehaviour
    {
        public const int PotCapacity = 3;

        [SerializeField] private SourceInventory sourceInventory;
        [SerializeField] private ResultSlot resultSlot;
        [SerializeField] private PotionRecipeBook recipeBook;

        private readonly List<RawMaterialData> contents = new List<RawMaterialData>(PotCapacity);

        public IReadOnlyList<RawMaterialData> Contents => contents;
        public bool IsFull => contents.Count >= PotCapacity;
        public bool IsEmpty => contents.Count == 0;

        /// <summary>ยาที่จะได้ตอนนี้ (null ถ้าของยังไม่ครบ 3 ชิ้น)</summary>
        public PotionData CurrentResult { get; private set; }

        public event Action OnContentsChanged;
        public event Action<PotionData> OnResultChanged;
        public event Action<AddMaterialResult> OnAddRejected;
        public event Action<PotionData> OnCrafted;

        // ---------- Step 1: Input Validation ----------

        /// <summary>เช็คว่าใส่วัตถุดิบนี้ลงหม้อได้หรือไม่ (ยังไม่ดึงของออกจากคลัง)</summary>
        public AddMaterialResult CanAdd(RawMaterialData material)
        {
            if (material == null) return AddMaterialResult.InvalidItem;
            if (IsFull) return AddMaterialResult.PotFull;
            if (HasType(material.MaterialType)) return AddMaterialResult.DuplicateType;
            if (sourceInventory == null || !sourceInventory.HasStock(material)) return AddMaterialResult.OutOfStock;
            return AddMaterialResult.Success;
        }

        /// <summary>ใส่วัตถุดิบลงหม้อ: ผ่านกฎแล้วจะลดจำนวนในคลังชั่วคราว และประเมินสูตรใหม่</summary>
        public AddMaterialResult TryAddMaterial(RawMaterialData material)
        {
            var result = CanAdd(material);
            if (result != AddMaterialResult.Success)
            {
                OnAddRejected?.Invoke(result);
                return result;
            }

            sourceInventory.TryTake(material);
            contents.Add(material);
            NotifyContentsChanged();
            return AddMaterialResult.Success;
        }

        public bool HasType(MaterialType type)
        {
            foreach (var m in contents)
            {
                if (m.MaterialType == type) return true;
            }
            return false;
        }

        // ---------- Step 3: Refund ----------

        /// <summary>เอาวัตถุดิบชิ้นเดียวออกจากหม้อ ของคืนกลับคลัง</summary>
        public bool RemoveMaterial(RawMaterialData material)
        {
            if (material == null || !contents.Remove(material)) return false;

            sourceInventory.Add(material);
            NotifyContentsChanged();
            return true;
        }

        /// <summary>เทยาทิ้ง: ของทั้งหมดในหม้อถูกส่งกลับคลัง จำนวนเพิ่มตามเดิม</summary>
        public void PourOut()
        {
            if (IsEmpty) return;

            foreach (var material in contents)
                sourceInventory.Add(material);

            contents.Clear();
            NotifyContentsChanged();
        }

        // ---------- Step 3: Confirm (Craft) ----------

        /// <summary>
        /// ยืนยัน Craft: ถ้า Result Slot ไม่เต็ม ใส่ยาลงช่อง และทำลายวัตถุดิบในหม้อถาวร (ไม่คืนคลัง)
        /// </summary>
        public CraftResult TryCraft()
        {
            if (!IsFull) return CraftResult.PotNotFull;
            if (CurrentResult == null) return CraftResult.NoResult;
            if (resultSlot == null || resultSlot.IsFull) return CraftResult.ResultSlotFull;

            var potion = CurrentResult;
            // จำวัตถุดิบที่ใช้จริงไว้กับขวดยา (snapshot ก่อนล้างหม้อ)
            resultSlot.TryAdd(new PotionInstance(potion, contents));

            contents.Clear(); // ทำลายวัตถุดิบถาวร (ไม่ Refund)
            NotifyContentsChanged();
            OnCrafted?.Invoke(potion);
            return CraftResult.Success;
        }

        // ---------- Step 2: Recipe Evaluation ----------

        private void NotifyContentsChanged()
        {
            OnContentsChanged?.Invoke();
            EvaluateRecipe();
        }

        private void EvaluateRecipe()
        {
            var result = recipeBook != null ? recipeBook.Evaluate(contents) : null;
            if (result == CurrentResult) return;

            CurrentResult = result;
            OnResultChanged?.Invoke(CurrentResult);
        }
    }
}
