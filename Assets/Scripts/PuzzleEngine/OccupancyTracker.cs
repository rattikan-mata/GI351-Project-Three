using System;
using System.Collections.Generic;

namespace PuzzleEngine
{
    /// <summary>
    /// นับ "collider" ที่แต่ละ "ตัว" แตะอยู่ ตัวหนึ่งจะถือว่าเข้า/ออก ก็ต่อเมื่อ collider แรกเข้า / collider สุดท้ายออก
    /// ทำให้วัตถุที่มีหลาย collider ไม่ถูกนับซ้ำ
    /// Prune ลบตัวที่หายไปแล้ว (เช่น ก้อนขนที่ถูก Destroy ตอนยังทับสวิตช์) ให้ออกโดยไม่ต้องรอ exit event
    /// </summary>
    public sealed class OccupancyTracker
    {
        private readonly Dictionary<object, int> counts = new Dictionary<object, int>();

        /// <summary>ตัวแรกเข้ามา</summary>
        public event Action<object> BodyEntered;

        /// <summary>ตัวสุดท้ายออก (หรือถูกลบ)</summary>
        public event Action<object> BodyLeft;

        /// <summary>จำนวนตัวที่อยู่ตอนนี้</summary>
        public int BodyCount => counts.Count;

        public void ColliderEntered(object body)
        {
            if (body == null) return;

            int n;
            counts.TryGetValue(body, out n);
            counts[body] = n + 1;
            if (n == 0) BodyEntered?.Invoke(body);
        }

        public void ColliderExited(object body)
        {
            if (body == null) return;

            int n;
            if (!counts.TryGetValue(body, out n)) return;

            if (n > 1)
            {
                counts[body] = n - 1;
                return;
            }

            counts.Remove(body);
            BodyLeft?.Invoke(body);
        }

        /// <summary>ลบตัวที่ isAlive คืน false ออก (เรียกก่อนประมวลผลการเข้าใหม่)</summary>
        public void Prune(Func<object, bool> isAlive)
        {
            if (counts.Count == 0 || isAlive == null) return;

            var dead = new List<object>();
            foreach (object key in counts.Keys)
            {
                if (!isAlive(key)) dead.Add(key);
            }

            foreach (object key in dead)
            {
                counts.Remove(key);
                BodyLeft?.Invoke(key);
            }
        }
    }
}
