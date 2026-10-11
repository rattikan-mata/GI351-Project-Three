using UnityEngine;

namespace PotionSystem
{
    /// <summary>
    /// แสดงยาสำเร็จรูปใน World Space: สร้าง Sprite ตามจุด 1-5 ตามลำดับที่ ResultSlot ได้รับยา
    /// </summary>
    public class ResultSlotDisplay2D : MonoBehaviour
    {
        [SerializeField] private ResultSlot resultSlot;
        [SerializeField] private Transform[] slotPoints = new Transform[ResultSlot.Capacity];

        private void OnEnable()
        {
            if (resultSlot == null) return;
            resultSlot.OnChanged += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            if (resultSlot != null) resultSlot.OnChanged -= Rebuild;
        }

        private void Rebuild()
        {
            for (int i = 0; i < slotPoints.Length; i++)
            {
                var point = slotPoints[i];
                if (point == null) continue;

                for (int c = point.childCount - 1; c >= 0; c--)
                    Destroy(point.GetChild(c).gameObject);

                if (i >= resultSlot.Potions.Count) continue;

                var potion = resultSlot.Potions[i];
                var go = new GameObject("Potion_" + potion.DisplayName);
                go.transform.SetParent(point, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 10;
                CraftingSprites2D.Apply(sr, potion, CraftingSprites2D.ColorFor(potion), 0.8f);
            }
        }
    }
}
