using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ยาที่ผสมเสร็จ
    /// Working potion: Red / Blue / Green | Bad potion: Death
    /// </summary>
    [CreateAssetMenu(fileName = "NewPotion", menuName = "Potion System/Potion", order = 1)]
    public class PotionData : ItemData
    {
        [SerializeField] private PotionKind kind;

        public PotionKind Kind => kind;
        public PotionCategory Category => kind == PotionKind.Death ? PotionCategory.Bad : PotionCategory.Working;
        public bool IsWorking => Category == PotionCategory.Working;
    }
}
