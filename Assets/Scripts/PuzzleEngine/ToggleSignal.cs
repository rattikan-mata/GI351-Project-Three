using System;
using System.Collections.Generic;

namespace PuzzleEngine
{
    /// <summary>
    /// สวิตช์แบบทอกเกิล
    /// - เมื่อมีตัวแรกเข้ามาบนสวิตช์ (ตอนที่ไม่มีใครอยู่เลย) สถานะจะสลับ 1 ครั้ง
    /// - ตัวที่เข้ามาเพิ่มตอนมีคนอยู่แล้ว ไม่สลับ
    /// - การออกไม่สลับ (รอบถัดไปที่มีตัวเข้ามาใหม่จึงจะสลับอีกครั้ง)
    /// - นับต่อ "ตัว" ไม่ใช่ต่อ collider จึงไม่สลับซ้ำจากวัตถุที่มีหลาย collider
    /// adapter เป็นคนกรองว่าตัวไหนนับได้ (แมวบิน ฯลฯ) ก่อนเรียก BodyEntered
    /// </summary>
    public sealed class ToggleSignal : ISignal
    {
        private readonly HashSet<object> occupants = new HashSet<object>();

        public bool IsOn { get; private set; }

        public event Action Changed;

        /// <summary>จำนวนตัวที่อยู่บนสวิตช์ตอนนี้</summary>
        public int OccupantCount => occupants.Count;

        /// <summary>มีตัวเข้ามาบนสวิตช์</summary>
        public void BodyEntered(object body)
        {
            if (body == null) return;

            bool wasEmpty = occupants.Count == 0;
            if (!occupants.Add(body)) return; // ตัวนี้อยู่แล้ว ไม่นับซ้ำ
            if (!wasEmpty) return;            // มีคนอยู่แล้ว ไม่สลับ

            IsOn = !IsOn;
            Changed?.Invoke();
        }

        /// <summary>ตัวออกจากสวิตช์ (หรือถูกทำลาย) ไม่สลับสถานะ</summary>
        public void BodyExited(object body)
        {
            if (body == null) return;
            occupants.Remove(body);
        }
    }
}
