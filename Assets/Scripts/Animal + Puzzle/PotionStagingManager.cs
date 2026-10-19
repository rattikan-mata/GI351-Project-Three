using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// จัดการช่องพักยา 3 ช่อง ส่งยาไปยัง Dropper 3 ตัว
    /// - ถ้ามียาไม่ครบ (เช่น มีแค่ช่อง 3 ช่องเดียว หรือมี 1-2 ขวด): ส่งยาที่มีออกไปทำงานทันที ไม่ต้องรอสวิตช์
    /// - ถ้าใส่ยาครบทุกช่อง: ส่งตามลำดับสวิตช์ (Switch Progression)
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
        [SerializeField] private PotionType skywardMapping = PotionType.Skyward; // เขียว+ฟ้า
        [SerializeField] private PotionType havocMapping = PotionType.Havoc;     // เขียว+ส้ม
        [SerializeField] private PotionType surgeMapping = PotionType.Surge;     // ฟ้า+ส้ม

        [Header("Switch Progression (เฉพาะตอนใส่ยาครบทุกช่อง)")]
        [Tooltip("ติ๊กไว้ = เมื่อใส่ยาครบทุกช่อง จะปล่อยยาเรียงทีละขั้นตอนตามลำดับสวิตช์\nไม่ติ๊ก = ปล่อยพร้อมกันเสมอ")]
        [SerializeField] private bool dispenseSequentially = true;

        [Tooltip("เว้นอย่างน้อยกี่วินาทีระหว่างการยิงแต่ละนัด (นับหลังสั่งยิง) ก่อนจะเช็คว่าปลดล็อคขั้นถัดไปได้ไหม")]
        [SerializeField] private float minShotGap = 1f;

        [Tooltip("จำนวนยาขั้นต่ำที่ทำให้ส่งตามลำดับสวิตช์ (น้อยกว่านี้ = ส่งทันทีพร้อมกัน) ปืนมี 4 ช่องแล้ว จึงไม่ใช้เงื่อนไข 'ครบทุกช่อง' อีก")]
        [SerializeField, Min(1)] private int minPotionsForSequence = 2;

        [Tooltip("สวิตช์เป้าหมายของแต่ละขั้นตอน (ช่อง 1 -> สวิตช์ 1, ช่อง 2 -> สวิตช์ 2, ช่อง 3 -> สวิตช์ 3)")]
        [SerializeField] private FloorSwitch[] stageSwitches = new FloorSwitch[3];

        [Tooltip("หน่วงเวลารอสวิตช์เป้าหมายนานสุดต่อขั้นตอน (วินาที) ป้องกันการค้างถ้าสัตว์ติดสิ่งกีดขวาง")]
        [SerializeField] private float stageTimeout = 8f;

        private Coroutine sendRoutine;
        private bool isSendingSequentially;

        public bool IsBusy => isSendingSequentially;

        // ชื่อสวิตช์ประจำแต่ละขั้น ใช้ผูกใหม่หลังรีเซ็ตพัซเซิล
        private string[] stageSwitchNames;

        private void Awake()
        {
            EnsureStageSwitchesInitialized();
            RememberStageSwitchNames();
        }

        private void RememberStageSwitchNames()
        {
            stageSwitchNames = new string[stageSwitches.Length];
            for (int i = 0; i < stageSwitches.Length; i++)
                stageSwitchNames[i] = stageSwitches[i] != null ? stageSwitches[i].name : null;
        }

        /// <summary>รีเซ็ตพัซเซิล: หยุดการส่งยาที่ค้างอยู่ (ยาที่ส่งไปแล้วไม่คืน ยาที่ยังอยู่ในช่องพักไม่ถูกแตะ)</summary>
        public void ResetState()
        {
            if (sendRoutine != null) StopCoroutine(sendRoutine);
            sendRoutine = null;
            isSendingSequentially = false;
        }

        /// <summary>หลังรีเซ็ตพัซเซิล สวิตช์ถูกสร้างใหม่ ผูกกลับด้วยชื่อเดิม</summary>
        public void RebindStageSwitches()
        {
            if (stageSwitchNames == null) return;
            var all = FindObjectsByType<FloorSwitch>();
            for (int i = 0; i < stageSwitchNames.Length && i < stageSwitches.Length; i++)
            {
                if (stageSwitchNames[i] == null) continue;
                stageSwitches[i] = null;
                foreach (var sw in all)
                    if (sw.name == stageSwitchNames[i]) { stageSwitches[i] = sw; break; }
            }
        }

        /// <summary>
        /// ฟังก์ชันผูกกับปุ่ม "ส่งยา"
        /// - ถ้าใส่ยาครบทุกช่อง: ส่งตามลำดับสวิตช์ (Switch Progression)
        /// - ถ้ามียาไม่ครบ (เช่น มีแค่ 1 หรือ 2 ขวด): ส่งออกไปทำงานทันที ไม่ต้องรอสวิตช์!
        /// </summary>
        public void SendPotionsToDropper()
        {
            if (isSendingSequentially)
            {
                Debug.Log("[PotionStagingManager] กำลังส่งยาตามลำดับอยู่ กรุณารอสักครู่");
                return;
            }

            int count = GetOccupiedSlotCount();
            if (count == 0)
            {
                Debug.Log("[PotionStagingManager] ไม่มีขวดยาในช่องพัก");
                return;
            }

            if (count >= minPotionsForSequence && dispenseSequentially)
            {
                // มียาตั้งแต่ขั้นต่ำขึ้นไป -> ส่งตามลำดับสวิตช์ (Switch Progression)
                Debug.Log($"[PotionStagingManager] มียา {count}/{stagingSlots.Length} ขวด -> ส่งตามลำดับสวิตช์");
                sendRoutine = StartCoroutine(SequentialSendWithSwitchesRoutine());
            }
            else
            {
                // มียาไม่ครบ (เช่น ใส่ไว้ช่องเดียว หรือ 2 ช่อง) -> ส่งยาที่มีออกไปทำงานทันที ไม่ต้องรอสวิตช์!
                Debug.Log($"[PotionStagingManager] มียาไม่ครบ ({count}/{stagingSlots.Length}) -> ส่งยาที่มีออกไปทำงานทันที");
                SendIncompleteImmediate();
            }
        }

        /// <summary>
        /// โหมดมียาไม่ครบ: ปล่อยยาที่มีอยู่ในช่องออกไปทำงานทันที ไม่ต้องรอสวิตช์ก่อนหน้า
        /// </summary>
        private void SendIncompleteImmediate()
        {
            bool sentAny = false;

            for (int i = 0; i < stagingSlots.Length; i++)
            {
                var slot = stagingSlots[i];
                if (slot == null || !slot.IsOccupied) continue;

                var potionData = slot.StoredPotion;
                PotionType pType = ConvertToPotionType(potionData);
                if (pType == PotionType.None)
                {
                    Debug.LogWarning($"[PotionStagingManager] ยา {potionData.DisplayName} ไม่สามารถแปลงเป็น PotionType ได้");
                    continue;
                }

                int targetIndex = i; // ช่อง i ยิงจากปืน i เสมอ ไม่เกี่ยวกับชนิดยา
                if (targetIndex < 0 || targetIndex >= droppers.Length || droppers[targetIndex] == null)
                {
                    Debug.LogWarning($"[PotionStagingManager] ไม่พบ Dropper ที่รับยา {pType} ได้สำหรับช่องที่ {i + 1}");
                    continue;
                }

                var dropper = droppers[targetIndex];

                if (dropper.TryAddPotion(pType))
                {
                    // ส่งยาออกไปทันที โดยข้ามการเช็ค prerequisiteSwitch เพื่อให้ทำงานทันทีแม้สวิตช์ก่อนหน้ายังไม่ติด
                    dropper.StartDispensing(ignorePrerequisite: true);
                    slot.Clear();
                    sentAny = true;
                }
                else
                {
                    Debug.Log($"[PotionStagingManager] Dropper ตัวที่ {targetIndex + 1} กำลังทำงานอยู่หรือช่องไม่ว่าง");
                }
            }

            if (sentAny)
            {
                Debug.Log("[PotionStagingManager] ส่งยาออกไปทำงานทันทีสำเร็จ");
            }
        }

        /// <summary>
        /// โหมดมียาครบทุกช่อง: ส่งทีละขั้นตอน และรอให้สวิตช์เป้าหมายของแต่ละขั้นตอนถูกกดก่อน จึงค่อยปล่อยขั้นตอนถัดไป
        /// </summary>
        private IEnumerator SequentialSendWithSwitchesRoutine()
        {
            isSendingSequentially = true;
            bool sentAny = false;

            EnsureStageSwitchesInitialized();

            // ชุดเก็บสวิตช์ที่ถูกกดแล้วในรอบนี้ เพื่อป้องกันปัญหาเมื่อ FloorSwitch เป็นแบบ Toggle แล้วถูกชนซ้ำจนดับ
            HashSet<FloorSwitch> triggeredSwitches = new HashSet<FloorSwitch>();

            Action<FloorSwitch> onSwitchChanged = (sw) =>
            {
                if (sw != null && sw.IsActive)
                {
                    triggeredSwitches.Add(sw);
                }
            };

            // สมัครรับฟัง Event ของสวิตช์ทุกตัวที่เกี่ยวข้อง
            List<FloorSwitch> watchedSwitches = new List<FloorSwitch>();
            if (stageSwitches != null)
            {
                foreach (var sw in stageSwitches)
                {
                    if (sw != null && !watchedSwitches.Contains(sw))
                    {
                        watchedSwitches.Add(sw);
                        sw.StateChanged += onSwitchChanged;
                        if (sw.IsActive) triggeredSwitches.Add(sw);
                    }
                }
            }

            try
            {
                for (int i = 0; i < stagingSlots.Length; i++)
                {
                    var slot = stagingSlots[i];
                    if (slot == null || !slot.IsOccupied) continue;

                    var potionData = slot.StoredPotion;
                    PotionType pType = ConvertToPotionType(potionData);
                    if (pType == PotionType.None) continue;

                    int targetIndex = i; // ช่อง i ยิงจากปืน i เสมอ ไม่เกี่ยวกับชนิดยา
                    if (targetIndex < 0 || targetIndex >= droppers.Length || droppers[targetIndex] == null) continue;

                    var dropper = droppers[targetIndex];

                    // 1. รอให้ Dropper พร้อม (ถ้ามีรอบก่อนหน้ากำลังทำงาน รอได้สูงสุด 3 วินาที)
                    float waitReadyTimer = 0f;
                    while (dropper.IsDispensing && waitReadyTimer < 3f)
                    {
                        waitReadyTimer += Time.deltaTime;
                        yield return null;
                    }

                    // 2. หาสวิตช์เป้าหมายของขั้นตอนนี้
                    FloorSwitch target = GetTargetSwitch(i, targetIndex);
                    if (target != null && !watchedSwitches.Contains(target))
                    {
                        watchedSwitches.Add(target);
                        target.StateChanged += onSwitchChanged;
                        if (target.IsActive) triggeredSwitches.Add(target);
                    }

                    // 3. ใส่ยาเข้า Dropper
                    bool added = dropper.TryAddPotion(pType);
                    if (!added)
                    {
                        dropper.ClearAllSlots();
                        added = dropper.TryAddPotion(pType);
                    }

                    if (added)
                    {
                        // สั่งปล่อยยา โดยสั่งข้าม prerequisite ของตัว Dropper เอง (เพราะ Manager เป็นคนคุมจังหวะให้แล้ว)
                        dropper.StartDispensing(ignorePrerequisite: true);
                        slot.Clear();
                        sentAny = true;

                        Debug.Log($"[PotionStagingManager] ส่งยาขั้นตอนที่ {i + 1} ไปยัง {dropper.name} (รอกดสวิตช์: {(target != null ? target.name : "ไม่มีสวิตช์")})");

                        // หน่วงเวลาสั้นๆ เพื่อให้ยาบินออกจากท่อ
                        yield return new WaitForSeconds(0.3f);

                        // 4. รอจนกว่า:
                        // - สวิตช์เป้าหมายถูกกด (IsActive เป็น true หรือถูกบันทึกใน triggeredSwitches)
                        // - หรือถ้าไม่มีสวิตช์เป้าหมาย ให้รอจน Dropper ปล่อยเสร็จ
                        // - หรือครบเวลา Timeout ป้องกันการค้าง
                        float timer = 0f;
                        while (timer < stageTimeout)
                        {
                            // เว้นช่วงขั้นต่ำระหว่างนัด ไม่ให้ยิงรัวทันทีที่สวิตช์ติด
                            if (timer < minShotGap)
                            {
                                timer += Time.deltaTime;
                                yield return null;
                                continue;
                            }

                            if (target != null && (target.IsActive || triggeredSwitches.Contains(target)))
                            {
                                Debug.Log($"[PotionStagingManager] สวิตช์ {target.name} ถูกกดแล้ว! ปลดล็อคขั้นตอนถัดไปทันที");
                                break;
                            }

                            if (target == null && !dropper.IsDispensing)
                            {
                                break;
                            }

                            timer += Time.deltaTime;
                            yield return null;
                        }

                        // หน่วงเวลาสั้นๆ ระหว่างขั้นตอน
                        yield return new WaitForSeconds(0.2f);
                    }
                    else
                    {
                        Debug.LogWarning($"[PotionStagingManager] Dropper ตัวที่ {targetIndex + 1} ไม่สามารถรับยาได้");
                    }
                }
            }
            finally
            {
                // เคลียร์ Event Listener ป้องกัน Memory Leak
                foreach (var sw in watchedSwitches)
                {
                    if (sw != null) sw.StateChanged -= onSwitchChanged;
                }

                if (sentAny)
                {
                    Debug.Log("[PotionStagingManager] ส่งยาตามลำดับสวิตช์เสร็จสิ้น");
                }

                isSendingSequentially = false;
                sendRoutine = null;
            }
        }

        /// <summary>
        /// หาสวิตช์เป้าหมายสำหรับขั้นตอนหรือ Dropper ที่ระบุ (มี Fallback อัตโนมัติถ้าไม่ได้ตั้งใน Inspector)
        /// </summary>
        private FloorSwitch GetTargetSwitch(int stageIndex, int dropperIndex)
        {
            // 1. ตรวจสอบจาก stageSwitches ตามลำดับขั้นตอน
            if (stageSwitches != null && stageIndex >= 0 && stageIndex < stageSwitches.Length && stageSwitches[stageIndex] != null)
                return stageSwitches[stageIndex];

            // 2. ตรวจสอบจาก stageSwitches ตาม dropperIndex
            if (stageSwitches != null && dropperIndex >= 0 && dropperIndex < stageSwitches.Length && stageSwitches[dropperIndex] != null)
                return stageSwitches[dropperIndex];

            // 3. ตรวจสอบจาก Dropper เอง
            if (dropperIndex >= 0 && dropperIndex < droppers.Length && droppers[dropperIndex] != null)
            {
                if (droppers[dropperIndex].TargetSwitch != null)
                    return droppers[dropperIndex].TargetSwitch;
                if (droppers[dropperIndex].PrerequisiteSwitch != null)
                    return droppers[dropperIndex].PrerequisiteSwitch;
            }

            return null;
        }

        /// <summary>
        /// ตรวจสอบและค้นหาสวิตช์ในฉากอัตโนมัติหากยังไม่ได้ตั้งค่าใน Inspector
        /// </summary>
        private void EnsureStageSwitchesInitialized()
        {
            if (stageSwitches == null || stageSwitches.Length < 3)
            {
                Array.Resize(ref stageSwitches, 3);
            }

            bool hasMissing = false;
            for (int i = 0; i < stageSwitches.Length; i++)
            {
                if (stageSwitches[i] == null)
                {
                    hasMissing = true;
                    break;
                }
            }

            if (!hasMissing) return;

            // 1. ลองดึงจาก Droppers ก่อน
            for (int i = 0; i < stageSwitches.Length; i++)
            {
                if (stageSwitches[i] == null && i < droppers.Length && droppers[i] != null)
                {
                    if (droppers[i].TargetSwitch != null)
                        stageSwitches[i] = droppers[i].TargetSwitch;
                    else if (droppers[i].PrerequisiteSwitch != null)
                        stageSwitches[i] = droppers[i].PrerequisiteSwitch;
                }
            }

            // 2. ถ้ายังไม่ครบ ให้ค้นหา FloorSwitch ทั้งหมดในฉาก แล้วเรียงตามตำแหน่งแกน X (ซ้ายไปขวา)
            bool stillMissing = false;
            for (int i = 0; i < stageSwitches.Length; i++)
            {
                if (stageSwitches[i] == null) { stillMissing = true; break; }
            }

            if (stillMissing)
            {
                FloorSwitch[] sceneSwitches = FindObjectsByType<FloorSwitch>(FindObjectsSortMode.None);
                if (sceneSwitches != null && sceneSwitches.Length > 0)
                {
                    Array.Sort(sceneSwitches, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
                    int assignIdx = 0;
                    for (int i = 0; i < stageSwitches.Length && assignIdx < sceneSwitches.Length; i++)
                    {
                        if (stageSwitches[i] == null)
                        {
                            stageSwitches[i] = sceneSwitches[assignIdx++];
                            Debug.Log($"[PotionStagingManager] ตรวจพบสวิตช์ในฉากอัตโนมัติ: เชื่อมต่อ {stageSwitches[i].name} เข้ากับขั้นตอนที่ {i + 1}");
                        }
                    }
                }
            }
        }

        private bool IsAllSlotsOccupied()
        {
            if (stagingSlots == null || stagingSlots.Length == 0) return false;
            foreach (var slot in stagingSlots)
            {
                if (slot == null || !slot.IsOccupied) return false;
            }
            return true;
        }

        private int GetOccupiedSlotCount()
        {
            if (stagingSlots == null) return 0;
            int count = 0;
            foreach (var slot in stagingSlots)
            {
                if (slot != null && slot.IsOccupied) count++;
            }
            return count;
        }

        private PotionType ConvertToPotionType(PotionData potion)
        {
            if (potion == null) return PotionType.None;
            switch (potion.Kind)
            {
                case PotionKind.Red: return redMapping;
                case PotionKind.Blue: return blueMapping;
                case PotionKind.Green: return greenMapping;
                case PotionKind.Skyward: return skywardMapping;
                case PotionKind.Havoc: return havocMapping;
                case PotionKind.Surge: return surgeMapping;
                default: return PotionType.None;
            }
        }
    }
}