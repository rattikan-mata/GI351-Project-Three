using System.Collections.Generic;
using NUnit.Framework;

namespace PuzzleEngine.Tests
{
    public class PotionSequenceFlightTests
    {
        [Test]
        public void NotInFlightWhenIdle()
        {
            var seq = new PotionSequence<string>(3f, 20f);
            Assert.IsFalse(seq.IsInFlight);
        }

        [Test]
        public void InFlightAfterDispenseUntilNotified()
        {
            var seq = new PotionSequence<string>(3f, 20f);
            seq.Start(new List<string> { "A" });

            Assert.IsTrue(seq.IsInFlight);
            seq.NotifyFinished();
            Assert.IsFalse(seq.IsInFlight);
        }

        [Test]
        public void LaunchFailingInsideDispenseDoesNotLeaveSequenceStuck()
        {
            // ตัวปล่อยล้มเหลวทันที: เรียก NotifyFinished ภายใน Dispense ต้องไม่ทำให้ลำดับค้าง
            var seq = new PotionSequence<string>(0f, 20f);
            int dispensed = 0;
            seq.Dispense += (i, p) =>
            {
                dispensed++;
                seq.NotifyFinished();
            };

            seq.Start(new List<string> { "A", "B" });
            seq.Update(0f);

            Assert.AreEqual(2, dispensed);
            Assert.IsFalse(seq.IsRunning);
        }
    }
}
