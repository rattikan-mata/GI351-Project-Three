using System;

namespace PuzzleEngine
{
    /// <summary>
    /// เงื่อนไขชนะของด่าน
    /// เช็กเป็นระยะด้วย Check() เมื่อครบเงื่อนไขครั้งแรกจะยิง Completed ครั้งเดียว และจำไว้ว่าจบแล้ว
    /// </summary>
    public sealed class LevelGoal
    {
        private readonly Func<bool> condition;

        public LevelGoal(Func<bool> condition)
        {
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public bool IsComplete { get; private set; }

        public event Action Completed;

        /// <summary>คืน true ถ้าด่านจบแล้ว (ทั้งที่เพิ่งจบตอนนี้หรือจบไปก่อนหน้า)</summary>
        public bool Check()
        {
            if (IsComplete) return true;
            if (!condition()) return false;

            IsComplete = true;
            Completed?.Invoke();
            return true;
        }
    }
}
