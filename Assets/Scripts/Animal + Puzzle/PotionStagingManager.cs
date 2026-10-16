using System.Collections.Generic;
using PuzzleEngine;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// จัดการช่องพักยา 3 ช่อง และปุ่ม "ส่งยา"
    /// กดปุ่มแล้วยาจะถูกปล่อยตามลำดับช่อง 1 -> 3 ตามกติกาใน PuzzleEngine.PotionSequence
    ///   - ช่องว่างถูกข้าม
    ///   - ยาชิ้นถัดไปรอให้ชิ้นก่อนหน้าติด แล้วรอ Gap วินาที
    /// ช่อง i ส่งไปที่ Dropper ตัวที่ i เสมอ
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

        [Header("Sequence Timing")]
        [Tooltip("รอกี่วินาทีหลังยาชิ้นก่อนหน้าติด ก่อนปล่อยชิ้นถัดไป")]
        [SerializeField] private float gapSeconds = 3f;

        [Tooltip("ถ้ายาไม่ติดภายในกี่วินาที ถือว่าติดแล้ว (กันลำดับค้าง)")]
        [SerializeField] private float timeoutSeconds = 20f;

        private PotionSequence<PotionData> sequence;
        private PotionSequenceDropper activeDropper;

        /// <summary>กำลังส่งยาอยู่ไหม (ใช้ซ่อน/ล็อกปุ่มได้)</summary>
        public bool IsSending => sequence != null && sequence.IsRunning;

        private void Awake()
        {
            sequence = new PotionSequence<PotionData>(gapSeconds, timeoutSeconds);
            sequence.Dispense += OnDispense;
            sequence.Finished += OnFinished;
        }

        private void Update()
        {
            if (sequence == null || !sequence.IsRunning) return;

            sequence.Update(Time.deltaTime);

            // ยาที่ปล่อยไปแล้วติดหรือยัง: ถ้า Dropper ที่ใช้อยู่ว่างแล้ว แปลว่าสัตว์ทำงานเสร็จ
            if (sequence.IsInFlight && (activeDropper == null || !activeDropper.IsBusy))
                sequence.NotifyFinished();
        }

        /// <summary>ฟังก์ชันผูกกับปุ่ม "ส่งยา"</summary>
        public void SendPotionsToDropper()
        {
            if (sequence == null || sequence.IsRunning) return;

            var slots = new List<PotionData>(stagingSlots.Length);
            foreach (PotionStagingSlot2D slot in stagingSlots)
            {
                slots.Add(slot != null ? slot.StoredPotion : null);
            }

            if (!sequence.Start(slots)) return;

            // ยาถูกย้ายเข้าลำดับแล้ว ล้างช่องพัก ผู้เล่นวางยาใหม่ได้ โดยไม่กระทบลำดับที่กำลังทำงาน
            foreach (PotionStagingSlot2D slot in stagingSlots)
            {
                if (slot != null) slot.Clear();
            }
        }

        private void OnDispense(int index, PotionData potion)
        {
            activeDropper = (index >= 0 && index < droppers.Length) ? droppers[index] : null;

            PotionType type = ConvertToPotionType(potion);
            bool launched = activeDropper != null && type != PotionType.None && activeDropper.LaunchPotion(type);

            if (!launched)
            {
                Debug.LogWarning($"[PotionStagingManager] ช่อง {index + 1} ปล่อยยาไม่ได้ ข้ามไปชิ้นถัดไป");
                activeDropper = null;
                sequence.NotifyFinished();
            }
        }

        private void OnFinished()
        {
            activeDropper = null;
            Debug.Log("[PotionStagingManager] ยาทั้งลำดับถูกใช้ครบแล้ว");
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
