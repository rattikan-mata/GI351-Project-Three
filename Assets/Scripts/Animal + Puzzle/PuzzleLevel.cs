using System.Collections.Generic;
using PuzzleEngine;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ด่าน 1 ด่าน: กำหนดเงื่อนไขชนะจากสวิตช์และประตูที่ลากมาใส่
/// - โหมด All = ทุกตัวต้อง ON (ประตูนับเป็น ON เมื่อเปิดอยู่)
/// - โหมด Any = ตัวใดตัวหนึ่ง ON ก็พอ
/// - ไม่ใส่อะไรเลย = ด่านจะไม่จบเอง
/// จบด่านครั้งแรกที่เงื่อนไขเป็นจริง แล้ว OnCompleted ทำงานครั้งเดียว
/// ว่าจะให้เกิดอะไรต่อ (โหลดด่านถัดไป, แสดง UI) ไปต่อที่ OnCompleted ใน Inspector
/// ตรรกะการเช็กอยู่ใน PuzzleEngine.LevelGoal
/// </summary>
public class PuzzleLevel : MonoBehaviour
{
    #region Goal
    [Header("Goal (เงื่อนไขชนะ)")]
    [SerializeField] private GateMode goalMode = GateMode.All;

    [Tooltip("สวิตช์ที่ต้องอยู่ในสถานะตามโหมด")]
    [SerializeField] private FloorSwitch[] goalSwitches;

    [Tooltip("ประตูที่ต้องอยู่ในสถานะตามโหมด (ประตูเปิด = ON)")]
    [SerializeField] private Door[] goalDoors;
    #endregion

    #region Events
    [Header("Events")]
    [Tooltip("ทำงานครั้งเดียวตอนจบด่าน")]
    public UnityEvent OnCompleted;
    #endregion

    #region State
    private readonly List<ISignal> goalSignals = new List<ISignal>();
    private LevelGoal goal;

    public bool IsComplete => goal != null && goal.IsComplete;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        BuildGoal();
    }

    private void Update()
    {
        if (goal == null || goal.IsComplete) return;
        goal.Check();
    }
    #endregion

    #region Logic
    private void BuildGoal()
    {
        goalSignals.Clear();

        if (goalSwitches != null)
        {
            foreach (FloorSwitch sw in goalSwitches)
            {
                if (sw != null) goalSignals.Add(sw);
            }
        }

        if (goalDoors != null)
        {
            foreach (Door door in goalDoors)
            {
                if (door != null) goalSignals.Add(door);
            }
        }

        goal = new LevelGoal(() => Gate.Evaluate(goalMode, goalSignals));
        goal.Completed += HandleCompleted;
    }

    private void HandleCompleted()
    {
        Debug.Log("[PuzzleLevel] จบด่านแล้ว");
        if (OnCompleted != null) OnCompleted.Invoke();
    }
    #endregion
}
