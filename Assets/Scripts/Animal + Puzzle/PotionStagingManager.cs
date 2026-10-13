using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// จัดการช่องพักยา 3 ช่อง ส่งยาไปยัง Dropper 3 ตัวแบบ 1:1 (Slot 1 -> Dropper 1, Slot 2 -> Dropper 2, Slot 3 -> Dropper 3)
    /// </summary>
    public class PotionStagingManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("ช่องพักยาทั้ง 3 ช่อง (Index 0, 1, 2)")]
        [SerializeField] private PotionStagingSlot2D[] stagingSlots = new PotionStagingSlot2D[3];

        [Tooltip("เครื่องปล่อยยา (Dropper) ทั้ง 3 เครื่อง แยกตามลำดับช่อง")]
        [SerializeField] private PotionSequenceDropper[] droppers = new PotionSequenceDropper[3];

        [Header("Mapping (PotionKind -> PotionType สำหรับสัตว์)")]
        [SerializeField] private PotionType redMapping = PotionType.Strength;   // ยาแดง  -> ยากล้ามผลักกล่อง
        [SerializeField] private PotionType blueMapping = PotionType.Light;     // ยาฟ้า  -> ยาบิน
        [SerializeField] private PotionType greenMapping = PotionType.Vomit;    // ยาเขียว -> ยาอ้วกก้อนขน

        /// <summary>
        /// ฟังก์ชันผูกกับปุ่ม "ส่งยา"
        /// นำยาจาก Staging Slot แต่ละช่อง ไปใส่และสั่งทำงาน Dropper ตัวที่ตรงกันทันที
        /// </summary>
        public void SendPotionsToDropper()
        {
            bool sentAny = false;

            for (int i = 0; i < stagingSlots.Length; i++)
            {
                var slot = stagingSlots[i];
                if (slot == null || !slot.IsOccupied) continue;

                if (i >= droppers.Length || droppers[i] == null)
                {
                    Debug.LogWarning($"[PotionStagingManager] ยังไม่ได้กำหนด Dropper สำหรับช่องที่ {i + 1}");
                    continue;
                }

                var potionData = slot.StoredPotion;
                PotionType pType = ConvertToPotionType(potionData);

                if (pType == PotionType.None)
                {
                    Debug.LogWarning($"[PotionStagingManager] ยา {potionData.DisplayName} ไม่สามารถแปลงเป็น PotionType ได้");
                    continue;
                }

                // สั่งเพิ่มยาเข้าไปใน Dropper ตัวที่ i
                if (droppers[i].TryAddPotion(pType))
                {
                    // สั่งให้ Dropper ตัวนี้เริ่มปล่อยยาทันที (เพราะแต่ละเครื่องมีช่องเดียว)
                    droppers[i].StartDispensing();
                    
                    slot.Clear(); // ล้างช่องพักหลังจากส่งสำเร็จ
                    sentAny = true;
                }
                else
                {
                    Debug.Log($"[PotionStagingManager] Dropper ตัวที่ {i + 1} กำลังทำงานอยู่หรือช่องไม่ว่าง");
                }
            }

            if (sentAny)
            {
                Debug.Log("[PotionStagingManager] ส่งยาไปยัง Dropper แต่ละตัวสำเร็จ");
            }
        }

        private PotionType ConvertToPotionType(PotionData potion)
        {
            if (potion == null) return PotionType.None;
            switch (potion.Kind)
            {
                case PotionKind.Red: return redMapping;
                case PotionKind.Blue: return blueMapping;
                case PotionKind.Green: return greenMapping;
                default: return PotionType.None;
            }
        }
    }
}