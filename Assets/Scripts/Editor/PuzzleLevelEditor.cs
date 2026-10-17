using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ตัวจัดวางด่านแบบตกแต่งแมพ: เลือกของจาก Palette แล้วคลิกในฉาก ของจะลงตารางให้อัตโนมัติ
/// - วาง: เลือกสวิตช์ / ประตู / สายพาน แล้วคลิกเพื่อวาง
/// - ผูก: คลิกสวิตช์ก่อน แล้วคลิกประตูหรือสายพานที่ให้สวิตช์นั้นควบคุม (คลิกซ้ำเพื่อยกเลิกการผูก)
/// - ลบ: คลิกของเพื่อลบ
/// - เป้าหมาย: คลิกสวิตช์หรือประตูเพื่อใส่/เอาออกจากเงื่อนไขชนะของ PuzzleLevel (กรอบเขียว = อยู่ในเงื่อนไข)
/// กด Esc เพื่อยกเลิกการเลือกสวิตช์ที่ค้างอยู่
/// ตรวจด่าน: หาประตู/สายพานที่ไม่มีสวิตช์, ด่านที่ไม่มีเงื่อนไขชนะ และสวิตช์ที่ไม่ได้ผูกกับอะไร
/// เปิดได้จาก Tools > Puzzle > Level Editor
/// </summary>
public class PuzzleLevelEditor : EditorWindow
{
    private enum Mode { Place, Link, Erase, Goal }
    private enum Item { FloorSwitch, Door, Conveyor }

    private static readonly string[] ModeNames = { "วาง", "ผูก", "ลบ", "เป้าหมาย" };
    private static readonly string[] ItemNames = { "สวิตช์", "ประตู", "สายพาน" };
    private static readonly string[] GoalModeNames = { "ทุกตัว (All)", "อย่างน้อยหนึ่ง (Any)" };

    /// <summary>ถ้าซูมออกไกลจนเส้นตารางเยอะเกินนี้ จะไม่วาดตาราง กันเครื่องหน่วง</summary>
    private const int MaxGridLines = 300;

    private PuzzlePalette palette;
    private float gridSize = 1f;
    private bool showGrid = true;
    private Mode mode = Mode.Place;
    private Item item = Item.FloorSwitch;
    private FloorSwitch pendingSwitch;
    private string status = "";
    private readonly List<string> issues = new List<string>();

