using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ชุดสูตรทั้งหมด + ยาที่ได้เมื่อสูตรไม่ตรง (Death potion)
    /// </summary>
    [CreateAssetMenu(fileName = "PotionRecipeBook", menuName = "Potion System/Recipe Book", order = 3)]
    public class PotionRecipeBook : ScriptableObject
    {
        [SerializeField] private List<PotionBlueprint> blueprints = new List<PotionBlueprint>();
        [Tooltip("ยาที่ได้เมื่อใส่ของครบ 3 ชิ้นแต่ไม่ตรงกับสูตรใดเลย")]
        [SerializeField] private PotionData deathPotion;

        public IReadOnlyList<PotionBlueprint> Blueprints => blueprints;
        public PotionData DeathPotion => deathPotion;

        /// <summary>
        /// Step 2: Recipe Evaluation
        /// ของยังไม่ครบ 3 ชิ้น -> null | ตรงสูตร -> Working potion | ไม่ตรงสูตรเลย -> Death potion
        /// </summary>
        public PotionData Evaluate(IReadOnlyList<RawMaterialData> potContents)
        {
            if (potContents == null || potContents.Count < PotionBlueprint.RequiredMaterialCount)
                return null;

            foreach (var blueprint in blueprints)
            {
                if (blueprint != null && blueprint.Matches(potContents))
                    return blueprint.ResultPotion;
            }
            return deathPotion;
        }
    }
}
