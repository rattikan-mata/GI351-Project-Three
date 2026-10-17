using UnityEngine;

/// <summary>
/// ปาเลตพรีแฟบสำหรับวางด่าน ใช้กับ Tools > Puzzle > Level Editor
/// สร้างได้จาก Assets > Create > Puzzle > Palette
/// </summary>
[CreateAssetMenu(fileName = "PuzzlePalette", menuName = "Puzzle/Palette")]
public class PuzzlePalette : ScriptableObject
{
    [Tooltip("พรีแฟบสวิตช์พื้น (มีคอมโพเนนต์ FloorSwitch)")]
    public FloorSwitch floorSwitchPrefab;

    [Tooltip("พรีแฟบประตู (มีคอมโพเนนต์ Door)")]
    public Door doorPrefab;

    [Tooltip("พรีแฟบสายพาน (มีคอมโพเนนต์ ConveyorBelt)")]
    public ConveyorBelt conveyorPrefab;
}
