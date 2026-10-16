using System.Collections.Generic;
using NUnit.Framework;

namespace PuzzleEngine.Tests
{
    public class ToggleSignalTests
    {
        [Test]
        public void FirstEntryTurnsOn()
        {
            var s = new ToggleSignal();
            s.BodyEntered("cat");
            Assert.IsTrue(s.IsOn);
        }

        [Test]
        public void ExitDoesNotChangeState()
        {
            var s = new ToggleSignal();
            s.BodyEntered("cat");
            s.BodyExited("cat");
            Assert.IsTrue(s.IsOn);
        }

        [Test]
        public void EntryAfterEmptyTogglesAgain()
        {
            var s = new ToggleSignal();
            s.BodyEntered("cat");  // on
            s.BodyExited("cat");
            s.BodyEntered("cat");  // off
            Assert.IsFalse(s.IsOn);
        }

        [Test]
        public void SecondBodyWhileOccupiedDoesNotToggle()
        {
            // ของสองชิ้นเข้าพร้อมกัน ต้องสลับแค่ครั้งเดียว
            var s = new ToggleSignal();
            s.BodyEntered("cat");
            s.BodyEntered("box");
            Assert.IsTrue(s.IsOn);
            Assert.AreEqual(2, s.OccupantCount);
        }

        [Test]
        public void SameBodyEnteringTwiceCountsOnce()
        {
            // collider หลายอันของวัตถุเดียวกัน ต้องไม่สลับซ้ำ
            var s = new ToggleSignal();
            s.BodyEntered("cat");
            s.BodyEntered("cat");
            Assert.IsTrue(s.IsOn);
            Assert.AreEqual(1, s.OccupantCount);
        }

        [Test]
        public void ChangedFiresOnlyOnToggle()
        {
            var s = new ToggleSignal();
            int count = 0;
            s.Changed += () => count++;

            s.BodyEntered("cat");  // fire
            s.BodyEntered("box");  // no fire
            s.BodyExited("cat");
            s.BodyExited("box");   // no fire
            s.BodyEntered("cat");  // fire

            Assert.AreEqual(2, count);
        }

        [Test]
        public void NullBodyIsIgnored()
        {
            var s = new ToggleSignal();
            s.BodyEntered(null);
            Assert.IsFalse(s.IsOn);
        }
    }

    public class GateTests
    {
        private static ISignal Sig(bool on)
        {
            var s = new ToggleSignal();
            if (on) s.BodyEntered("x");
            return s;
        }

        [Test]
        public void AllWithEveryInputOnIsTrue()
        {
            var inputs = new List<ISignal> { Sig(true), Sig(true) };
            Assert.IsTrue(Gate.Evaluate(GateMode.All, inputs));
        }

        [Test]
        public void AllWithOneOffIsFalse()
        {
            var inputs = new List<ISignal> { Sig(true), Sig(false) };
            Assert.IsFalse(Gate.Evaluate(GateMode.All, inputs));
        }

        [Test]
        public void AnyWithOneOnIsTrue()
        {
            var inputs = new List<ISignal> { Sig(false), Sig(true) };
            Assert.IsTrue(Gate.Evaluate(GateMode.Any, inputs));
        }

        [Test]
        public void EmptyInputsAreFalse()
        {
            Assert.IsFalse(Gate.Evaluate(GateMode.All, new List<ISignal>()));
            Assert.IsFalse(Gate.Evaluate(GateMode.Any, new List<ISignal>()));
            Assert.IsFalse(Gate.Evaluate(GateMode.All, null));
        }

        [Test]
        public void NullInputsAreSkipped()
        {
            var inputs = new List<ISignal> { null, Sig(true) };
            Assert.IsTrue(Gate.Evaluate(GateMode.All, inputs));
        }
    }

    public class PotionSequenceTests
    {
        // ใช้ string แทนยา: null = ช่องว่าง
        private static PotionSequence<string> Make(float gap = 3f, float timeout = 20f)
        {
            return new PotionSequence<string>(gap, timeout);
        }

        private static List<string> Log(PotionSequence<string> seq)
        {
            var log = new List<string>();
            seq.Dispense += (i, p) => log.Add("dispense:" + i + ":" + p);
            seq.SlotSkipped += i => log.Add("skip:" + i);
            seq.Finished += () => log.Add("finished");
            return log;
        }

