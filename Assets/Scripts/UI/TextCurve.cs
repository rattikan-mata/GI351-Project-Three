using TMPro;
using UnityEngine;

/// <summary>
/// ดัดตัวหนังสือ TMP ให้โค้งตามผิวทรงกระบอก (เช่นตัวเลขบนหม้อ)
/// - ตัวอักษรยิ่งห่างกลางยิ่งยกขึ้น (โค้งแบบยิ้ม) และเอียงตามความโค้ง
/// - ตัวอักษรที่ขอบถูกบีบแคบลงเล็กน้อยเหมือนหันออกจากกล้อง
/// </summary>
[RequireComponent(typeof(TMP_Text))]
[ExecuteAlways]
public class TextCurve : MonoBehaviour
{
    [Tooltip("ความสูงที่ขอบตัวหนังสือยกขึ้นเทียบกับตรงกลาง (หน่วยของตัวหนังสือ) ค่าลบ = โค้งคว่ำ")]
    [SerializeField] private float bend = 0.12f;

    [Tooltip("รัศมีผิวทรงกระบอก (หน่วยของตัวหนังสือ) ยิ่งน้อยขอบยิ่งถูกบีบ 0 = ไม่บีบ")]
    [SerializeField, Min(0f)] private float cylinderRadius = 1.6f;

    private TMP_Text text;
    private bool dirty = true;

    public float Bend { get => bend; set { bend = value; dirty = true; } }
    public float CylinderRadius { get => cylinderRadius; set { cylinderRadius = value; dirty = true; } }

    private void OnEnable()
    {
        text = GetComponent<TMP_Text>();
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        dirty = true;
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        if (text != null) text.ForceMeshUpdate();
    }

    private void OnValidate() => dirty = true;

    private void OnTextChanged(Object obj)
    {
        if (obj == text && !warping) dirty = true;
    }

    private bool warping;

    private void LateUpdate()
    {
        if (!dirty || text == null) return;
        dirty = false;
        Warp();
    }

    private void Warp()
    {
        warping = true; // ForceMeshUpdate ยิงอีเวนต์ข้อความเปลี่ยน ไม่ให้วนดัดซ้ำทุกเฟรม
        text.ForceMeshUpdate();
        warping = false;
        TMP_TextInfo info = text.textInfo;
        if (info.characterCount == 0) return;

        Bounds b = text.textBounds;
        float half = Mathf.Max(0.0001f, b.extents.x);
        float cx = b.center.x;

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];
            if (!ch.isVisible) continue;

            int mi = ch.materialReferenceIndex;
            int vi = ch.vertexIndex;
            Vector3[] verts = info.meshInfo[mi].vertices;

            Vector3 mid = (verts[vi] + verts[vi + 2]) * 0.5f;
            float u = (mid.x - cx) / half; // -1..1

            // บีบตามผิวทรงกระบอก: ตำแหน่ง x ใหม่ = R*sin(x/R)
            float newX = mid.x;
            float squeeze = 1f;
            if (cylinderRadius > 0.0001f)
            {
                float dx = mid.x - cx;
                newX = cx + cylinderRadius * Mathf.Sin(dx / cylinderRadius);
                squeeze = Mathf.Cos(dx / cylinderRadius);
            }

            float lift = bend * u * u;
            float slope = 2f * bend * u / half;
            Quaternion rot = Quaternion.Euler(0f, 0f, Mathf.Atan(slope) * Mathf.Rad2Deg);

            for (int k = 0; k < 4; k++)
            {
                Vector3 local = verts[vi + k] - mid;
                local.x *= squeeze;
                verts[vi + k] = new Vector3(newX, mid.y + lift, mid.z) + rot * local;
            }
        }

        for (int m = 0; m < info.meshInfo.Length; m++)
        {
            info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
            text.UpdateGeometry(info.meshInfo[m].mesh, m);
        }
    }
}
