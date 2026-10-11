using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// Result Slot: ช่องเก็บยาสำเร็จรูป ความจุสูงสุด 5 ขวด
    /// รอนำไปวางในคิวยาให้แมวกินต่อไป
    /// </summary>
    public class ResultSlot : MonoBehaviour
    {
        public const int Capacity = 5;

        private readonly List<PotionInstance> potions = new List<PotionInstance>(Capacity);

        public IReadOnlyList<PotionInstance> Potions => potions;
        public int Count => potions.Count;
        public bool IsFull => potions.Count >= Capacity;

        public event Action<PotionInstance> OnPotionAdded;
        public event Action<PotionInstance> OnPotionRemoved;
        public event Action OnChanged;

        public bool TryAdd(PotionInstance potion)
        {
            if (potion == null || potion.Data == null || IsFull) return false;

            potions.Add(potion);
            OnPotionAdded?.Invoke(potion);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>หยิบยาออกจากช่อง (เช่น นำไปวางในคิวให้แมว)</summary>
        public bool TryTake(int index, out PotionInstance potion)
        {
            potion = null;
            if (index < 0 || index >= potions.Count) return false;

            potion = potions[index];
            potions.RemoveAt(index);
            OnPotionRemoved?.Invoke(potion);
            OnChanged?.Invoke();
            return true;
        }
    }
}
