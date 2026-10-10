using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// วัตถุดิบ (Raw Material)
    /// Monster Parts: Skull, Eyeball | Ore: Heart gemstone, Ice crystal | Herb: Berry, Mushroom
    /// จำนวนที่ผู้เล่นมี (Quantity) เก็บอยู่ใน SourceInventory
    /// </summary>
    [CreateAssetMenu(fileName = "NewRawMaterial", menuName = "Potion System/Raw Material", order = 0)]
    public class RawMaterialData : ItemData
    {
        [SerializeField] private MaterialType materialType;

        public MaterialType MaterialType => materialType;
    }
}
