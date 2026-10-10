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

    #region State
    public bool IsDispensing { get; private set; }
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        foreach (var s in slots)
            if (s != null) s.PotionInserted += OnSlotInserted;
    }

    private void OnDisable()
    {
        foreach (var s in slots)
            if (s != null) s.PotionInserted -= OnSlotInserted;
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

    /// <summary>เริ่มปล่อยยาตามลำดับ (เรียกจากปุ่ม UI ได้ถ้าไม่ใช้ Auto Start)</summary>
    public void StartDispensing()
    {
        if (IsDispensing) return;
        StartCoroutine(DispenseRoutine());
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

    private IEnumerator DispenseRoutine()
    {
        IsDispensing = true;

        foreach (var slot in slots)
        {
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
            float timer = 0f;
            while (p != null && timer < finishTimeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // รอให้สัตว์ทำงานเสร็จ
            while (animal.IsAutoRunning && timer < finishTimeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }

        IsDispensing = false;
    }

    private TestAnimal FindAnimalFor(PotionType type)
    {
        foreach (var a in animals)
            if (a != null && a.RequiredPotionType == type) return a;
        return null;
    }
    #endregion
}
