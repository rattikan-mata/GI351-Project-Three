using UnityEngine;

/// <summary>
/// ตัวช่วยเล่นเสียงของชั้นขวดยา/น้องนักปรุงยา/ส่งยา: เรียกผ่านชื่อในคลังเสียงของ AudioManager (Sound Library)
/// ถ้าไม่มี AudioManager ในซีน หรือยังไม่ได้ใส่คลิป จะเงียบไป ไม่ทำให้เกมพัง
/// เปลี่ยนเสียง: เอาไฟล์ใหม่ไปใส่ในช่องชื่อเดียวกันของ Audio Manager prefab (หรือทับไฟล์ใน Assets/Audioes/Crafting)
/// </summary>
public static class CraftingSfx
{
    public const string PotionPick = "TrayPick";               // หยิบขวดยา (ใช้คลิปชื่อ TrayPick เดิม)
    public const string PotBrew = "PotBrew";                   // น้องเริ่มคนหม้อ
    public const string PotionCrafted = "PotionCrafted";       // ได้ยาพื้นฐาน
    public const string PotionCraftedCombo = "PotionCraftedCombo"; // ได้ยาผสม (Skyward/Havoc/Surge)
    public const string PotionPlace = "PotionPlace";           // วางขวดยาลงช่อง/คืนเข้าชั้น
    public const string PotionPlaceFail = "PotionPlaceFail";   // ทำไม่ได้ (เช่น ปรุงครบจำนวนแล้ว)
    public const string SendPotion = "SendPotion";             // โยกคันโยกส่งยาลง
    public const string NextShot = "NextShot";                 // กดปุ่ม ▶ ยิงปืนตัวถัดไป
    public const string LeverReturn ="LeverReturn";           // คันโยกส่งยากลับขึ้นที่เดิม

    public static void Play(string soundName, float volumeScale = 1f, float pitchVariance = 0f)
    {
        var audio = AudioManager.Instance;
        if (audio != null) audio.PlaySFX(soundName, volumeScale, pitchVariance);
    }
}
