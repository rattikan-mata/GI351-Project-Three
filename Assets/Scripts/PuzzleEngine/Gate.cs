using System.Collections.Generic;

namespace PuzzleEngine
{
    /// <summary>All = ทุกตัวต้อง ON / Any = อย่างน้อยหนึ่งตัว ON</summary>
    public enum GateMode
    {
        All,
        Any
    }

    /// <summary>
    /// รวมสัญญาณหลายตัวเป็นเงื่อนไขเดียว ใช้กับประตู สายพาน และเงื่อนไขชนะ
    /// - ไม่มี input ที่ใช้ได้เลย (ว่าง / null ทั้งหมด) = false
    /// - input ที่เป็น null จะถูกข้าม
    /// </summary>
    public static class Gate
    {
        public static bool Evaluate(GateMode mode, IList<ISignal> inputs)
        {
            if (inputs == null) return false;

            int total = 0;
            int on = 0;
            for (int i = 0; i < inputs.Count; i++)
            {
                ISignal s = inputs[i];
                if (s == null) continue;

                total++;
                if (s.IsOn) on++;
            }

            if (total == 0) return false;
            return mode == GateMode.All ? on == total : on > 0;
        }
    }
}
