#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PotionSystem.EditorTools
{
    /// <summary>
    /// เมนู Tools > Potion System > Create Default Assets
    /// สร้าง ScriptableObject ตามสเปก (วัตถุดิบ 6, ยา 4, Blueprint 3, Recipe Book) ไว้ที่ Assets/Data/PotionSystem
    /// ไม่เขียนทับของเดิมถ้ามีอยู่แล้ว
    /// </summary>
    public static class PotionSystemAssetCreator
    {
        private const string Root = "Assets/Data/PotionSystem";

        [MenuItem("Tools/Potion System/Create Default Assets")]
        public static void CreateDefaultAssets()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(Root);
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Potions");
            EnsureFolder(Root + "/Blueprints");

            // Raw materials
            var skull = Material("Skull", MaterialType.MonsterParts);
            var eyeball = Material("Eyeball", MaterialType.MonsterParts);
            var heart = Material("Heart Gemstone", MaterialType.Ore);
            var ice = Material("Ice Crystal", MaterialType.Ore);
            var berry = Material("Berry", MaterialType.Herb);
            var mushroom = Material("Mushroom", MaterialType.Herb);

            // Potions
            var red = Potion("Red Potion", PotionKind.Red);
            var blue = Potion("Blue Potion", PotionKind.Blue);
            var green = Potion("Green Potion", PotionKind.Green);
            var death = Potion("Death Potion", PotionKind.Death);

            // Blueprints
            var bpRed = Blueprint("Red Potion Blueprint", red, skull, heart, mushroom);
            var bpBlue = Blueprint("Blue Potion Blueprint", blue, skull, ice, berry);
            var bpGreen = Blueprint("Green Potion Blueprint", green, eyeball, heart, berry);

            // Recipe book
            string bookPath = Root + "/PotionRecipeBook.asset";
            var book = AssetDatabase.LoadAssetAtPath<PotionRecipeBook>(bookPath);
            if (book == null)
            {
                book = ScriptableObject.CreateInstance<PotionRecipeBook>();
                AssetDatabase.CreateAsset(book, bookPath);

                var so = new SerializedObject(book);
                var list = so.FindProperty("blueprints");
                list.arraySize = 3;
                list.GetArrayElementAtIndex(0).objectReferenceValue = bpRed;
                list.GetArrayElementAtIndex(1).objectReferenceValue = bpBlue;
                list.GetArrayElementAtIndex(2).objectReferenceValue = bpGreen;
                so.FindProperty("deathPotion").objectReferenceValue = death;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PotionSystem] สร้าง Default Assets เสร็จที่ " + Root);
        }

        private static RawMaterialData Material(string displayName, MaterialType type)
        {
            string path = $"{Root}/Materials/{displayName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RawMaterialData>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<RawMaterialData>();
            AssetDatabase.CreateAsset(asset, path);
            var so = new SerializedObject(asset);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("materialType").enumValueIndex = (int)type;
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static PotionData Potion(string displayName, PotionKind kind)
        {
            string path = $"{Root}/Potions/{displayName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PotionData>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<PotionData>();
            AssetDatabase.CreateAsset(asset, path);
            var so = new SerializedObject(asset);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static PotionBlueprint Blueprint(string assetName, PotionData result, params RawMaterialData[] materials)
        {
            string path = $"{Root}/Blueprints/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PotionBlueprint>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<PotionBlueprint>();
            AssetDatabase.CreateAsset(asset, path);
            var so = new SerializedObject(asset);
            var list = so.FindProperty("useMaterials");
            list.arraySize = materials.Length;
            for (int i = 0; i < materials.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
            so.FindProperty("resultPotion").objectReferenceValue = result;
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
