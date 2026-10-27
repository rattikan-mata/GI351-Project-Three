using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PotionSystem
{
    /// <summary>
    /// ชั้นวางขวดยา 6 ช่อง (แทนระบบปรุงยาแบบถาดเดิม)
    /// - เริ่มด่าน: สุ่มยาจาก Level Potions มาให้ Starting Potions ขวด
    /// - ยาชนิดเดียวกันซ้อนเป็นตัวเลข (x2, x3) ชนิดใหม่ลงช่องว่างช่องแรก
    /// - น้องนักปรุงยา (PotionBrewer) ปรุงเพิ่มได้ Brews Per Stage ครั้งต่อด่าน
    ///   (ปุ่มรีเซ็ตพัซเซิลไม่แตะชั้นนี้ ได้คืนเมื่อโหลดด่านใหม่ เช่น Restart ในหน้าจบเกม)
    /// </summary>
    public class PotionShelf : MonoBehaviour
    {
        [Header("Level Config: ยาที่ด่านนี้สุ่มได้")]
        [Tooltip("รายการยาที่สุ่มได้ในด่านนี้ (ทั้งยาเริ่มต้นและยาที่น้องปรุง) ใส่ซ้ำได้ ยิ่งซ้ำยิ่งออกบ่อย")]
        [SerializeField] private List<PotionData> levelPotions = new List<PotionData>();
        [Tooltip("จำนวนขวดที่สุ่มให้ตอนเริ่มด่าน")]
        [SerializeField, Min(0)] private int startingPotions = 3;
        [Tooltip("น้องปรุงยาได้กี่ครั้งต่อด่าน (ปุ่มรีเซ็ตพัซเซิลไม่คืน ได้คืนเมื่อโหลดด่านใหม่)")]
        [FormerlySerializedAs("brewsPerReset")]
        [SerializeField, Min(0)] private int brewsPerStage = 10;
        [Tooltip("เปิด = ใช้ seed คงที่ (สุ่มได้ผลเดิมทุกครั้ง ไว้เทสต์)")]
        [SerializeField] private bool useSeed = false;
        [SerializeField] private int seed = 12345;

        [Header("Slots (ช่องบนชั้น เรียงจากช่องแรก)")]
        [SerializeField] private PotionShelfSlot2D[] slots = new PotionShelfSlot2D[6];

        /// <summary>เรียกทุกครั้งที่ของบนชั้นหรือจำนวนครั้งปรุงที่เหลือเปลี่ยน</summary>
        public event Action OnChanged;

        public int BrewsLeft { get; private set; }
        public int BrewsPerStage => brewsPerStage;

        /// <summary>เพิ่มขึ้นทุกครั้งที่รีเซ็ตชั้น (น้องใช้เช็คว่ายาที่กำลังปรุงอยู่ยังเป็นของรอบนี้ไหม)</summary>
        public int ResetVersion { get; private set; }

        private System.Random rng;

        private void Start()
        {
            rng = useSeed ? new System.Random(seed) : new System.Random();
            ResetShelf();
        }

        /// <summary>ล้างชั้น สุ่มยาเริ่มต้นใหม่ และคืนจำนวนครั้งปรุงให้ครบ (ใช้ตอนเริ่มด่านและตอนรีเซ็ตพัซเซิล)</summary>
        public void ResetShelf()
        {
            if (rng == null) rng = useSeed ? new System.Random(seed) : new System.Random();
            ResetVersion++;

            foreach (var slot in slots)
                if (slot != null) slot.Set(null, 0);

            for (int i = 0; i < startingPotions; i++)
            {
                var potion = RandomPotion();
                if (potion != null) Add(potion);
            }

            BrewsLeft = brewsPerStage;
            OnChanged?.Invoke();
        }

        /// <summary>สุ่มยา 1 ขวดจากรายการของด่าน (null ถ้ารายการว่าง)</summary>
        public PotionData RandomPotion()
        {
            var valid = levelPotions.FindAll(p => p != null);
            if (valid.Count == 0) return null;
            return valid[rng.Next(valid.Count)];
        }

        /// <summary>ใส่ยา 1 ขวดลงชั้น: ชนิดเดิมซ้อนจำนวน / ชนิดใหม่ลงช่องว่างช่องแรก คืน false ถ้าไม่มีที่</summary>
        public bool Add(PotionData potion)
        {
            if (potion == null) return false;

            foreach (var slot in slots)
            {
                if (slot != null && slot.Potion != null && slot.Potion.Kind == potion.Kind)
                {
                    slot.Set(slot.Potion, slot.Count + 1);
                    OnChanged?.Invoke();
                    return true;
                }
            }

            foreach (var slot in slots)
            {
                if (slot != null && slot.IsEmpty)
                {
                    slot.Set(potion, 1);
                    OnChanged?.Invoke();
                    return true;
                }
            }

            Debug.LogWarning($"[PotionShelf] ชั้นเต็ม ใส่ {potion.DisplayName} ไม่ได้", this);
            return false;
        }

        /// <summary>หยิบยา 1 ขวดออกจากช่องนี้ (ช่องหมดแล้ว = ว่าง)</summary>
        public bool TakeOne(PotionShelfSlot2D slot, out PotionData potion)
        {
            potion = null;
            if (slot == null || slot.IsEmpty) return false;

            potion = slot.Potion;
            int left = slot.Count - 1;
            slot.Set(left > 0 ? potion : null, left);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>ใช้สิทธิ์ปรุง 1 ครั้ง คืน false ถ้าหมดแล้ว</summary>
        public bool TryConsumeBrew()
        {
            if (BrewsLeft <= 0) return false;
            BrewsLeft--;
            OnChanged?.Invoke();
            return true;
        }
    }
}
