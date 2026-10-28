using UnityEngine;

/// <summary>
/// กล่องที่แขวนด้วยเชือกกลางอากาศ: กล่องลอยค้างอยู่จนกว่า "เชือก" (Door ที่พังได้) จะถูกทำลายหรือเปิด แล้วกล่องจึงตกลงมา
/// (ใช้ระบบพักแรงโน้มถ่วงของ PushableBox เหมือนที่สายพานใช้)
/// ระหว่างที่แขวนอยู่ เชือกกับกล่องจะแกว่งไปมาเบาๆ เหมือนลูกตุ้มรอบปลายเชือกด้านบน
/// ใช้ในด่านที่กล่องต้องตกลงไปทับสวิตช์
/// </summary>
public class HangingBox : MonoBehaviour
{
    [SerializeField] private PushableBox box;
    [Tooltip("เชือก: Door ที่พังได้ (เช่น Door_Wood ปรับให้บาง) พอเชือกพังหรือเปิด กล่องจะตก")]
    [SerializeField] private Door rope;

    [Header("Sway (แกว่ง)")]
    [Tooltip("มุมแกว่งสูงสุดของเชือก (องศา) 0 = ไม่แกว่ง")]
    [SerializeField, Range(0f, 15f)] private float swayDegrees = 2.5f;
    [Tooltip("เวลาแกว่งครบ 1 รอบ (วินาที) ยิ่งมากยิ่งช้า")]
    [SerializeField, Min(0.5f)] private float swayPeriod = 3.4f;
    [Tooltip("จุดที่เชือกผูกไว้ด้านบน (จุดหมุน) ว่างไว้ = ใช้ขอบบนของภาพเชือก")]
    [SerializeField] private Transform pivot;

    private bool held;
    private Vector3 pivotPos;
    private Vector3 ropeStartPos, boxStartPos;
    private Quaternion ropeStartRot, boxStartRot;
    private float phase;

    private void Start()
    {
        if (box == null) return;
        box.AddGravityHold();
        held = true;

        boxStartPos = box.transform.position;
        boxStartRot = box.transform.rotation;
        if (rope != null)
        {
            ropeStartPos = rope.transform.position;
            ropeStartRot = rope.transform.rotation;
            var sr = rope.GetComponent<SpriteRenderer>();
            pivotPos = pivot != null ? pivot.position
                : sr != null ? new Vector3(sr.bounds.center.x, sr.bounds.max.y, 0f)
                : rope.transform.position;
        }
        else pivotPos = boxStartPos + Vector3.up * 2f;

        phase = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        if (!held) return;
        if (box == null) { held = false; return; }
        if (rope == null || rope.IsBroken || rope.IsOpen) Release();
    }

    private void LateUpdate()
    {
        if (!held || box == null || swayDegrees <= 0f) return;

        float angle = Mathf.Sin(Time.time * (Mathf.PI * 2f / swayPeriod) + phase) * swayDegrees;
        var turn = Quaternion.Euler(0f, 0f, angle);

        if (rope != null)
        {
            rope.transform.position = pivotPos + turn * (ropeStartPos - pivotPos);
            rope.transform.rotation = turn * ropeStartRot;
        }
        box.transform.position = pivotPos + turn * (boxStartPos - pivotPos);
        box.transform.rotation = turn * boxStartRot;
    }

    private void Release()
    {
        held = false;
        if (box != null)
        {
            box.transform.rotation = boxStartRot;
            box.RemoveGravityHold();
        }
    }

    private void OnDestroy()
    {
        if (held && box != null) box.RemoveGravityHold();
    }
}
