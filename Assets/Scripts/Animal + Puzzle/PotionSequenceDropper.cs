using System.Collections;
using UnityEngine;

/// <summary>
/// เครื่องปล่อยยาตามลำดับ (แทน PotionDropper ตัวเก่า):
/// - มีช่องใส่ยาหลายช่อง (Slots) เรียงลำดับ ช่อง 1 -> 2 -> 3
/// - ผู้เล่นปรุงยาให้ถูกชนิด แล้วใส่เข้าช่องตามลำดับ (ระบบปรุงยาของเพื่อนเรียก TryAddPotion)
/// - พอช่องเต็ม (หรือกดปุ่มเริ่ม) ยาจะถูกปล่อยออกมาทีละตัวตามลำดับช่อง
///   ยาแต่ละตัวลอยไปหาสัตว์ที่ใช้ยาชนิดนั้น แล้วสัตว์ทำงานเอง
/// - รอให้สัตว์ตัวนั้นทำเสร็จก่อน ค่อยปล่อยยาตัวถัดไป (ลำดับถึงจะมีผลกับ puzzle)
/// </summary>
public class PotionSequenceDropper : MonoBehaviour
{
    #region Links
    [Header("Links")]
    [Tooltip("ช่องใส่ยาเรียงตามลำดับที่จะปล่อย (ช่อง 1 ปล่อยก่อน)")]
    [SerializeField] private PotionSlot[] slots;

    [Tooltip("สัตว์ทั้งหมดในด่าน ยาจะไปหาสัตว์ที่ Required Potion ตรงกับชนิดยา")]
    [SerializeField] private TestAnimal[] animals;

    [Tooltip("ลาก Prefab ยาที่มี PotionProjectile.cs มาใส่")]
    [SerializeField] private PotionProjectile potionPrefab;

    [Tooltip("จุดที่ยาโผล่ออกมา (ว่างไว้ = ตำแหน่งของเครื่องนี้)")]
    [SerializeField] private Transform dropPoint;

    [Tooltip("(ไม่บังคับ) จุดที่ยาต้องผ่านตามลำดับก่อนถึงสัตว์ เช่น ปลายราง")]
    [SerializeField] private Transform[] pathPoints;
    #endregion

    #region Settings
    [Header("Settings")]
    [Tooltip("ติ๊กไว้ = ช่องเต็มทุกช่องแล้วเริ่มปล่อยเอง / ไม่ติ๊ก = รอเรียก StartDispensing() (เช่น ผูกกับปุ่ม)")]
    [SerializeField] private bool autoStartWhenFull = true;

    [Tooltip("หน่วงกี่วินาทีก่อนปล่อยยาแต่ละตัว")]
    [SerializeField] private float dropInterval = 0.5f;

    [Tooltip("ความเร็วที่ยาลอยไป")]
    [SerializeField] private float potionSpeed = 4f;

    [Tooltip("ติ๊กไว้ = รอให้สัตว์ทำงานเสร็จก่อนปล่อยยาตัวถัดไป")]
    [SerializeField] private bool waitForAnimalToFinish = true;

    [Tooltip("รอสัตว์ทำเสร็จนานสุดกี่วินาที (กันค้าง ถ้าสัตว์ติดอะไรสักอย่าง)")]
    [SerializeField] private float finishTimeout = 20f;
    #endregion

    #region Switch Progression
    [Header("Switch Progression (ใช้สวิตช์ปลดล็อคขั้นตอน)")]
    [Tooltip("สวิตช์เงื่อนไขก่อนปล่อยยา: ต้องถูกกดก่อนถึงจะเริ่มปล่อยยาตัวนี้ (ถ้าไม่ใส่ = ปล่อยได้ทันที)")]
    [SerializeField] private FloorSwitch prerequisiteSwitch;

    [Tooltip("ติ๊กไว้ = เมื่อ prerequisiteSwitch ถูกกด จะเริ่มปล่อยยาอัตโนมัติทันที (ถ้ามียาในช่อง)")]
    [SerializeField] private bool autoStartOnPrerequisiteSwitch = false;

