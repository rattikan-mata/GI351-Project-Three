using System.Collections;
using System.Collections.Generic;
using PotionSystem;
using UnityEngine;

/// <summary>
/// ปุ่มรีเซ็ตพัซเซิล (ผูก ResetPuzzle() กับปุ่ม RESTRT): เริ่มพัซเซิลใหม่โดยไม่โหลดซีนซ้ำ
/// - ตอนเริ่มเกม เก็บสำเนาต้นฉบับของวัตถุพัซเซิล (สัตว์ กล่อง ประตู สวิตช์ ฯลฯ) ไว้
/// - ตอนรีเซ็ต ทิ้งของที่เล่นไปแล้วทั้งหมด (รวมของที่พัง/ถูกทำลาย) แล้วสร้างจากสำเนาต้นฉบับใหม่
/// - รีเซ็ตเฉพาะพัซเซิล: ของนอกพัซเซิล (ชั้นขวดยา ช่องเตรียมยิง น้องนักปรุงยา) ไม่ถูกแตะ ส่วนยาที่ส่งไปแล้วไม่คืน
/// </summary>
[DefaultExecutionOrder(-10000)]
public class PuzzleResetManager : MonoBehaviour
{
    [Header("Puzzle Objects")]
    [Tooltip("วัตถุราก (Root ของ Hierarchy) ที่เป็นส่วนของพัซเซิลและต้องรีเซ็ต เช่น Puzzle Level และ Door ต้องเป็นวัตถุที่อยู่นอกสุดของฉาน")]
    [SerializeField] private Transform[] puzzleRoots;

    [Header("Systems To Reset")]
    [SerializeField] private PotionStagingManager stagingManager;
    [SerializeField] private PotionSequenceDropper[] droppers;

    private GameObject template;
    private readonly List<Transform> liveRoots = new List<Transform>();
    private bool resetting;

    public bool IsResetting => resetting;

    /// <summary>จำนวนครั้งที่กดรีเซ็ตพัซเซิลในรอบนี้ (WinZone ใช้นับจำนวนครั้งที่ลองไว้คิดดาว)</summary>
    public int ResetCount { get; private set; }

    #region Setup
    private void Awake()
    {
        // เก็บสำเนาต้นฉบับ: ย้ายรากทั้งหมดเข้าภาชนะที่ปิดอยู่ แล้วโคลนภาชนะ (โคลนจึงไม่ทำงาน)
        // การโคลนทั้งก้อนช่วยให้การอ้างอิงข้ามกันระหว่างราก (เช่น ประตูอ้างสวิตช์) ชี้ไปที่สำเนาด้วยกันเอง
        var container = new GameObject("PuzzleTemplate");
        container.SetActive(false);

        var oldParents = new List<Transform>();
        foreach (var root in puzzleRoots)
        {
            if (root == null) continue;
            oldParents.Add(root.parent);
            root.SetParent(container.transform, true);
        }

        template = Instantiate(container);
        template.name = "PuzzleTemplate (pristine)";
        template.transform.SetParent(transform, false);

        int i = 0;
        foreach (var root in puzzleRoots)
        {
            if (root == null) continue;
            root.SetParent(oldParents[i++], true);
            liveRoots.Add(root);
        }
        Destroy(container);
    }
    #endregion

    #region API
    /// <summary>ผูกกับปุ่มรีเซ็ต</summary>
    public void ResetPuzzle()
    {
        if (resetting) return;
        ResetCount++;
        StartCoroutine(ResetRoutine());
    }
    #endregion

    #region Logic
    private IEnumerator ResetRoutine()
    {
        resetting = true;

        // 1) หยุดระบบส่งยา/ปืน แล้วเคลียร์เข็มและของที่พัซเซิลสร้างขึ้นมา
        if (stagingManager != null) stagingManager.ResetState();
        foreach (var d in droppers)
            if (d != null) d.ResetState();

        foreach (var dart in FindObjectsByType<PotionProjectile>())
            Destroy(dart.gameObject);
        foreach (var ball in FindObjectsByType<Hairball>())
            Destroy(ball.gameObject);

        // 2) ทิ้งพัซเซิลเดิม (Destroy ทำตอนจบเฟรม จึงรอ 1 เฟรม)
        foreach (var root in liveRoots)
            if (root != null) Destroy(root.gameObject);
        liveRoots.Clear();
        yield return null;

        // 3) สร้างพัซเซิลใหม่จากสำเนาต้นฉบับ แล้วเอาออกจากภาชนะมาไว้ที่รากของฉาน
        var fresh = Instantiate(template);
        fresh.name = "PuzzleFresh";
        fresh.transform.SetParent(null, true);
        fresh.SetActive(true);

        var children = new List<Transform>();
        foreach (Transform c in fresh.transform) children.Add(c);
        foreach (var c in children)
        {
            c.SetParent(null, true);
            liveRoots.Add(c);
        }
        Destroy(fresh);
        yield return null; // ให้ Awake/Start ของของใหม่ทำงานก่อน

        // 4) ผูกการอ้างอิงที่ชี้มายังของใหม่ใหม่อีกครั้ง
        if (AnimalSelectionManager.Instance != null) AnimalSelectionManager.Instance.RefreshAnimals();
        foreach (var d in droppers)
            if (d != null) d.RebindTargets();
        if (stagingManager != null) stagingManager.RebindStageSwitches();

        resetting = false;
    }
    #endregion
}
