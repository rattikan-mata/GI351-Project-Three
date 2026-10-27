namespace PotionSystem
{
    /// <summary>
    /// ขวดยา 1 ขวดที่อยู่ในช่องเตรียมยิง: จำ PotionData ไว้
    /// เป็น class (reference type) เพื่อให้เทียบตัวตนของแต่ละขวดได้
    /// </summary>
    public sealed class PotionInstance
    {
        public PotionData Data { get; }

        public string DisplayName => Data != null ? Data.DisplayName : string.Empty;

        public PotionInstance(PotionData data)
        {
            Data = data;
        }
    }
}
