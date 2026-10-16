using System;

namespace PuzzleEngine
{
    /// <summary>สัญญาณ 1 เส้น: เปิดอยู่ (true) หรือปิดอยู่ (false) ใช้ได้กับ สวิตช์ / เกต / เงื่อนไขอื่นๆ</summary>
    public interface ISignal
    {
        bool IsOn { get; }

        /// <summary>ยิงเมื่อ IsOn เปลี่ยนค่า</summary>
        event Action Changed;
    }
}
