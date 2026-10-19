namespace PotionSystem
{
    /// <summary>ประเภทของวัตถุดิบ (1 ประเภทใส่ในหม้อได้แค่ 1 ชิ้น)</summary>
    public enum MaterialType
    {
        MonsterParts,
        Ore,
        Herb
    }

    /// <summary>ชนิดของยา</summary>
    public enum PotionKind
    {
        Red,
        Blue,
        Green,
        Skyward,    // เขียว + ฟ้า
        Havoc,      // เขียว + ส้ม
        Surge       // ฟ้า + ส้ม
    }

    /// <summary>ผลลัพธ์ของการพยายามใส่วัตถุดิบลงหม้อ</summary>
    public enum AddMaterialResult
    {
        Success,
        InvalidItem,
        PotFull,
        DuplicateType,
        OutOfStock
    }

    /// <summary>ผลลัพธ์ของการกดยืนยัน Craft</summary>
    public enum CraftResult
    {
        Success,
        PotNotFull,
        NoResult,
        ResultSlotFull
    }
}
