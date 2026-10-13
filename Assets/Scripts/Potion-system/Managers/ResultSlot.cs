using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// Result Slot: ช่องเก็บยาสำเร็จรูป ความจุสูงสุด 5 ขวด
    /// ตอนนี้เป็นช่องคงที่ 5 ช่อง (ช่องว่างเป็น null) ยาไม่เลื่อนชดเชยเมื่อหยิบออก
    /// จึงวางยาลงช่องไหนก็ได้ ด้วย TryAddAt(index, potion)
    /// หมายเหตุ: Potions มีขนาด 5 เสมอ และบางช่องอาจเป็น null
    /// </summary>
    public class ResultSlot : MonoBehaviour
    {
        public const int Capacity = 5;

        private readonly PotionInstance[] slots = new PotionInstance[Capacity];

        /// <summary>ยาในแต่ละช่อง (ยาวเท่ากับ Capacity ช่องว่าง = null)</summary>
        public IReadOnlyList<PotionInstance> Potions => slots;

        /// <summary>จำนวนยาที่มีอยู่จริง (ไม่นับช่องว่าง)</summary>
        public int Count
        {
            get
            {
                int n = 0;
                foreach (var p in slots) if (p != null) n++;
                return n;
            }
        }

        public bool IsFull => Count >= Capacity;

        public bool IsSlotEmpty(int index) => index >= 0 && index < Capacity && slots[index] == null;

        public event Action<PotionInstance> OnPotionAdded;
        public event Action<PotionInstance> OnPotionRemoved;
        public event Action OnChanged;

        /// <summary>เพิ่มยาในช่องว่างช่องแรก (ใช้ตอนปรุงยาเสร็จ)</summary>
        public bool TryAdd(PotionInstance potion)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (slots[i] == null) return TryAddAt(i, potion);
            }
            return false;
        }

        /// <summary>วางยาลงช่องที่ระบุ (ต้องเป็นช่องว่าง)</summary>
        public bool TryAddAt(int index, PotionInstance potion)
        {
            if (potion == null || potion.Data == null) return false;
            if (index < 0 || index >= Capacity || slots[index] != null) return false;

            slots[index] = potion;
            OnPotionAdded?.Invoke(potion);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>ย้ายยาจากช่อง from ไปช่อง to (ถ้าช่อง to มียาอยู่ จะสลับที่กัน)</summary>
        public bool TryMove(int from, int to)
        {
            if (from == to) return false;
            if (from < 0 || from >= Capacity || to < 0 || to >= Capacity) return false;
            if (slots[from] == null) return false;

            (slots[from], slots[to]) = (slots[to], slots[from]);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>หยิบยาออกจากช่อง (ช่องอื่นไม่เลื่อน)</summary>
        public bool TryTake(int index, out PotionInstance potion)
        {
            potion = null;
            if (index < 0 || index >= Capacity || slots[index] == null) return false;

            potion = slots[index];
            slots[index] = null;
            OnPotionRemoved?.Invoke(potion);
            OnChanged?.Invoke();
            return true;
        }
    }
}