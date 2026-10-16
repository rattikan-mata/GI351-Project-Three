using System;
using System.Collections.Generic;

namespace PuzzleEngine
{
    /// <summary>
    /// ลำดับการปล่อยยาหลังกดปุ่มส่งยา (ไม่ผูกกับ Unity, ไม่รู้จักชนิดสัตว์)
    ///
    /// กติกา
    /// - ไล่ช่องตามลำดับ 0 -> N-1
    /// - ช่องว่างถูกข้ามไปเลย ไม่มีการรอหรือหน่วงเพิ่ม
    /// - ยาชิ้นแรกที่มีของจะถูกปล่อยทันที
    /// - ยาชิ้นถัดไปจะรอจนยาชิ้นก่อนหน้า "ติด" (adapter เรียก NotifyFinished) แล้วรออีก Gap วินาทีก่อนปล่อย
    /// - ถ้ายาไม่ติดภายใน Timeout วินาที ถือว่าติดแล้ว (กันลำดับค้าง)
    /// - กดซ้ำระหว่างทำงานจะถูกละเว้น
    /// </summary>
    public sealed class PotionSequence<TPotion> where TPotion : class
    {
        private readonly Queue<KeyValuePair<int, TPotion>> pending = new Queue<KeyValuePair<int, TPotion>>();

        private bool inFlight;
        private float flightTime;
        private float cooldown;

        /// <param name="gapSeconds">รอกี่วินาทีหลังยาชิ้นก่อนหน้าติด ก่อนปล่อยชิ้นถัดไป</param>
        /// <param name="timeoutSeconds">ยาที่ไม่ติดภายในเวลานี้ จะถือว่าติดแล้ว</param>
        public PotionSequence(float gapSeconds = 3f, float timeoutSeconds = 20f)
        {
            Gap = Math.Max(0f, gapSeconds);
            Timeout = Math.Max(0.01f, timeoutSeconds);
        }

        public float Gap { get; }

        public float Timeout { get; }

        public bool IsRunning { get; private set; }

        /// <summary>ยาชิ้นที่ปล่อยไปแล้วยังไม่ติด (รอ NotifyFinished หรือ timeout)</summary>
        public bool IsInFlight => inFlight;

        /// <summary>ปล่อยยาออกไป (ช่องที่, ยา) ช่องเริ่มที่ 0</summary>
        public event Action<int, TPotion> Dispense;

        /// <summary>ช่องที่ถูกข้ามตอนกด</summary>
        public event Action<int> SlotSkipped;

        /// <summary>ยาทุกชิ้นถูกปล่อยและติดครบแล้ว</summary>
        public event Action Finished;

        /// <summary>
        /// กดปุ่มส่งยา
        /// คืน true ถ้าเริ่มลำดับ (มียาอย่างน้อย 1 ชิ้นและยังไม่ได้ทำงานอยู่)
        /// slots คือช่องพักยาเรียงตามลำดับ ค่า null = ช่องว่าง
        /// </summary>
        public bool Start(IList<TPotion> slots)
        {
            if (IsRunning || slots == null) return false;

            pending.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                {
                    SlotSkipped?.Invoke(i);
                    continue;
                }
                pending.Enqueue(new KeyValuePair<int, TPotion>(i, slots[i]));
            }

            if (pending.Count == 0) return false;

            IsRunning = true;
            Dispatch();
            return true;
        }

        /// <summary>ยาชิ้นที่ปล่อยไปล่าสุดติดแล้ว (สัตว์ทำงานเสร็จ) เรียกจากฝั่ง Unity</summary>
        public void NotifyFinished()
        {
            if (!IsRunning || !inFlight) return;

            inFlight = false;
            if (pending.Count == 0)
            {
                Complete();
                return;
            }
            cooldown = Gap;
        }

        /// <summary>เรียกทุกเฟรมจากฝั่ง Unity ด้วย deltaTime</summary>
        public void Update(float deltaTime)
        {
            if (!IsRunning) return;

            if (inFlight)
            {
                flightTime += deltaTime;
                if (flightTime >= Timeout) NotifyFinished();
                return;
            }

            if (cooldown > 0f)
            {
                cooldown -= deltaTime;
                if (cooldown > 0f) return;
                cooldown = 0f;
            }

            Dispatch();
        }

        /// <summary>หยุดทันที (เช่น รีเซ็ตด่าน) ยาที่เหลือในคิวจะถูกทิ้ง</summary>
        public void Cancel()
        {
            if (!IsRunning) return;

            pending.Clear();
            inFlight = false;
            cooldown = 0f;
            IsRunning = false;
        }

        private void Dispatch()
        {
            KeyValuePair<int, TPotion> next = pending.Dequeue();
            inFlight = true;
            flightTime = 0f;
            cooldown = 0f;
            Dispense?.Invoke(next.Key, next.Value);
        }

        private void Complete()
        {
            IsRunning = false;
            inFlight = false;
            Finished?.Invoke();
        }
    }
}
