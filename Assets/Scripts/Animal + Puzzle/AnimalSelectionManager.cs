using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ตัวจัดการ point and click:
/// - คลิกซ้ายที่สัตว์ = เลือกตัวนั้น (หรือกดปุ่มเลข 1-3 ในแป้นพิมพ์)
/// - คลิกซ้ายที่พื้น = สั่งตัวที่เลือกอยู่เดินไปจุดนั้น
/// - ปุ่มใช้ความสามารถ (ค่าเริ่มต้น Space หรือคลิกขวา) = ให้ตัวที่เลือกอยู่ใช้ความสามารถ (เล็งตามเมาส์)
/// ปุ่มทุกปุ่มปรับได้ใน Inspector
/// </summary>
public class AnimalSelectionManager : MonoBehaviour
{
    public static AnimalSelectionManager Instance { get; private set; }

    #region References
    [Header("References")]
    [Tooltip("สัตว์ทดลองทั้งหมดที่ควบคุมได้ (ถ้าเว้นว่างจะค้นหาในซีนเองตอนเริ่มเกม)")]
    [SerializeField] private List<TestAnimal> animals = new List<TestAnimal>();

    [Tooltip("Layer ของสัตว์ ใช้ตรวจว่าคลิกโดนสัตว์ตัวไหน")]
    [SerializeField] private LayerMask animalLayer;

    [Tooltip("กล้องที่ใช้แปลงตำแหน่งเมาส์ (ว่างไว้ = Camera.main)")]
    [SerializeField] private Camera worldCamera;
    #endregion

    #region Controls
    [Header("Controls (ปรับปุ่มได้)")]
    [SerializeField] private KeyCode abilityKey = KeyCode.Space;

    [Tooltip("ให้คลิกขวาใช้ความสามารถได้ด้วย")]
    [SerializeField] private bool allowRightClickAbility = true;

    [Tooltip("ปุ่มเลือกสัตว์ตามลำดับในลิสต์ (ช่องที่ 1 = ตัวแรก ช่องที่ 2 = ตัวที่สอง ...)")]
    [SerializeField] private KeyCode[] selectKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

    [Tooltip("ไม่ให้คลิกทะลุ UI ลงมาสั่งเดิน")]
    [SerializeField] private bool blockClicksOverUI = true;

    [SerializeField] private bool selectFirstAnimalAtStart = true;
    #endregion

    #region Move Marker
    [Header("Move Marker (Optional)")]
    [Tooltip("(ไม่บังคับ) Prefab เครื่องหมายที่โผล่ตรงจุดที่คลิกสั่งเดิน")]
    [SerializeField] private GameObject moveMarkerPrefab;
    [SerializeField] private float moveMarkerLifetime = 0.6f;
    #endregion

    #region State
    public TestAnimal SelectedAnimal { get; private set; }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (worldCamera == null) worldCamera = Camera.main;
    }

    private void Start()
    {
        if (animals.Count == 0)
        {
            animals.AddRange(FindObjectsByType<TestAnimal>(FindObjectsSortMode.InstanceID));
        }

        animalOrder.Clear();
        foreach (var a in animals) if (a != null) animalOrder.Add(a.name);

        if (selectFirstAnimalAtStart && animals.Count > 0) SelectAnimal(animals[0]);
    }

    private readonly List<string> animalOrder = new List<string>();

    /// <summary>หลังรีเซ็ตพัซเซิล สัตว์ถูกสร้างใหม่ ผูกรายชื่อใหม่ตามลำดับเดิม (ปุ่ม 1-3 ยังตรงตัวเดิม)</summary>
    public void RefreshAnimals()
    {
        SelectedAnimal = null;
        var found = FindObjectsByType<TestAnimal>();
        animals.Clear();
        foreach (var n in animalOrder)
            foreach (var a in found)
                if (a.name == n) { animals.Add(a); break; }

        if (selectFirstAnimalAtStart && animals.Count > 0) SelectAnimal(animals[0]);
    }

    private void Update()
    {
        HandleKeySelection();
        HandleMouseClick();
        HandleAbilityInput();
    }
    #endregion

    #region Input Handling
    private void HandleKeySelection()
    {
        for (int i = 0; i < selectKeys.Length && i < animals.Count; i++)
        {
            if (Input.GetKeyDown(selectKeys[i])) SelectAnimal(animals[i]);
        }
    }

    private void HandleMouseClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (blockClicksOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 world = GetMouseWorldPosition();

        // คลิกโดนสัตว์ = เลือกตัวนั้น
        Collider2D hit = Physics2D.OverlapPoint(world, animalLayer);
        if (hit != null && hit.GetComponentInParent<TestAnimal>() is TestAnimal clicked)
        {
            SelectAnimal(clicked);
            return;
        }

        // คลิกที่พื้น = สั่งตัวที่เลือกอยู่เดินไปที่นั่น
        if (SelectedAnimal != null)
        {
            SelectedAnimal.MoveTo(world);
            SpawnMoveMarker(world);
        }
    }

    private void HandleAbilityInput()
    {
        if (SelectedAnimal == null) return;

        bool pressed = Input.GetKeyDown(abilityKey) || (allowRightClickAbility && Input.GetMouseButtonDown(1));
        if (!pressed) return;
        if (blockClicksOverUI && Input.GetMouseButtonDown(1)
            && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        SelectedAnimal.UseAbility(GetMouseWorldPosition());
    }
    #endregion

    #region Helpers
    public void SelectAnimal(TestAnimal animal)
    {
        if (SelectedAnimal == animal) return;

        if (SelectedAnimal != null) SelectedAnimal.Deselect();
        SelectedAnimal = animal;
        if (SelectedAnimal != null) SelectedAnimal.Select();
    }

    private Vector2 GetMouseWorldPosition()
    {
        return worldCamera.ScreenToWorldPoint(Input.mousePosition);
    }

    private void SpawnMoveMarker(Vector2 position)
    {
        if (moveMarkerPrefab == null) return;
        GameObject marker = Instantiate(moveMarkerPrefab, position, Quaternion.identity);
        Destroy(marker, moveMarkerLifetime);
    }
    #endregion
}
