using UnityEngine;

/// <summary>ควันเล็กๆ ตอนสัตว์สลาย (สร้างจากโค้ดใน TestAnimal): ลอยขึ้น ขยาย แล้วจางหาย</summary>
public class DissolvePuff : MonoBehaviour
{
    private Vector2 velocity;
    private float startSize;
    private float life;
    private float age;
    private SpriteRenderer sr;
    private Color startColor;

    public void Init(Vector2 velocity, float size, float life)
    {
        this.velocity = velocity;
        startSize = size;
        this.life = Mathf.Max(0.05f, life);
        sr = GetComponent<SpriteRenderer>();
        startColor = sr != null ? sr.color : Color.white;
        transform.localScale = Vector3.one * size;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / life);
        transform.position += (Vector3)(velocity * Time.deltaTime);
        velocity *= 1f - 2f * Time.deltaTime;
        transform.localScale = Vector3.one * startSize * (1f + k);
        if (sr != null)
        {
            Color c = startColor;
            c.a = startColor.a * (1f - k);
            sr.color = c;
        }
        if (age >= life) Destroy(gameObject);
    }
}
