using UnityEngine;

/// <summary>
/// เครื่องปล่อยยา 1 เครื่อง (ต่อ 1 ช่องพัก)
///
/// PotionStagingManager สั่ง LaunchPotion ทีละชิ้นตามลำดับใน PotionSequence
/// ยาแต่ละชิ้นลอยไปหาสัตว์ที่ใช้ยาชนิดนั้น แล้วสัตว์ทำงานเอง
/// IsBusy บอกว่าสัตว์ยังทำงานอยู่ไหม เพื่อให้ลำดับรอจนชิ้นนี้ติดก่อน
/// </summary>
public class PotionSequenceDropper : MonoBehaviour
{
    #region Links
    [Header("Links")]
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
    [Tooltip("ความเร็วที่ยาลอยไป")]
    [SerializeField] private float potionSpeed = 4f;
    #endregion

    #region State
    private PotionProjectile activeProjectile;
    private TestAnimal activeAnimal;

    /// <summary>ยังมียาลอยอยู่ หรือสัตว์ตัวที่รับยาไปยังทำงานอยู่ (ลำดับใช้รอให้ชิ้นนี้ติดก่อน)</summary>
    public bool IsBusy => activeProjectile != null || (activeAnimal != null && activeAnimal.IsAutoRunning);
    #endregion

    #region API
    /// <summary>ปล่อยยา 1 ชิ้นไปหาสัตว์ที่ใช้ยาชนิดนี้ (ใช้โดย PotionStagingManager) คืน false ถ้าปล่อยไม่ได้</summary>
    public bool LaunchPotion(PotionType type)
    {
        if (type == PotionType.None || potionPrefab == null) return false;

        TestAnimal animal = FindAnimalFor(type);
        if (animal == null)
        {
            Debug.LogWarning($"[PotionSequenceDropper] ไม่มีสัตว์ที่ใช้ยา {type} ในลิสต์ Animals ยาเสียเปล่า");
            return false;
        }

        Vector3 pos = (dropPoint != null) ? dropPoint.position : transform.position;
        PotionProjectile p = Instantiate(potionPrefab, pos, Quaternion.identity);
        p.Init(type, animal, pathPoints, potionSpeed);

        activeProjectile = p;
        activeAnimal = animal;
        return true;
    }
    #endregion

    #region Helpers
    private TestAnimal FindAnimalFor(PotionType type)
    {
        foreach (var a in animals)
            if (a != null && a.RequiredPotionType == type) return a;
        return null;
    }
    #endregion
}
