using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// โซนชนะ: เมื่อกล่องรางวัล (หีบ) ตกลงมานอนนิ่งในโซนนี้ครบเวลาที่ตั้ง จะเรียกหน้าจอชนะ (EndGameUIController.ShowWinScreen)
/// - วางบนวัตถุที่มี Collider2D ครอบช่องรับรางวัล (จะถูกตั้งเป็น Trigger ให้เอง)
/// - หากล่องจากชื่อ (ขึ้นต้นด้วย Target Box Name) ไม่ผูกกับวัตถุในฉาก เพื่อให้ยังใช้ได้หลังกดรีเซ็ตพัซเซิลที่สร้างกล่องใหม่
/// - ตรวจทุกรอบฟิสิกส์เอง (ไม่พึ่ง OnTriggerStay เพราะกล่องที่นอนนิ่งอาจหลับแล้วไม่ส่งเหตุการณ์)
/// - วัตถุนี้ ตัวจัดการรีเซ็ต และหน้าจอชนะ ต้องอยู่นอกกลุ่มพัซเซิลที่ถูกรีเซ็ต
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WinZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EndGameUIController endGame;
    [Tooltip("(ไม่บังคับ) ใช้นับจำนวนครั้งที่ลอง (กดรีเซ็ต + 1) ไว้คิดดาว ว่าง = นับเป็นลองครั้งแรกเสมอ")]
    [SerializeField] private PuzzleResetManager resetManager;

    [Header("Win Condition")]
    [Tooltip("ชื่อกล่องรางวัล (นับชื่อที่ขึ้นต้นด้วยข้อความนี้ เช่น Chest หรือ Chest(Clone))")]
    [SerializeField] private string targetBoxName = "Chest";

    [Tooltip("กล่องต้องนอนนิ่งในโซนนานเท่านี้ (วินาที) ถึงจะชนะ")]
    [SerializeField, Min(0f)] private float settleSeconds = 0.5f;

    [Tooltip("ขยับน้อยกว่านี้ต่อรอบฟิสิกส์ (หน่วย Unity) ถือว่านิ่ง")]
    [SerializeField, Min(0f)] private float stillDistance = 0.005f;

    private Collider2D zone;
    private readonly List<Collider2D> hits = new List<Collider2D>();
    private PushableBox tracked;
    private Vector2 lastPosition;
    private float stillTimer;
    private bool won;

    public bool HasWon => won;

    private void Awake()
    {
        zone = GetComponent<Collider2D>();
        zone.isTrigger = true;
    }

    private void FixedUpdate()
    {
        if (won) return;

        PushableBox found = FindBoxInZone();
        if (found == null)
        {
            tracked = null;
            stillTimer = 0f;
            return;
        }

        Vector2 pos = found.transform.position;
        if (tracked != found)
        {
            tracked = found;
            lastPosition = pos;
            stillTimer = 0f;
            return;
        }

        bool still = (pos - lastPosition).sqrMagnitude <= stillDistance * stillDistance && !found.IsMoving;
        lastPosition = pos;
        stillTimer = still ? stillTimer + Time.fixedDeltaTime : 0f;

        if (stillTimer >= settleSeconds) Win();
    }

    // กล่องรางวัลที่กึ่งกลางอยู่ในโซนจริง (ไม่ใช่แค่ขอบแตะ)
    private PushableBox FindBoxInZone()
    {
        var filter = new ContactFilter2D();
        filter.NoFilter();
        zone.Overlap(filter, hits);

        foreach (var h in hits)
        {
            if (h == null) continue;
            var box = h.GetComponentInParent<PushableBox>();
            if (box == null || box.IsBroken || !box.name.StartsWith(targetBoxName)) continue;
            if (zone.OverlapPoint(box.transform.position)) return box;
        }
        return null;
    }

    private void Win()
    {
        won = true;
        int attempts = resetManager != null ? resetManager.ResetCount + 1 : 1;
        Debug.Log($"[WinZone] ชนะ! หีบนอนนิ่งในช่องรางวัล (ลองครั้งที่ {attempts})");
        if (endGame != null) endGame.ShowWinScreen(attempts);
        else Debug.LogWarning("[WinZone] ยังไม่ได้ผูก EndGameUIController", this);
    }
}
