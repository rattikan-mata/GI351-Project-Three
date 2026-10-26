using UnityEngine;

/// <summary>
/// ตัวช่วยเล่นเสียงของระบบคราฟยา/ส่งยา: เรียกผ่านชื่อในคลังเสียงของ AudioManager (Sound Library)
/// ถ้าไม่มี AudioManager ในซีน หรือยังไม่ได้ใส่คลิป จะเงียบไป ไม่ทำให้เกมพัง
/// เปลี่ยนเสียง: เอาไฟล์ใหม่ไปใส่ในช่องชื่อเดียวกันของ Audio Manager prefab (หรือทับไฟล์ใน Assets/Audioes/Crafting)
/// </summary>
public static class CraftingSfx
{
    public const string TrayPick = "TrayPick";                 // หยิบถาดวัตถุดิบ
    public const string PotionPick = TrayPick;                 // หยิบขวดยา: ใช้เสียงเดียวกับหยิบถาดวัตถุดิบ
    public const string TrayDrop = "TrayDrop";                // ปล่อยถาดลงหม้อสำเร็จ
    public const string TrayReject = "TrayReject";             // ใส่ถาดลงหม้อไม่ได้ (หม้อไม่ว่าง/ถาดผิดกฎ)
    public const string TrayReady = "TrayReady";               // ถาดคูลดาวน์จบ พร้อมใช้ใหม่
    public const string PotBrew = "PotBrew";                   // เริ่มต้มยา (ยาว ~3 วินาทีตาม brewSeconds)
    public const string PotionCrafted = "PotionCrafted";       // ได้ยาพื้นฐาน
    public const string PotionCraftedCombo = "PotionCraftedCombo"; // ได้ยาผสม (Skyward/Havoc/Surge)
    public const string PotionPlace = "PotionPlace";           // วางขวดยาลงช่อง/ย้ายช่อง
    public const string PotionPlaceFail = "PotionPlaceFail";   // วางขวดยาไม่ได้
    public const string PotionTrash = "PotionTrash";           // ทิ้งยาลงถังขยะ
    public const string SendPotion = "SendPotion";             // กดปุ่มส่งยา

    public static void Play(string soundName, float volumeScale = 1f, float pitchVariance = 0f)
    {
        var audio = AudioManager.Instance;
        if (audio != null) audio.PlaySFX(soundName, volumeScale, pitchVariance);
    }
}
