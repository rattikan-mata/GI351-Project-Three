using System.Collections.Generic;
using NUnit.Framework;

namespace PuzzleEngine.Tests
{
    public class OccupancyTrackerTests
    {
        private static List<string> Log(OccupancyTracker t)
        {
            var log = new List<string>();
            t.BodyEntered += b => log.Add("in:" + b);
            t.BodyLeft += b => log.Add("out:" + b);
            return log;
        }

        [Test]
        public void SingleColliderEnterAndExit()
        {
            var t = new OccupancyTracker();
            var log = Log(t);

            t.ColliderEntered("cat");
            t.ColliderExited("cat");

            CollectionAssert.AreEqual(new List<string> { "in:cat", "out:cat" }, log);
        }

        [Test]
        public void TwoCollidersOfSameBodyCountAsOne()
        {
            // วัตถุเดียวที่มี 2 collider: เข้าครั้งเดียว ออกเมื่อ collider สุดท้ายออก
            var t = new OccupancyTracker();
            var log = Log(t);

            t.ColliderEntered("cat");
            t.ColliderEntered("cat");
            t.ColliderExited("cat");           // ยังเหลืออีกอัน ยังไม่ออก
            Assert.AreEqual(1, t.BodyCount);
            t.ColliderExited("cat");           // ออกจริง

            CollectionAssert.AreEqual(new List<string> { "in:cat", "out:cat" }, log);
        }

        [Test]
        public void PruneRemovesDestroyedBodies()
        {
            // ก้อนขนถูก Destroy ตอนยังทับสวิตช์ ต้องถูกนับว่าออก
            var t = new OccupancyTracker();
            var log = Log(t);
            var alive = new HashSet<object>();

            t.ColliderEntered("hairball");
            alive.Add("cat");
            t.ColliderEntered("cat");

            t.Prune(o => alive.Contains(o));

            CollectionAssert.AreEqual(new List<string> { "in:hairball", "in:cat", "out:hairball" }, log);
            Assert.AreEqual(1, t.BodyCount);
        }

        [Test]
        public void ExitOfUnknownBodyDoesNothing()
        {
            var t = new OccupancyTracker();
            var log = Log(t);

            t.ColliderExited("ghost");

            Assert.AreEqual(0, log.Count);
        }

        [Test]
        public void NullBodyIsIgnored()
        {
            var t = new OccupancyTracker();
            var log = Log(t);

            t.ColliderEntered(null);
            t.ColliderExited(null);

            Assert.AreEqual(0, log.Count);
        }

        [Test]
        public void CatWithTwoCollidersTogglesSwitchOnce()
        {
            // ต่อกับ ToggleSignal: วัตถุที่มี 2 collider ต้องสลับสวิตช์แค่ครั้งเดียว
            var t = new OccupancyTracker();
            var toggle = new ToggleSignal();
            t.BodyEntered += toggle.BodyEntered;
            t.BodyLeft += toggle.BodyExited;

            t.ColliderEntered("cat");
            t.ColliderEntered("cat");

            Assert.IsTrue(toggle.IsOn);
        }
    }
}
