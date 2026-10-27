using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// จัดการช่องเตรียมยิง ส่งยาไปยัง Dropper ตามลำดับช่อง (ช่อง i -> Dropper i)
    /// จำนวนช่องแล้วแต่ด่าน (ด่าน 1-2 = 3 ช่อง ด่านต่อไป = 4 ช่อง) ใส่ช่องและ Dropper ในซีนให้ครบแล้วผูกที่นี่
    /// - Require All Slots Filled: ต้องใส่ยาครบทุกช่องที่ผูกปืน/สัตว์แล้วก่อน ถึงกดส่งได้
    /// - ส่งตั้งแต่ Min Potions For Sequence ขวดขึ้นไป: ปล่อยตามลำดับสวิตช์ (Switch Progression)
    /// </summary>
    public class PotionStagingManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("ช่องเตรียมยิงของด่านนี้ เรียงตามลำดับ (ช่อง 1, 2, 3, 4...)")]
        [SerializeField] private PotionStagingSlot2D[] stagingSlots = new PotionStagingSlot2D[3];

        [Tooltip("เครื่องปล่อยยา (Dropper) แยกตามลำดับช่อง จำนวนเท่ากับช่องเตรียมยิง")]
        [SerializeField] private PotionSequenceDropper[] droppers = new PotionSequenceDropper[3];

        [Header("Send Rule")]
        [Tooltip("ติ๊กไว้ = ต้องใส่ยาครบทุกช่องก่อนถึงกดส่งได้ (นับเฉพาะช่องที่ผูก Dropper ที่มีสัตว์เป้าหมายแล้ว)\nไม่ครบ = ส่งไม่ได้ ช่องที่ยังว่างจะกะพริบบอก")]
        [SerializeField] private bool requireAllSlotsFilled = true;

        [Tooltip("ติ๊กไว้ = กดส่ง 1 ครั้งยิงยา 1 ขวด (ช่องแรกที่มียาก่อน) คันโยกเด้งกลับ แล้วกดอีกทีเพื่อยิงขวดถัดไป\nกดครั้งแรกต้องใส่ยาครบทุกช่องตามกฎด้านบน ครั้งต่อไปไม่ต้องครบ\nไม่ติ๊ก = กดครั้งเดียวส่งทั้งหมดตามลำดับสวิตช์ (แบบเดิม)")]
        [SerializeField] private bool stepByStepSend = true;

        [Tooltip("โหมดทีละขวด: ติ๊กไว้ = พอสัตว์ที่ได้ยาทำงานสำเร็จ (สวิตช์เป้าหมายถูกกด) หรือหยุดนิ่งแล้ว จะยิงขวดถัดไปให้เอง\nไม่ติ๊ก = ต้องกดปุ่ม ▶ เองเท่านั้น (ปุ่ม ▶ กดยิงก่อนได้เสมอ ไม่ต้องรอ)")]
        [SerializeField] private bool autoAdvance = true;

        [Header("Mapping (PotionKind -> PotionType สำหรับสัตว์)")]
        [SerializeField] private PotionType redMapping = PotionType.Strength;   // ยาแดง  -> ยากล้ามผลักกล่อง
        [SerializeField] private PotionType blueMapping = PotionType.Light;     // ยาฟ้า  -> ยาบิน
        [SerializeField] private PotionType greenMapping = PotionType.Vomit;    // ยาเขียว -> ยาอ้วกก้อนขน
        [SerializeField] private PotionType skywardMapping = PotionType.Skyward; // เขียว+ฟ้า
        [SerializeField] private PotionType havocMapping = PotionType.Havoc;     // เขียว+ส้ม
        [SerializeField] private PotionType surgeMapping = PotionType.Surge;     // ฟ้า+ส้ม

        [Header("Switch Progression")]
        [Tooltip("ติ๊กไว้ = เมื่อใส่ยาครบทุกช่อง จะปล่อยยาเรียงทีละขั้นตอนตามลำดับสวิตช์\nไม่ติ๊ก = ปล่อยพร้อมกันเสมอ")]
        [SerializeField] private bool dispenseSequentially = true;

        [Tooltip("เว้นอย่างน้อยกี่วินาทีระหว่างการยิงแต่ละนัด (นับหลังสั่งยิง) ก่อนจะเช็คว่าปลดล็อคขั้นถัดไปได้ไหม")]
        [SerializeField] private float minShotGap = 1f;

        [Tooltip("จำนวนยาขั้นต่ำที่ทำให้ส่งตามลำดับสวิตช์ (น้อยกว่านี้ = ส่งทันทีพร้อมกัน)")]
        [SerializeField, Min(1)] private int minPotionsForSequence = 2;

        [Tooltip("สวิตช์เป้าหมายของแต่ละขั้นตอน (ช่อง 1 -> สวิตช์ 1, ช่อง 2 -> สวิตช์ 2, ช่อง 3 -> สวิตช์ 3)")]
        [SerializeField] private FloorSwitch[] stageSwitches = new FloorSwitch[3];

        [Tooltip("หน่วงเวลารอสวิตช์เป้าหมายนานสุดต่อขั้นตอน (วินาที) ป้องกันการค้างถ้าสัตว์ติดสิ่งกีดขวาง")]
        [SerializeField] private float stageTimeout = 8f;

        private Coroutine sendRoutine;
        private bool isSendingSequentially;
        private bool stepBatchActive; // โหมดทีละขวด: ยิงขวดแรกไปแล้ว ขวดที่เหลือกดยิงต่อได้เลย
        private Coroutine autoRoutine;
        private int lastFiredIndex = -1;

        public bool IsBusy => isSendingSequentially;

        /// <summary>โหมดทีละขวด: คันโยกเริ่มชุดยิง ปุ่ม ▶ ยิงขวดถัดไป</summary>
        public bool StepByStep => stepByStepSend;
        /// <summary>โหมดทีละขวด: อยู่ระหว่างชุดยิง (ยิงขวดแรกแล้ว ยังมียาเหลือในช่อง)</summary>
        public bool IsBatchActive => stepBatchActive;
        public int RemainingPotions => GetOccupiedSlotCount();

        /// <summary>คันโยกกดแล้วยิงได้ (คันโยกโยกลง)</summary>
        public event Action SendAccepted;
        /// <summary>คันโยกกดแล้วยิงไม่ได้ เช่น ยาไม่ครบช่อง หรืออยู่กลางชุดยิงอยู่ (คันโยกสั่น)</summary>
        public event Action SendRejected;
        /// <summary>ปุ่ม ▶ กดแล้วยิงไม่ได้ (ปุ่มสั่น)</summary>
        public event Action NextRejected;
        /// <summary>ชุดยิงจบ (ยิงขวดสุดท้ายแล้ว หรือรีเซ็ตพัซเซิลแล้วคืนยาเข้าชั้น) คันโยกเด้งกลับ</summary>
        public event Action BatchEnded;

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

        private void Update()
        {
            // ยาในช่องหมดระหว่างชุดยิง (เช่น ลากกลับเข้าชั้นเอง) = ชุดจบ
            if (stepBatchActive && GetOccupiedSlotCount() == 0) EndBatch();
        }

        private void EndBatch()
        {
            StopAutoAdvance();
            if (!stepBatchActive) return;
            stepBatchActive = false;
            BatchEnded?.Invoke();
        }

        private void StopAutoAdvance()
        {
            if (autoRoutine != null) StopCoroutine(autoRoutine);
            autoRoutine = null;
        }

        /// <summary>เริ่มเฝ้าสัตว์ของนัดที่เพิ่งยิง: พอสำเร็จ/หยุดนิ่ง ยิงขวดถัดไปให้เอง (ถ้าอยู่ในชุดยิงและเปิด Auto Advance)</summary>
        private void BeginAutoAdvance()
        {
            StopAutoAdvance();
            if (!autoAdvance || !stepBatchActive || lastFiredIndex < 0) return;
            autoRoutine = StartCoroutine(AutoAdvanceRoutine(lastFiredIndex));
        }

        private IEnumerator AutoAdvanceRoutine(int index)
        {
            var dropper = droppers[index];
            var target = GetTargetSwitch(index, index);
            bool triggered = false;
            Action<FloorSwitch> onChanged = sw => { if (sw != null && sw.IsActive) triggered = true; };
            if (target != null) target.StateChanged += onChanged;

            bool finished = false;
            try
            {
                float t = 0f;
                while (t < stageTimeout)
                {
                    t += Time.deltaTime;
                    if (t >= minShotGap)
                    {
                        // สำเร็จ: สวิตช์เป้าหมายถูกกด / ไม่มีสวิตช์และปืนปล่อยเสร็จ / สัตว์ทำงานเสร็จและหยุดนิ่ง
                        if (target != null && (target.IsActive || triggered)) { finished = true; break; }
                        if (target == null && !dropper.IsDispensing) { finished = true; break; }
                        var animal = dropper.TargetAnimal;
                        if (!dropper.IsDispensing && animal != null && !animal.IsAutoRunning && !animal.IsMoving) { finished = true; break; }
                    }
                    yield return null;
                }
            }
            finally
            {
                if (target != null) target.StateChanged -= onChanged;
            }

            // ครบเวลาแล้วสัตว์ยังไม่เสร็จ (ติดอะไรอยู่) = ไม่ยิงต่อเอง ให้ผู้เล่นกด ▶ เอง
            if (!finished) { autoRoutine = null; yield break; }

            yield return new WaitForSeconds(0.2f);

            float waited = 0f;
            while (stepBatchActive && waited < 3f)
            {
                if (SendNextPotion())
                {
                    CraftingSfx.Play(CraftingSfx.NextShot);
                    autoRoutine = null;
                    if (GetOccupiedSlotCount() == 0) EndBatch();
                    else BeginAutoAdvance();
                    yield break;
                }
                waited += Time.deltaTime;
                yield return null; // ปืนของขวดถัดไปยังไม่ว่าง รอสักครู่
            }
            autoRoutine = null;
        }

        /// <summary>
        /// รีเซ็ตพัซเซิล: หยุดการส่งยาที่ค้างอยู่ (ยาที่ส่งไปแล้วไม่คืน)
        /// ถ้ารีเซ็ตกลางชุดยิง ยาที่ยังไม่ถูกใช้ในช่องเด้งกลับเข้าชั้นขวดยา และคันโยกเด้งกลับ
        /// </summary>
        public void ResetState()
        {
            if (sendRoutine != null) StopCoroutine(sendRoutine);
            sendRoutine = null;
            isSendingSequentially = false;
            StopAutoAdvance();

            if (stepBatchActive) ReturnUnusedPotionsToShelf();
        }

        private void ReturnUnusedPotionsToShelf()
        {
            var shelf = FindAnyObjectByType<PotionShelf>();
            foreach (var slot in stagingSlots)
            {
                if (slot == null || !slot.IsOccupied) continue;
                if (shelf != null && shelf.Add(slot.StoredPotion))
                {
                    slot.Clear();
                    CraftingSfx.Play(CraftingSfx.PotionPlace, 1f, 0.05f);
                }
            }
            EndBatch();
        }

        /// <summary>ผูกกับปุ่ม ▶ : ยิงยาขวดถัดไปของชุดยิงที่เริ่มด้วยคันโยกแล้ว</summary>
        public void FireNextPotion()
        {
            if (!stepByStepSend || !stepBatchActive || GetOccupiedSlotCount() == 0)
            {
                CraftingSfx.Play(CraftingSfx.PotionPlaceFail);
                NextRejected?.Invoke();
                return;
            }

            if (!SendNextPotion())
            {
                CraftingSfx.Play(CraftingSfx.PotionPlaceFail);
                NextRejected?.Invoke();
                return;
            }

            CraftingSfx.Play(CraftingSfx.NextShot);
            if (GetOccupiedSlotCount() == 0) EndBatch();
            else BeginAutoAdvance();
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
        /// - Require All Slots Filled: ยังใส่ไม่ครบทุกช่องที่ใช้งาน = ไม่ส่ง และช่องว่างกะพริบ
        /// - ส่งตั้งแต่ Min Potions For Sequence ขวดขึ้นไป: ส่งตามลำดับสวิตช์ (Switch Progression)
        /// - น้อยกว่านั้น: ส่งออกไปทำงานทันที ไม่ต้องรอสวิตช์
        /// </summary>
        public void SendPotionsToDropper()
        {
            if (isSendingSequentially)
            {
                Debug.Log("[PotionStagingManager] กำลังส่งยาตามลำดับอยู่ กรุณารอสักครู่");
                return;
            }

            if (stepByStepSend && stepBatchActive)
            {
                Debug.Log("[PotionStagingManager] อยู่กลางชุดยิงแล้ว ใช้ปุ่ม ▶ ยิงขวดถัดไป");
                CraftingSfx.Play(CraftingSfx.PotionPlaceFail);
                SendRejected?.Invoke();
                return;
            }

            int count = GetOccupiedSlotCount();
            if (count == 0)
            {
                Debug.Log("[PotionStagingManager] ไม่มีขวดยาในช่องพัก");
                stepBatchActive = false;
                CraftingSfx.Play(CraftingSfx.PotionPlaceFail);
                SendRejected?.Invoke();
                return;
            }

            if (requireAllSlotsFilled && !AllActiveSlotsFilled())
            {
                Debug.Log("[PotionStagingManager] ต้องใส่ยาให้ครบทุกช่องก่อนส่ง");
                CraftingSfx.Play(CraftingSfx.PotionPlaceFail);
                for (int i = 0; i < stagingSlots.Length; i++)
                    if (IsActiveSlot(i) && !stagingSlots[i].IsOccupied) stagingSlots[i].FlashEmpty();
                SendRejected?.Invoke();
                return;
            }

            if (stepByStepSend)
            {
                if (SendNextPotion())
                {
                    CraftingSfx.Play(CraftingSfx.SendPotion);
                    stepBatchActive = GetOccupiedSlotCount() > 0;
                    BeginAutoAdvance();
                    SendAccepted?.Invoke();
                }
                else
                {
                    CraftingSfx.Play(CraftingSfx.PotionPlaceFail);
                    SendRejected?.Invoke();
                }
                return;
            }

            CraftingSfx.Play(CraftingSfx.SendPotion);
            SendAccepted?.Invoke();

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
        /// โหมดทีละขวด: ยิงยาจากช่องแรกที่มียา 1 ขวด คืน false ถ้ายิงไม่ได้ (ปืนยังไม่ว่าง/ไม่มีปืนรับ)
        /// </summary>
        private bool SendNextPotion()
        {
            for (int i = 0; i < stagingSlots.Length; i++)
            {
                var slot = stagingSlots[i];
                if (slot == null || !slot.IsOccupied) continue;

                PotionType pType = ConvertToPotionType(slot.StoredPotion);
                if (pType == PotionType.None) continue;
                if (i >= droppers.Length || droppers[i] == null) continue;

                var dropper = droppers[i];
                if (!dropper.TryAddPotion(pType))
                {
                    Debug.Log($"[PotionStagingManager] Dropper ตัวที่ {i + 1} ยังไม่ว่าง รอสักครู่แล้วกดใหม่");
                    return false;
                }

                dropper.StartDispensing(ignorePrerequisite: dropper.IsPrerequisiteMet);
                slot.Clear();
                lastFiredIndex = i;
                Debug.Log($"[PotionStagingManager] ยิงยาช่องที่ {i + 1} ไปยัง {dropper.name}");
                return true;
            }
            return false;
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
                    // ถ้า Dropper มีสวิตช์เงื่อนไข และสวิตช์ยังไม่ติด ให้ใส่ยาไว้ในช่องแล้วรอสวิตช์ (ignorePrerequisite: false)
                    bool canDispenseNow = dropper.IsPrerequisiteMet;
                    dropper.StartDispensing(ignorePrerequisite: canDispenseNow);
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

                    // 2.5 ถ้า Dropper ตัวนี้มีสวิตช์เงื่อนไข (PrerequisiteSwitch) ที่ยังไม่ถูกกด ให้รอจนกว่าสวิตช์เงื่อนไขจะถูกกดก่อน
                    if (dropper.PrerequisiteSwitch != null && !dropper.PrerequisiteSwitch.IsActive && !triggeredSwitches.Contains(dropper.PrerequisiteSwitch))
                    {
                        if (!watchedSwitches.Contains(dropper.PrerequisiteSwitch))
                        {
                            watchedSwitches.Add(dropper.PrerequisiteSwitch);
                            dropper.PrerequisiteSwitch.StateChanged += onSwitchChanged;
                        }

                        Debug.Log($"[PotionStagingManager] ขั้นตอนที่ {i + 1} ({dropper.name}) กำลังรอสวิตช์เงื่อนไข {dropper.PrerequisiteSwitch.name}...");
                        float prereqTimer = 0f;
                        while (prereqTimer < stageTimeout)
                        {
                            if (dropper.PrerequisiteSwitch.IsActive || triggeredSwitches.Contains(dropper.PrerequisiteSwitch))
                            {
                                Debug.Log($"[PotionStagingManager] สวิตช์เงื่อนไข {dropper.PrerequisiteSwitch.name} ถูกกดแล้ว! ปลดล็อคให้ {dropper.name} ปล่อยยาได้");
                                break;
                            }
                            prereqTimer += Time.deltaTime;
                            yield return null;
                        }
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
                        // ปล่อยยา (ถ้าสวิตช์เงื่อนไขยังไม่ติด ให้รอสวิตช์ ไม่สั่งข้าม)
                        bool canDispense = dropper.IsPrerequisiteMet || (dropper.PrerequisiteSwitch != null && triggeredSwitches.Contains(dropper.PrerequisiteSwitch));
                        dropper.StartDispensing(ignorePrerequisite: canDispense);
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

                            // สัตว์ใช้ความสามารถเสร็จแล้วและหยุดนิ่ง (เช่น Strong cat ได้ยา Havoc ทุบของแล้วหยุด ไม่ได้ไปกดสวิตช์)
                            // ไม่ต้องรอสวิตช์จนครบ Stage Timeout ให้ส่งยาขั้นต่อไปได้เลย
                            var shotAnimal = dropper.TargetAnimal;
                            if (!dropper.IsDispensing && shotAnimal != null && !shotAnimal.IsAutoRunning && !shotAnimal.IsMoving)
                            {
                                Debug.Log($"[PotionStagingManager] {shotAnimal.name} ทำงานเสร็จและหยุดนิ่งแล้ว ปลดล็อคขั้นตอนถัดไปโดยไม่รอสวิตช์");
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
            // สวิตช์ประจำขั้นต้องมีอย่างน้อยเท่าจำนวนช่องของด่าน (3 หรือ 4 ช่อง)
            int stageCount = Mathf.Max(3, stagingSlots != null ? stagingSlots.Length : 0);
            if (stageSwitches == null || stageSwitches.Length < stageCount)
            {
                Array.Resize(ref stageSwitches, stageCount);
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

        /// <summary>ช่องนี้ใช้งานจริงไหม (มีช่อง และผูก Dropper ที่มีสัตว์เป้าหมายแล้ว)</summary>
        private bool IsActiveSlot(int i)
        {
            return stagingSlots != null && i < stagingSlots.Length && stagingSlots[i] != null
                && droppers != null && i < droppers.Length && droppers[i] != null && droppers[i].TargetAnimal != null;
        }

        /// <summary>ทุกช่องที่ใช้งานจริงมียาครบหรือยัง</summary>
        private bool AllActiveSlotsFilled()
        {
            for (int i = 0; i < stagingSlots.Length; i++)
                if (IsActiveSlot(i) && !stagingSlots[i].IsOccupied) return false;
            return true;
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