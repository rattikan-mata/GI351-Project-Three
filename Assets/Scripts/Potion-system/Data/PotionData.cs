using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ยาที่ผสมเสร็จ
    /// ยาเดี่ยว: Red(ส้ม) / Blue / Green | ยาผสม: Skyward / Havoc / Surge
    /// </summary>
    [CreateAssetMenu(fileName = "NewPotion", menuName = "Potion System/Potion", order = 1)]
    public class PotionData : ItemData
    {
        [SerializeField] private PotionKind kind;

        public PotionKind Kind => kind;
    }
}