        [Test]
        public void EmptySlotsDoNotStart()
        {
            var seq = Make();
            var log = Log(seq);

            bool started = seq.Start(new List<string> { null, null });

            Assert.IsFalse(started);
            Assert.IsFalse(seq.IsRunning);
            CollectionAssert.AreEqual(new List<string> { "skip:0", "skip:1" }, log);
        }

        [Test]
        public void FirstPotionIsDispensedImmediately()
        {
            var seq = Make();
            var log = Log(seq);

            seq.Start(new List<string> { "A" });

            CollectionAssert.AreEqual(new List<string> { "dispense:0:A" }, log);
        }

        [Test]
        public void EmptySlotBeforeFirstPotionIsSkippedWithoutWaiting()
        {
            var seq = Make();
            var log = Log(seq);

            seq.Start(new List<string> { null, "B" });

            CollectionAssert.AreEqual(new List<string> { "skip:0", "dispense:1:B" }, log);
        }

        [Test]
        public void NextPotionWaitsForPreviousToFinishPlusGap()
        {
            // A, ช่องว่าง, B  ->  B ต้องรอให้ A ติดก่อน แล้วรอ 3 วิ ช่องว่างไม่เพิ่มเวลา
            var seq = Make(gap: 3f);
            var log = Log(seq);

            seq.Start(new List<string> { "A", null, "B" });
            seq.Update(10f);                    // A ยังไม่ติด ห้ามปล่อย B
            CollectionAssert.AreEqual(new List<string> { "skip:1", "dispense:0:A" }, log);

            seq.NotifyFinished();               // A ติด
            seq.Update(2.9f);                   // ยังไม่ครบ 3 วิ
            CollectionAssert.AreEqual(new List<string> { "skip:1", "dispense:0:A" }, log);

            seq.Update(0.2f);                   // ครบ 3 วิ
            CollectionAssert.AreEqual(new List<string> { "skip:1", "dispense:0:A", "dispense:2:B" }, log);

            seq.NotifyFinished();               // B ติด -> จบ
            Assert.IsFalse(seq.IsRunning);
            Assert.AreEqual("finished", log[log.Count - 1]);
        }

        [Test]
        public void TimeoutCountsAsFinished()
        {
            // ยาไม่ติดเลย ลำดับต้องไม่ค้าง
            var seq = Make(gap: 3f, timeout: 5f);
            var log = Log(seq);

            seq.Start(new List<string> { "A", "B" });
            seq.Update(5f);                     // timeout -> A ถือว่าติด เริ่มนับ gap
            seq.Update(3f);                     // ครบ gap -> ปล่อย B

            CollectionAssert.AreEqual(new List<string> { "dispense:0:A", "dispense:1:B" }, log);
        }

        [Test]
        public void StartWhileRunningIsIgnored()
        {
            var seq = Make();
            var log = Log(seq);

            seq.Start(new List<string> { "A", "B" });
            bool second = seq.Start(new List<string> { "C" });

            Assert.IsFalse(second);
            CollectionAssert.AreEqual(new List<string> { "dispense:0:A" }, log);
        }

        [Test]
        public void CancelStopsAndDropsRemainingPotions()
        {
            var seq = Make();
            var log = Log(seq);

            seq.Start(new List<string> { "A", "B" });
            seq.Cancel();
            seq.NotifyFinished();
            seq.Update(10f);

            CollectionAssert.AreEqual(new List<string> { "dispense:0:A" }, log);
            Assert.IsFalse(seq.IsRunning);
        }

        [Test]
        public void ZeroGapDispatchesNextOnNextUpdate()
        {
            var seq = Make(gap: 0f);
            var log = Log(seq);

            seq.Start(new List<string> { "A", "B" });
            seq.NotifyFinished();
            seq.Update(0f);

            CollectionAssert.AreEqual(new List<string> { "dispense:0:A", "dispense:1:B" }, log);
        }
    }

    public class LevelGoalTests
    {
        [Test]
        public void CompletesOnceWhenConditionBecomesTrue()
        {
            bool doorOpen = false;
            var goal = new LevelGoal(() => doorOpen);
            int fired = 0;
            goal.Completed += () => fired++;

            Assert.IsFalse(goal.Check());
            doorOpen = true;
            Assert.IsTrue(goal.Check());
            doorOpen = false;                   // ประตูปิดกลับ ด่านยังจบอยู่
            Assert.IsTrue(goal.Check());
            Assert.AreEqual(1, fired);
        }
    }
}
