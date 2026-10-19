using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// ปืนยิงเข็ม (แทนช่องส่งยาแบบเดิม): ขายึดอยู่นิ่ง ตัวถังหมุนหัวไปเล็งสัตว์เป้าหมาย
/// พอเล็งตรงแล้วจึงยิงเข็มออกไป สีตัวถัง = สีช่องพักยาช่องเดียวกัน สีเข็ม = ชนิดยา
/// PotionSequenceDropper เรียก AimAndFire() แล้วรอจนเข็มถึงตัวสัตว์
/// </summary>
public class DartGun : MonoBehaviour
{
    [Serializable]
    public class DartSprite
    {
        public PotionType type;
        public Sprite sprite;
    }

    #region Parts
    [Header("Parts")]
    [Tooltip("จุดหมุนของตัวถัง (วางไว้ที่ปลายขายึด) ตัวถังเป็นลูกของจุดนี้")]
    [SerializeField] private Transform aimPivot;

    [Tooltip("สไปรต์ตัวถัง (สีตามช่องพักยา)")]
    [SerializeField] private SpriteRenderer barrelRenderer;

    [Tooltip("ปลายกระบอกที่เข็มโผล่ออกมา (ว่างไว้ = คำนวณจากจุดหมุน)")]
    [SerializeField] private Transform muzzle;
    #endregion

    #region Aim
    [Header("Aim")]
    [Tooltip("มุม (องศา) ของ 'ทิศปลายกระบอก' ในรูปสไปรต์ที่ยังไม่หมุน (รูปปัจจุบันปลายชี้ลงเฉียงซ้าย ≈ -115) ถ้าเล็งเพี้ยนให้ปรับตัวนี้")]
    [SerializeField] private float spriteForwardAngle = -115f;

    [Tooltip("ความเร็วหมุนตัวถัง (องศา/วินาที)")]
    [SerializeField] private float aimSpeed = 260f;

    [Tooltip("คลาดเคลื่อนได้กี่องศาถึงถือว่าเล็งตรงแล้วยิง")]
    [SerializeField] private float fireAngleTolerance = 3f;

    [Tooltip("เล็งนานสุดกี่วินาที ถ้าไม่ตรงก็ยิงเลย (กันค้าง)")]
    [SerializeField] private float aimTimeout = 3f;

    [Tooltip("หน่วงหลังเล็งตรงก่อนยิง (วินาที)")]
    [SerializeField] private float fireDelay = 0.12f;

    [Tooltip("ระยะถอยหลังของตัวถังตอนยิง (หน่วย)")]
    [SerializeField] private float recoilDistance = 0.12f;
    #endregion

    #region Dart
    [Header("Dart")]
    [Tooltip("สไปรต์เข็มของยาแต่ละชนิด")]
    [SerializeField] private DartSprite[] dartSprites;

    [Tooltip("มุมทิศปลายเข็มในรูปสไปรต์ที่ยังไม่หมุน")]
    [SerializeField] private float dartSpriteForwardAngle = -115f;

    [SerializeField] private float dartScale = 0.5f;

    [Tooltip("ห่างจากเป้าเท่านี้ถือว่าเข็มถึงแล้ว")]
    [SerializeField] private float dartArriveDistance = 0.3f;

    [Tooltip("ปลายเข็มที่ยื่นพ้นปากกระบอกตอนบรรจุยา (หน่วยของจุดหมุน) ยิ่งมากยิ่งยื่นยาว")]
    [SerializeField] private float needleProtrusion = 0.7f;
    #endregion

    /// <summary>เข็มที่เพิ่งยิงออกไปล่าสุด (null ถ้าถึงเป้าและถูกทำลายแล้ว)</summary>
    public PotionProjectile LastDart { get; private set; }

    private Vector3 barrelRestLocalPos;
    private SpriteRenderer loadedNeedle;

    private void Awake()
    {
        if (aimPivot == null) aimPivot = transform;
        if (barrelRenderer != null) barrelRestLocalPos = barrelRenderer.transform.localPosition;
        CreateLoadedNeedle();
    }

    /// <summary>บรรจุเข็มชนิด type เข้าปืน: เข็มซ่อนอยู่หลังตัวถัง เห็นแค่ปลายเข็มยื่นออกมาที่ปากกระบอก</summary>
    public void Load(PotionType type)
    {
        Sprite s = FindDartSprite(type);
        if (s == null) { Unload(); return; }

        float k = dartScale / Mathf.Max(0.0001f, aimPivot.lossyScale.x);
        loadedNeedle.sprite = s;
        loadedNeedle.transform.localScale = Vector3.one * k;

        float a = dartSpriteForwardAngle * Mathf.Deg2Rad;
        Vector2 fwd = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        Vector3 e = s.bounds.extents;
        float half = (e.x * Mathf.Abs(fwd.x) + e.y * Mathf.Abs(fwd.y)) * 0.85f * k;
        Vector3 muzzleLocal = muzzle != null ? aimPivot.InverseTransformPoint(muzzle.position) : (Vector3)(fwd * 1.5f);
        loadedNeedle.transform.localPosition = muzzleLocal + (Vector3)(fwd * (needleProtrusion - half));
        loadedNeedle.enabled = true;
    }

