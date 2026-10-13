using UnityEngine;

/// <summary>
/// (ไม่บังคับ) แปะที่ GameObject ที่มี Collider2D เพื่อกำหนดว่าจะให้มีขอบตอนเมาส์ชี้ยังไง
/// ของที่มี OnMouseDown/OnMouseDrag/OnMouseUp หรือเป็นของในระบบยา/สัตว์ ได้ขอบอัตโนมัติอยู่แล้ว ไม่ต้องแปะ
/// ใช้เมื่อ: ของใหม่ที่คลิกผ่านระบบอื่น / อยากเลือก SpriteRenderer เอง / อยากปิดขอบเฉพาะตัว
/// </summary>
[DisallowMultipleComponent]
public class HoverOutlineTarget : MonoBehaviour
{
    [Tooltip("SpriteRenderer ที่จะให้มีขอบ ว่างไว้ = เลือกให้เอง (ตัวบนวัตถุนี้ หรือตัวลูกที่ใหญ่ที่สุด)")]
    [SerializeField] private SpriteRenderer[] renderers;

    [Tooltip("ติ๊กไว้ = วัตถุนี้ไม่มีขอบตอนเมาส์ชี้ (แม้จะคลิกได้)")]
    [SerializeField] private bool disableOutline = false;

    [Tooltip("ติ๊กไว้ = มีขอบตอนเมาส์ชี้เฉยๆ / ไม่ติ๊ก = มีขอบเฉพาะตอนเป็นที่รับของที่ลากมา (ใช้กับ Drop Target ล้วนๆ)")]
    [SerializeField] private bool outlineOnHover = true;

    [Tooltip("ติ๊กไว้ = ของนี้เป็นของที่ลากได้ (ตอนลาก ที่รับ (Drop Target) ทุกชนิดจะมีขอบ)")]
    [SerializeField] private bool isDraggable = false;

    [Tooltip("ติ๊กไว้ = ของนี้เป็นที่รับของที่ลากมาวาง จะมีขอบตอนเมาส์ลากของมาทับ")]
    [SerializeField] private bool isDropTarget = false;

    public SpriteRenderer[] Renderers => renderers;
    public bool DisableOutline => disableOutline;
    public bool OutlineOnHover => outlineOnHover;
    public bool IsDraggable => isDraggable;
    public bool IsDropTarget => isDropTarget;
}