    [MenuItem("Tools/Puzzle/Level Editor")]
    public static void Open()
    {
        GetWindow<PuzzleLevelEditor>("Puzzle Level").Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    #region Window
    private void OnGUI()
    {
        palette = (PuzzlePalette)EditorGUILayout.ObjectField("Palette", palette, typeof(PuzzlePalette), false);
        gridSize = Mathf.Max(0.05f, EditorGUILayout.FloatField("ขนาดตาราง", gridSize));
        showGrid = EditorGUILayout.Toggle("แสดงตาราง", showGrid);

        mode = (Mode)GUILayout.Toolbar((int)mode, ModeNames);

        if (mode == Mode.Place)
        {
            if (palette == null)
            {
                EditorGUILayout.HelpBox("สร้าง Palette ที่ Assets > Create > Puzzle > Palette แล้วลากมาใส่ช่องด้านบน", MessageType.Info);
            }
            else
            {
                item = (Item)GUILayout.Toolbar((int)item, ItemNames);
                if (GetPrefab(item) == null)
                    EditorGUILayout.HelpBox("รายการนี้ยังไม่ได้ใส่พรีแฟบใน Palette", MessageType.Warning);
            }
        }
        else if (mode == Mode.Link)
        {
            string hint = pendingSwitch == null
                ? "คลิกสวิตช์ก่อน"
                : "เลือก " + pendingSwitch.name + " อยู่ คลิกประตูหรือสายพานเพื่อผูก";
            EditorGUILayout.HelpBox(hint, MessageType.Info);
        }
        else if (mode == Mode.Goal)
        {
            DrawGoalPanel();
        }

        if (GUILayout.Button("ตรวจด่าน")) RunValidation();

        GUILayout.Label(status);
        foreach (string msg in issues)
            EditorGUILayout.HelpBox(msg, MessageType.Warning);
    }

    private void DrawGoalPanel()
    {
        PuzzleLevel level = FindLevel();
        if (level == null)
        {
            EditorGUILayout.HelpBox("ยังไม่มี PuzzleLevel ในฉาก", MessageType.Warning);
            if (GUILayout.Button("สร้าง PuzzleLevel ในฉาก")) CreateLevel();
            return;
        }

        EditorGUILayout.HelpBox("คลิกสวิตช์หรือประตูเพื่อใส่/เอาออกจากเงื่อนไขชนะของ " + level.name, MessageType.Info);

        var so = new SerializedObject(level);
        so.Update();
        SerializedProperty modeProp = so.FindProperty("goalMode");
        int current = modeProp.enumValueIndex;
        int picked = GUILayout.Toolbar(current, GoalModeNames);
        if (picked != current)
        {
            Undo.RecordObject(level, "Change goal mode");
            modeProp.enumValueIndex = picked;
            so.ApplyModifiedProperties();
        }
    }
    #endregion

    #region Scene
    private void OnSceneGUI(SceneView view)
    {
        Event e = Event.current;
        Vector3 world = MouseToPlaneZ0(e.mousePosition);
        Vector3 snapped = Snap(world);

        if (e.type == EventType.Repaint)
        {
            if (showGrid) DrawGrid(view);
            DrawLinks();
            DrawGoalMarkers();
            if (mode == Mode.Place && palette != null) DrawSquare(snapped, Color.cyan);
            if (pendingSwitch != null) DrawSquare(pendingSwitch.transform.position, Color.yellow);
        }
        else if (e.type == EventType.MouseMove)
        {
            SceneView.RepaintAll();
        }
        else if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            HandleClick(world, snapped);
            e.Use();
            SceneView.RepaintAll();
            Repaint();
        }
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            pendingSwitch = null;
            status = "ยกเลิกการเลือกสวิตช์แล้ว";
            e.Use();
            SceneView.RepaintAll();
            Repaint();
        }
    }

    private void HandleClick(Vector3 world, Vector3 snapped)
    {
        switch (mode)
        {
            case Mode.Place: PlaceAt(snapped); break;
            case Mode.Link: LinkAt(world); break;
            case Mode.Erase: EraseAt(world); break;
            case Mode.Goal: GoalAt(world); break;
        }
    }

    private void PlaceAt(Vector3 pos)
    {
        if (palette == null) return;

        UnityEngine.Object prefab = GetPrefab(item);
        if (prefab == null)
        {
            status = "รายการนี้ยังไม่ได้ใส่พรีแฟบใน Palette";
            return;
        }

        UnityEngine.Object instance = PrefabUtility.InstantiatePrefab(prefab);
        Component asComponent = instance as Component;
        GameObject go = asComponent != null ? asComponent.gameObject : instance as GameObject;
        if (go == null) return;

        Undo.RegisterCreatedObjectUndo(go, "Place " + item);
        go.transform.position = pos;
        Selection.activeGameObject = go;
        status = "วาง " + go.name + " แล้ว";
    }

    private void LinkAt(Vector3 world)
    {
        Component hit = FindNearest(world);
        if (hit == null)
        {
            status = "ตรงจุดนี้ไม่มีสวิตช์ ประตู หรือสายพาน";
            return;
        }

        FloorSwitch sw = hit as FloorSwitch;
        if (sw != null)
        {
            pendingSwitch = sw;
            status = "เลือก " + sw.name + " แล้ว คลิกประตูหรือสายพานที่จะให้ควบคุม";
            return;
        }

        if (pendingSwitch == null)
        {
            status = "ต้องเลือกสวิตช์ก่อน";
            return;
        }

        ToggleLink(hit, pendingSwitch);
    }

    private void GoalAt(Vector3 world)
    {
        PuzzleLevel level = FindLevel();
        if (level == null)
        {
            status = "ยังไม่มี PuzzleLevel ในฉาก";
            return;
        }

        Component hit = FindNearest(world);
        if (hit == null)
        {
            status = "ไม่มีสวิตช์หรือประตูตรงจุดนี้";
            return;
        }

        if (hit is ConveyorBelt)
        {
            status = "สายพานไม่ใช้เป็นเงื่อนไขชนะ";
            return;
        }

        string propName = hit is FloorSwitch ? "goalSwitches" : "goalDoors";
        bool removed = ToggleRef(level, propName, hit);
        status = removed
            ? "เอา " + hit.name + " ออกจากเงื่อนไขชนะของ " + level.name
            : "ใส่ " + hit.name + " เข้าเงื่อนไขชนะของ " + level.name;
    }

    private void EraseAt(Vector3 world)
    {
        Component hit = FindNearest(world);
        if (hit == null)
        {
            status = "ไม่มีอะไรให้ลบตรงจุดนี้";
            return;
        }

        if (pendingSwitch != null && hit.gameObject == pendingSwitch.gameObject) pendingSwitch = null;
        status = "ลบ " + hit.gameObject.name;
        Undo.DestroyObjectImmediate(hit.gameObject);
    }

    private void CreateLevel()
    {
        GameObject go = new GameObject("PuzzleLevel");
        Undo.RegisterCreatedObjectUndo(go, "Create PuzzleLevel");
        go.AddComponent<PuzzleLevel>();
        Selection.activeGameObject = go;
        status = "สร้าง PuzzleLevel แล้ว";
    }
    #endregion

    #region Linking
    /// <summary>ผูกหรือเลิกผูกสวิตช์กับประตูหรือสายพาน (ใช้ช่อง switches)</summary>
    private void ToggleLink(Component target, FloorSwitch sw)
    {
        bool removed = ToggleRef(target, "switches", sw);
        status = removed
            ? "ยกเลิกการผูก " + sw.name + " กับ " + target.name
            : "ผูก " + sw.name + " กับ " + target.name;
    }

    /// <summary>
    /// เพิ่มหรือเอาค่าออกจากช่องลิสต์ชื่อ propName ของคอมโพเนนต์
    /// คืน true ถ้าเอาออก, false ถ้าเพิ่มเข้า
    /// </summary>
    private static bool ToggleRef(Component owner, string propName, UnityEngine.Object value)
    {
        var so = new SerializedObject(owner);
        so.Update();
        SerializedProperty list = so.FindProperty(propName);
        if (list == null)
        {
            Debug.LogWarning("[PuzzleLevelEditor] " + owner.name + " ไม่มีช่อง " + propName);
            return false;
        }

        // สร้างลิสต์ใหม่เองแทนการลบช่องตรง ๆ เพราะการลบช่องที่เป็น reference บางครั้งทิ้งค่า null ไว้
        var refs = new List<UnityEngine.Object>();
        bool wasPresent = false;
        for (int i = 0; i < list.arraySize; i++)
        {
            UnityEngine.Object v = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (!wasPresent && v == value)
            {
                wasPresent = true;
                continue;
            }
            refs.Add(v);
        }
        if (!wasPresent) refs.Add(value);

        Undo.RecordObject(owner, "Toggle " + propName);
        list.arraySize = refs.Count;
        for (int i = 0; i < refs.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = refs[i];
        so.ApplyModifiedProperties();

        return wasPresent;
    }

    private void DrawLinks()
    {
        Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        foreach (Door door in UnityEngine.Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
            DrawLinksTo(door);
        foreach (ConveyorBelt belt in UnityEngine.Object.FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None))
            DrawLinksTo(belt);
    }

    private static void DrawLinksTo(Component target)
    {
        var so = new SerializedObject(target);
        so.Update();
        SerializedProperty list = so.FindProperty("switches");
        if (list == null) return;

        for (int i = 0; i < list.arraySize; i++)
        {
            FloorSwitch sw = list.GetArrayElementAtIndex(i).objectReferenceValue as FloorSwitch;
            if (sw != null) Handles.DrawLine(sw.transform.position, target.transform.position);
        }
    }

    private void DrawGoalMarkers()
    {
        PuzzleLevel level = FindLevel();
        if (level == null) return;

        DrawGoalMarkersFor(level, "goalSwitches");
        DrawGoalMarkersFor(level, "goalDoors");
    }

    private void DrawGoalMarkersFor(PuzzleLevel level, string propName)
    {
        var so = new SerializedObject(level);
        so.Update();
        SerializedProperty list = so.FindProperty(propName);
        if (list == null) return;

        for (int i = 0; i < list.arraySize; i++)
        {
            Component c = list.GetArrayElementAtIndex(i).objectReferenceValue as Component;
            if (c != null) DrawSquare(c.transform.position, Color.green);
        }
    }
    #endregion

    #region Validation
    private void RunValidation()
    {
        issues.Clear();
        var linked = new HashSet<FloorSwitch>();

        foreach (Door door in UnityEngine.Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
        {
            if (CountRefs(door, "switches", linked) == 0)
                issues.Add(door.name + ": ไม่มีสวิตช์ผูก (ประตูจะใช้ค่า Starts Open ตลอด)");
        }

        foreach (ConveyorBelt belt in UnityEngine.Object.FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None))
        {
            if (CountRefs(belt, "switches", linked) == 0)
                issues.Add(belt.name + ": ไม่มีสวิตช์ผูก (สายพานจะทำงานตลอด)");
        }

        foreach (PuzzleLevel level in UnityEngine.Object.FindObjectsByType<PuzzleLevel>(FindObjectsSortMode.None))
        {
            int goals = CountRefs(level, "goalSwitches", linked) + CountRefs(level, "goalDoors", linked);
            if (goals == 0)
                issues.Add(level.name + ": ยังไม่มีเงื่อนไขชนะ (ด่านจะไม่จบเอง)");
        }

        foreach (FloorSwitch sw in UnityEngine.Object.FindObjectsByType<FloorSwitch>(FindObjectsSortMode.None))
        {
            if (!linked.Contains(sw))
                issues.Add(sw.name + ": ไม่ได้ผูกกับประตู สายพาน หรือด่านใด");
        }

        status = issues.Count == 0 ? "ตรวจแล้ว ไม่พบปัญหา" : "พบ " + issues.Count + " รายการ";
    }

    /// <summary>นับช่องที่มีค่า (ไม่ใช่ null) และเก็บสวิตช์ที่พบลงใน linked</summary>
    private static int CountRefs(Component owner, string propName, HashSet<FloorSwitch> linked)
    {
        var so = new SerializedObject(owner);
        so.Update();
        SerializedProperty list = so.FindProperty(propName);
        if (list == null) return 0;

        int count = 0;
        for (int i = 0; i < list.arraySize; i++)
        {
            UnityEngine.Object v = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (v == null) continue;

            count++;
            FloorSwitch sw = v as FloorSwitch;
            if (sw != null) linked.Add(sw);
        }
        return count;
    }
    #endregion

    #region Helpers
    private UnityEngine.Object GetPrefab(Item i)
    {
        if (palette == null) return null;

        switch (i)
        {
            case Item.FloorSwitch: return palette.floorSwitchPrefab;
            case Item.Door: return palette.doorPrefab;
            default: return palette.conveyorPrefab;
        }
    }

    private static PuzzleLevel FindLevel()
    {
        PuzzleLevel[] levels = UnityEngine.Object.FindObjectsByType<PuzzleLevel>(FindObjectsSortMode.None);
        return levels.Length > 0 ? levels[0] : null;
    }

    /// <summary>หาสวิตช์ ประตู หรือสายพานที่อยู่ใกล้จุดนี้ที่สุด (ในระยะครึ่งช่องตาราง)</summary>
    private Component FindNearest(Vector3 world)
    {
        float bestDist = gridSize * 0.6f;
        Component best = null;

        foreach (FloorSwitch s in UnityEngine.Object.FindObjectsByType<FloorSwitch>(FindObjectsSortMode.None))
            Consider(s, world, ref bestDist, ref best);
        foreach (Door d in UnityEngine.Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
            Consider(d, world, ref bestDist, ref best);
        foreach (ConveyorBelt c in UnityEngine.Object.FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None))
            Consider(c, world, ref bestDist, ref best);

        return best;
    }

    private static void Consider(Component c, Vector3 world, ref float bestDist, ref Component best)
    {
        float d = Vector2.Distance(c.transform.position, world);
        if (d <= bestDist)
        {
            bestDist = d;
            best = c;
        }
    }

    private Vector3 Snap(Vector3 p)
    {
        float g = Mathf.Max(0.05f, gridSize);
        return new Vector3(Mathf.Round(p.x / g) * g, Mathf.Round(p.y / g) * g, 0f);
    }

    /// <summary>
    /// แปลงตำแหน่งเมาส์ในหน้าต่าง Scene เป็นจุดบนระนาบ z = 0 (ของ 2D)
    /// ใช้ HandleUtility.GUIPointToWorldRay แล้วหาจุดที่รังสีตัดระนาบนี้
    /// </summary>
    private static Vector3 MouseToPlaneZ0(Vector2 guiPosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        if (Mathf.Abs(ray.direction.z) < 0.000001f)
            return new Vector3(ray.origin.x, ray.origin.y, 0f);

        float t = -ray.origin.z / ray.direction.z;
        Vector3 hit = ray.origin + ray.direction * t;
        hit.z = 0f;
        return hit;
    }

    /// <summary>วาดเส้นตารางเฉพาะพื้นที่ที่มองเห็นในหน้าต่าง Scene</summary>
    private void DrawGrid(SceneView view)
    {
        Camera cam = view.camera;
        if (cam == null) return;

        float g = Mathf.Max(0.05f, gridSize);
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;

        float minX = c.x - halfW;
        float maxX = c.x + halfW;
        float minY = c.y - halfH;
        float maxY = c.y + halfH;

        int cols = (int)((maxX - minX) / g) + 2;
        int rows = (int)((maxY - minY) / g) + 2;
        if (cols > MaxGridLines || rows > MaxGridLines) return;

        Handles.color = new Color(1f, 1f, 1f, 0.12f);

        float startX = Mathf.Floor(minX / g) * g;
        for (int i = 0; i < cols; i++)
        {
            float x = startX + i * g;
            Handles.DrawLine(new Vector3(x, minY, 0f), new Vector3(x, maxY, 0f));
        }

        float startY = Mathf.Floor(minY / g) * g;
        for (int j = 0; j < rows; j++)
        {
            float y = startY + j * g;
            Handles.DrawLine(new Vector3(minX, y, 0f), new Vector3(maxX, y, 0f));
        }
    }

    private void DrawSquare(Vector3 center, Color color)
    {
        float h = gridSize * 0.5f;
        Handles.color = color;

        Vector3 a = center + new Vector3(-h, -h, 0f);
        Vector3 b = center + new Vector3(h, -h, 0f);
        Vector3 c = center + new Vector3(h, h, 0f);
        Vector3 d = center + new Vector3(-h, h, 0f);
        Handles.DrawLine(a, b);
        Handles.DrawLine(b, c);
        Handles.DrawLine(c, d);
        Handles.DrawLine(d, a);
    }
    #endregion
}
