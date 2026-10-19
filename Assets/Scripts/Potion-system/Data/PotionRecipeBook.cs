using System.Collections.Generic;
using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// ชุดสูตรทั้งหมด (สูตรไม่ตรง = ไม่ได้ยา)
    /// </summary>
    [CreateAssetMenu(fileName = "PotionRecipeBook", menuName = "Potion System/Recipe Book", order = 3)]
    public class PotionRecipeBook : ScriptableObject
    {
        [SerializeField] private List<PotionBlueprint> blueprints = new List<PotionBlueprint>();

        public IReadOnlyList<PotionBlueprint> Blueprints => blueprints;

        /// <summary>
        /// Step 2: Recipe Evaluation
        /// ของยังไม่ครบ 3 ชิ้น -> null | ตรงสูตร -> ยานั้น | ไม่ตรงสูตรเลย -> null (ต้องเทของทิ้ง)
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
            return null;
        }
    }
}