    [Tooltip("สวิตช์เป้าหมายที่สัตว์ตัวนี้ต้องไปกด: เมื่อสวิตช์นี้ Active ถือว่าเสร็จทันที ตัดจบการรอ ไม่ต้องรอนาน")]
    [SerializeField] private FloorSwitch targetSwitch;

    [Tooltip("(กรณีเครื่องนี้มีหลายช่อง) สวิตช์เป้าหมายแยกแต่ละช่องตามลำดับ 1, 2, 3")]
    [SerializeField] private FloorSwitch[] targetSwitches;

    [Tooltip("เวลารอสูงสุดกรณีมีสวิตช์เป้าหมาย (วินาที) ถ้าไม่โดนสวิตช์ในเวลานี้จะตัดจบ")]
    [SerializeField] private float switchWaitTimeout = 8f;
    #endregion

    #region State
    public bool IsDispensing { get; private set; }
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        foreach (var s in slots)
            if (s != null) s.PotionInserted += OnSlotInserted;

        if (prerequisiteSwitch != null)
            prerequisiteSwitch.StateChanged += OnPrerequisiteSwitchChanged;
    }

    private void OnDisable()
    {
        foreach (var s in slots)
            if (s != null) s.PotionInserted -= OnSlotInserted;

        if (prerequisiteSwitch != null)
            prerequisiteSwitch.StateChanged -= OnPrerequisiteSwitchChanged;
    }

    private void OnPrerequisiteSwitchChanged(FloorSwitch sw)
    {
        if (autoStartOnPrerequisiteSwitch && sw != null && sw.IsActive && !IsDispensing)
        {
            bool hasPotion = false;
            foreach (var s in slots)
            {
                if (s != null && s.IsOccupied) { hasPotion = true; break; }
            }
            if (hasPotion)
            {
                Debug.Log($"[{name}] สวิตช์เงื่อนไข {sw.name} ทำงานแล้ว เริ่มปล่อยยาอัตโนมัติ");
                StartDispensing();
            }
        }
    }
    #endregion

    #region API
    /// <summary>ใส่ยาลงช่องว่างช่องแรก คืน true ถ้าใส่ได้ (ระบบปรุงยาของเพื่อนเรียกตัวนี้)</summary>
    public bool TryAddPotion(PotionType potion)
    {
        if (IsDispensing || potion == PotionType.None) return false;

        foreach (var s in slots)
        {
            if (s == null || s.IsOccupied) continue;
            return s.TryInsert(potion);
        }
        return false; // ช่องเต็มหมดแล้ว
    }

    /// <summary>เริ่มปล่อยยาตามลำดับ (ignorePrerequisite = true คือสั่งปล่อยทันทีโดยไม่เช็คสวิตช์เงื่อนไขก่อนหน้า)</summary>
    public void StartDispensing(bool ignorePrerequisite = false)
    {
        if (IsDispensing) return;
        StartCoroutine(DispenseRoutine(ignorePrerequisite));
    }

    /// <summary>เคลียร์ทุกช่อง (เช่น ผู้เล่นกดรีเซ็ต หรือทิ้งยาลงถังขยะ) ทำไม่ได้ตอนกำลังปล่อย</summary>
    public void ClearAllSlots()
    {
        if (IsDispensing) return;
        foreach (var s in slots)
            if (s != null) s.Clear();
    }
    #endregion

    #region Logic
    private void OnSlotInserted(PotionSlot slot, PotionType potion)
    {
        if (!autoStartWhenFull || IsDispensing) return;

        foreach (var s in slots)
            if (s != null && !s.IsOccupied) return; // ยังมีช่องว่าง รอก่อน

        StartDispensing();
    }

    
    /// <summary>ใส่ยาลงในช่อง Dropper ตาม Index ที่ระบุโดยตรง (0, 1, 2)</summary>
    public bool TryInsertToSlot(int index, PotionType potion)
    {
        if (IsDispensing || potion == PotionType.None) return false;
        if (slots == null || index < 0 || index >= slots.Length) return false;
        if (slots[index] == null || slots[index].IsOccupied) return false;

        return slots[index].TryInsert(potion);
    }

    /// <summary>สวิตช์เงื่อนไขพร้อมทำงานแล้วหรือไม่</summary>
    public bool IsPrerequisiteMet => prerequisiteSwitch == null || prerequisiteSwitch.IsActive;
    public FloorSwitch PrerequisiteSwitch => prerequisiteSwitch;
    public FloorSwitch TargetSwitch => targetSwitch;

    /// <summary>มีสัตว์ในเครื่องนี้ที่ต้องการยานี้หรือไม่</summary>
    public bool CanAcceptPotion(PotionType type) => FindAnimalFor(type) != null;
    
    private IEnumerator DispenseRoutine(bool ignorePrerequisite = false)
    {
        IsDispensing = true;

        // ถ้ามีสวิตช์เงื่อนไขก่อนหน้า และไม่ได้สั่งข้าม แต่สวิตช์ยังไม่ถูกกด -> ไม่ปล่อยยา และไม่ปล่อยให้ผู้เล่นรอนาน
        if (!ignorePrerequisite && prerequisiteSwitch != null && !prerequisiteSwitch.IsActive)
        {
            Debug.LogWarning($"[{name}] สวิตช์เงื่อนไข ({prerequisiteSwitch.name}) ยังไม่ถูกกด! ข้ามขั้นตอนนี้ไปก่อนเพื่อไม่ให้รอนาน (ยาคงอยู่ในช่อง)");
            IsDispensing = false;
            yield break;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.IsOccupied) continue;

            PotionType type = slot.StoredPotion;
            TestAnimal animal = FindAnimalFor(type);

            yield return new WaitForSeconds(dropInterval);

            slot.Clear(); // ยาออกจากช่องแล้ว

            if (animal == null)
            {
                Debug.LogWarning($"[PotionSequenceDropper] ไม่มีสัตว์ที่ใช้ยา {type} ในลิสต์ Animals ยาเสียเปล่า");
                continue;
            }

            Vector3 pos = (dropPoint != null) ? dropPoint.position : transform.position;
            PotionProjectile p = Instantiate(potionPrefab, pos, Quaternion.identity);
            p.Init(type, animal, pathPoints, potionSpeed);

            if (!waitForAnimalToFinish) continue;

            // รอให้ยาถึงตัวสัตว์ (ยาจะถูกทำลายตอนถึง)
            float flyTimer = 0f;
            while (p != null && flyTimer < finishTimeout)
            {
                flyTimer += Time.deltaTime;
                yield return null;
            }

            // หาสวิตช์เป้าหมายของรอบนี้ (ถ้ามี)
            FloorSwitch curTarget = (targetSwitches != null && i < targetSwitches.Length && targetSwitches[i] != null)
                ? targetSwitches[i]
                : targetSwitch;

            float waitTimer = 0f;
            float maxWait = (curTarget != null) ? switchWaitTimeout : finishTimeout;

            // รอให้สัตว์ทำงานเสร็จ หรือ สวิตช์เป้าหมายถูกกด (อย่างใดอย่างหนึ่งถึงก่อน จบการรอทันที)
            while (waitTimer < maxWait)
            {
                // 1. ถ้ามีสวิตช์เป้าหมาย และสวิตช์นั้นถูกกดแล้ว -> สำเร็จทันที! ตัดจบการรอ ไม่ต้องรอนาน
                if (curTarget != null && curTarget.IsActive)
                {
                    Debug.Log($"[{name}] สวิตช์เป้าหมาย ({curTarget.name}) ถูกกดแล้ว! สำเร็จขั้นตอนทันที");
                    break;
                }

                // 2. ถ้าสัตว์หยุดทำงานและหยุดขยับแล้ว (ทำเสร็จ หรือติดสิ่งกีดขวาง) -> ตัดจบ ไม่ต้องรอ timeout นาน
                if (!animal.IsAutoRunning && !animal.IsMoving)
                {
                    break;
                }

                waitTimer += Time.deltaTime;
                yield return null;
            }
        }

        IsDispensing = false;
    }

    public TestAnimal FindAnimalFor(PotionType type)
    {
        foreach (var a in animals)
            if (a != null && a.RequiredPotionType == type) return a;
        return null;
    }
    #endregion
}
