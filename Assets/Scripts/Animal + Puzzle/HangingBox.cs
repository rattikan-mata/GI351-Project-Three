using UnityEngine;

/// <summary>
/// กล่องที่แขวนด้วยเชือกกลางอากาศ: กล่องลอยค้างอยู่จนกว่า "เชือก" (Door ที่พังได้) จะถูกทำลายหรือเปิด แล้วกล่องจึงตกลงมา
/// (ใช้ระบบพักแรงโน้มถ่วงของ PushableBox เหมือนที่สายพานใช้)
/// ใช้ในด่านที่กล่องต้องตกลงไปทับสวิตช์
/// </summary>
public class HangingBox : MonoBehaviour
{
    [SerializeField] private PushableBox box;
    [Tooltip("เชือก: Door ที่พังได้ (เช่น Door_Wood ปรับให้บาง) พอเชือกพังหรือเปิด กล่องจะตก")]
    [SerializeField] private Door rope;

    private bool held;

    private void Start()
    {
        if (box == null) return;
        box.AddGravityHold();
        held = true;
    }

    private void Update()
    {
        if (!held) return;
        if (box == null) { held = false; return; }
        if (rope == null || rope.IsBroken || rope.IsOpen) Release();
    }

    private void Release()
    {
        held = false;
        if (box != null) box.RemoveGravityHold();
    }

    private void OnDestroy()
    {
        if (held && box != null) box.RemoveGravityHold();
    }
}