    /// <summary>เอาเข็มออกจากปืน (เช่น เคลียร์ช่อง)</summary>
    public void Unload()
    {
        if (loadedNeedle != null) loadedNeedle.enabled = false;
    }

    private void CreateLoadedNeedle()
    {
        var go = new GameObject("LoadedNeedle");
        go.transform.SetParent(aimPivot, false);
        // ทิศปลายเข็มในรูปเท่ากับทิศปลายตัวถัง จึงหมุนตามจุดหมุนได้เลย
        loadedNeedle = go.AddComponent<SpriteRenderer>();
        if (barrelRenderer != null)
        {
            loadedNeedle.sortingLayerID = barrelRenderer.sortingLayerID;
            loadedNeedle.sortingOrder = barrelRenderer.sortingOrder - 1; // อยู่หลังตัวถัง
        }
        loadedNeedle.enabled = false;
    }

    /// <summary>หมุนตัวถังไปหาสัตว์ เมื่อเล็งตรงแล้วยิงเข็มชนิด type ออกไป (เรียกด้วย yield return)</summary>
    public IEnumerator AimAndFire(PotionType type, TestAnimal target, float dartSpeed)
    {
        LastDart = null;
        if (target == null) yield break;

        float timer = 0f;
        while (timer < aimTimeout && target != null)
        {
            float want = AngleTo(target.transform.position);
            float cur = aimPivot.eulerAngles.z;
            float next = Mathf.MoveTowardsAngle(cur, want, aimSpeed * Time.deltaTime);
            aimPivot.rotation = Quaternion.Euler(0f, 0f, next);

            if (Mathf.Abs(Mathf.DeltaAngle(next, want)) <= fireAngleTolerance) break;

            timer += Time.deltaTime;
            yield return null;
        }

        if (target == null) yield break;

        // เล็งตรงแล้ว: ล็อกมุมให้ตรงเป้าพอดีก่อนยิง
        aimPivot.rotation = Quaternion.Euler(0f, 0f, AngleTo(target.transform.position));
        if (fireDelay > 0f) yield return new WaitForSeconds(fireDelay);
        if (target == null) yield break;

        aimPivot.rotation = Quaternion.Euler(0f, 0f, AngleTo(target.transform.position));
        LastDart = SpawnDart(type, target, dartSpeed);
        StartCoroutine(RecoilRoutine());
    }

    private float AngleTo(Vector3 worldPos)
    {
        Vector2 dir = worldPos - MuzzlePosition();
        if (dir.sqrMagnitude < 0.0001f) return aimPivot.eulerAngles.z;
        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - spriteForwardAngle;
    }

    private Vector3 MuzzlePosition() => muzzle != null ? muzzle.position : aimPivot.position;

    private PotionProjectile SpawnDart(PotionType type, TestAnimal target, float speed)
    {
        var go = new GameObject("Dart_" + type);
        // เข็มที่บิน เริ่มจากตำแหน่งเดียวกับเข็มที่โผล่อยู่ที่ปากกระบอก จะได้ต่อเนื่อง
        bool wasLoaded = loadedNeedle != null && loadedNeedle.enabled;
        go.transform.position = wasLoaded ? loadedNeedle.transform.position : MuzzlePosition();
        go.transform.localScale = Vector3.one * dartScale;
        Unload();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = FindDartSprite(type);
        if (barrelRenderer != null)
        {
            sr.sortingLayerID = barrelRenderer.sortingLayerID;
            sr.sortingOrder = barrelRenderer.sortingOrder + 1;
        }

        var dart = go.AddComponent<PotionProjectile>();
        dart.Init(type, target, null, speed);
        dart.ConfigureDart(dartSpriteForwardAngle, dartArriveDistance);
        return dart;
    }

    private Sprite FindDartSprite(PotionType type)
    {
        if (dartSprites != null)
            foreach (var d in dartSprites)
                if (d != null && d.type == type) return d.sprite;
        return null;
    }

    private IEnumerator RecoilRoutine()
    {
        if (barrelRenderer == null || recoilDistance <= 0f) yield break;

        Transform b = barrelRenderer.transform;
        Vector3 back = b.parent != null
            ? b.parent.InverseTransformDirection(-ForwardWorld())
            : -ForwardWorld();
        Vector3 from = barrelRestLocalPos + back * recoilDistance;

        float t = 0f;
        const float duration = 0.18f;
        while (t < duration)
        {
            t += Time.deltaTime;
            b.localPosition = Vector3.Lerp(from, barrelRestLocalPos, t / duration);
            yield return null;
        }
        b.localPosition = barrelRestLocalPos;
    }

    private Vector3 ForwardWorld()
    {
        float a = (spriteForwardAngle + aimPivot.eulerAngles.z) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
    }
}
