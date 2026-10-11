#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PotionSystem.EditorTools
{
    /// <summary>
    /// เมนู Tools > Potion System > Setup 2D Crafting Scene
    /// สร้าง Shelf / Pot / Result Slots (2D GameObjects, World Space) พร้อมตั้งค่า Component ให้ครบ
    /// </summary>
    public static class Crafting2DSceneSetup
    {
        private const string RootName = "2D Crafting";
        private const string DataRoot = "Assets/Data/PotionSystem";

        [MenuItem("Tools/Potion System/Setup 2D Crafting Scene")]
        public static void Setup()
        {
            // ลบของเก่าที่เคย Setup ไว้ เพื่อให้กดซ้ำได้
            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var inventory = Object.FindFirstObjectByType<SourceInventory>();
            var pot = Object.FindFirstObjectByType<PotManager>();
            var resultSlot = Object.FindFirstObjectByType<ResultSlot>();

            // ---------- Logic objects (ใช้ของเดิมใน Scene ถ้ามี) ----------
            GameObject logicGo = null;
            if (inventory == null || pot == null || resultSlot == null)
            {
                logicGo = new GameObject("PotionSystem");
                Undo.RegisterCreatedObjectUndo(logicGo, "Setup 2D Crafting Scene");
                if (inventory == null) inventory = logicGo.AddComponent<SourceInventory>();
                if (resultSlot == null) resultSlot = logicGo.AddComponent<ResultSlot>();
                if (pot == null) pot = logicGo.AddComponent<PotManager>();
            }

            var materials = LoadAll<RawMaterialData>();
            EnsureStartingStock(inventory, materials);

            var recipeBook = LoadAll<PotionRecipeBook>();
            var potSo = new SerializedObject(pot);
            SetRef(potSo, "sourceInventory", inventory);
            SetRef(potSo, "resultSlot", resultSlot);
            if (recipeBook.Count > 0 && potSo.FindProperty("recipeBook").objectReferenceValue == null)
                SetRef(potSo, "recipeBook", recipeBook[0]);
            potSo.ApplyModifiedProperties();

            // ---------- Root ----------
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Setup 2D Crafting Scene");

            EnsureCamera();
            var potStation = CreatePot(root.transform, pot);
            CreateShelf(root.transform, inventory, potStation);
            CreateResultSlots(root.transform, resultSlot);
            EnsureLegacyMouseEvents();

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("[PotionSystem] Setup 2D Crafting Scene เสร็จแล้ว กด Play เพื่อทดสอบ ลากวัตถุดิบใส่หม้อ แล้วคลิกหม้อเพื่อ Craft");
        }

        // ---------- Shelf ----------

        private static void CreateShelf(Transform root, SourceInventory inventory, PotStation2D potStation)
        {
            var shelf = new GameObject("Shelf");
            shelf.transform.SetParent(root, false);
            shelf.transform.position = new Vector3(-5f, 0f, 0f);

            // แผ่นชั้นวาง (ตกแต่ง)
            var board = CreateSprite("ShelfBoard", shelf.transform, new Color(0.45f, 0.3f, 0.15f), 0);
            board.transform.localScale = new Vector3(3f, 7f, 1f);

            var materials = new List<RawMaterialData>();
            var so = new SerializedObject(inventory);
            var stock = so.FindProperty("startingStock");
            for (int i = 0; i < stock.arraySize; i++)
            {
                var m = stock.GetArrayElementAtIndex(i).FindPropertyRelative("material").objectReferenceValue as RawMaterialData;
                if (m != null && !materials.Contains(m)) materials.Add(m);
            }

            const float spacing = 1.5f;
            float top = (materials.Count - 1) * spacing * 0.5f;
            for (int i = 0; i < materials.Count; i++)
            {
                var m = materials[i];
                var go = new GameObject("Material_" + m.DisplayName);
                go.transform.SetParent(shelf.transform, false);
                go.transform.localPosition = new Vector3(0f, top - i * spacing, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 5;
                if (m.Icon != null)
                {
                    sr.sprite = m.Icon;
                }
                else
                {
                    sr.sprite = CraftingSprites2D.Square;
                    sr.color = CraftingSprites2D.ColorFor(m);
                    go.transform.localScale = Vector3.one * 0.9f;
                }

                var col = go.AddComponent<BoxCollider2D>(); // ขนาดตาม Sprite อัตโนมัติ
                col.isTrigger = true;

                var drag = go.AddComponent<DraggableMaterial2D>();
                var dso = new SerializedObject(drag);
                SetRef(dso, "material", m);
                SetRef(dso, "sourceInventory", inventory);
                SetRef(dso, "pot", potStation);
                dso.ApplyModifiedProperties();
            }
        }

        // ---------- Pot ----------

        private static PotStation2D CreatePot(Transform root, PotManager pot)
        {
            var go = new GameObject("Pot");
            go.transform.SetParent(root, false);
            go.transform.position = Vector3.zero;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2.5f, 2.5f);
            col.isTrigger = true;

            var body = CreateSprite("PotBody", go.transform, new Color(0.25f, 0.25f, 0.3f), 0);
            body.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

            // Child ลอยเหนือหม้อ แสดงวัตถุดิบที่ใส่ (สูงสุด 3)
            var contentRoot = new GameObject("Contents");
            contentRoot.transform.SetParent(go.transform, false);
            contentRoot.transform.localPosition = new Vector3(0f, 2f, 0f);

            var displays = new SpriteRenderer[PotManager.PotCapacity];
            for (int i = 0; i < displays.Length; i++)
            {
                var slot = CreateSprite("Content_" + (i + 1), contentRoot.transform, Color.white, 6);
                slot.transform.localPosition = new Vector3((i - 1) * 1.0f, 0f, 0f);
                slot.SetActive(false);
                displays[i] = slot.GetComponent<SpriteRenderer>();
            }

            var station = go.AddComponent<PotStation2D>();
            var so = new SerializedObject(station);
            SetRef(so, "potManager", pot);
            var arr = so.FindProperty("contentDisplays");
            arr.arraySize = displays.Length;
            for (int i = 0; i < displays.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = displays[i];
            so.ApplyModifiedProperties();
            return station;
        }

        // ---------- Result Slots ----------

        private static void CreateResultSlots(Transform root, ResultSlot resultSlot)
        {
            var go = new GameObject("ResultSlots");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, -3.5f, 0f);

            var points = new Transform[ResultSlot.Capacity];
            for (int i = 0; i < points.Length; i++)
            {
                var p = new GameObject("ResultSlot_" + (i + 1));
                p.transform.SetParent(go.transform, false);
                p.transform.localPosition = new Vector3((i - 2) * 1.5f, 0f, 0f);
                points[i] = p.transform;
            }

            var display = go.AddComponent<ResultSlotDisplay2D>();
            var so = new SerializedObject(display);
            SetRef(so, "resultSlot", resultSlot);
            var arr = so.FindProperty("slotPoints");
            arr.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.ApplyModifiedProperties();
        }

        // ---------- Helpers ----------

        private static GameObject CreateSprite(string name, Transform parent, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CraftingSprites2D.Square;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        private static void EnsureStartingStock(SourceInventory inventory, List<RawMaterialData> materials)
        {
            var so = new SerializedObject(inventory);
            var stock = so.FindProperty("startingStock");
            if (stock.arraySize == 0)
            {
                stock.arraySize = materials.Count;
                for (int i = 0; i < materials.Count; i++)
                {
                    var e = stock.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("material").objectReferenceValue = materials[i];
                    e.FindPropertyRelative("quantity").intValue = 3;
                }
                so.ApplyModifiedProperties();
            }
            if (materials.Count == 0 && stock.arraySize == 0)
                Debug.LogWarning("[PotionSystem] ไม่พบ RawMaterialData ให้รัน Tools > Potion System > Create Default Assets ก่อน");
        }

        private static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
            }
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(-1.5f, 0f, -10f);
        }

        private static void EnsureLegacyMouseEvents()
        {
            Physics2D.queriesHitTriggers = true; // ให้ OnMouseDown ทำงานกับ Collider ที่เป็น Trigger (หม้อ)
        }

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataRoot }))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) list.Add(a);
            }
            return list;
        }

        private static void SetRef(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
        }
    }
}
#endif
