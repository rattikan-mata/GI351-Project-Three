using System.Collections;
using UnityEngine;

/// <summary>
/// สคริปต์สำหรับติดบน Prefab ลำแสงเลเซอร์ระยะไกลของแพะ (Goat Laser Beam)
/// รองรับทั้ง Prefab ที่ใช้ LineRenderer, SpriteRenderer (ยืด/เรียงสไปรต์), และ ParticleSystem
/// สามารถใส่ประกายไฟหัวยิง (Muzzle), ประกายไฟจุดชน (Impact), และเอฟเฟกต์ค่อยๆ จางหาย (Fade Out) ได้
/// </summary>
public class GoatLaserBeam : MonoBehaviour
{
    public enum BeamVisualMode
    {
        [Tooltip("ตรวจหา LineRenderer อัตโนมัติ (ถ้ามี) มิฉะนั้นใช้ SpriteRenderer")]
        AutoDetect,
        [Tooltip("ใช้ LineRenderer วาดเส้นเลเซอร์")]
        LineRenderer,
        [Tooltip("ใช้ SpriteRenderer ยืด/ขยายความยาวสไปรต์ตามแนวทิศทาง")]
        StretchedSprite,
        [Tooltip("ใช้ ParticleSystem อย่างเดียว")]
        ParticlesOnly
    }

    #region Visual Components
    [Header("Visual Components (Optional - เว้นว่างจะค้นหาให้อัตโนมัติ)")]
    [Tooltip("รูปแบบการแสดงผลของลำแสง")]
    [SerializeField] private BeamVisualMode visualMode = BeamVisualMode.AutoDetect;

    [Tooltip("LineRenderer สำหรับลำแสง (ถ้ามี)")]
    [SerializeField] private LineRenderer lineRenderer;

    [Tooltip("SpriteRenderer สำหรับลำแสง (ถ้ามี)")]
    [SerializeField] private SpriteRenderer beamSprite;

    [Header("Beam Dimensions & Style")]
    [Tooltip("ความหนาของลำแสง (หน่วย)")]
    [SerializeField] private float beamWidth = 0.2f;

    [Tooltip("มุมชดเชยของสไปรต์ (องศา) เช่น ถ้าภาพสไปรต์เดิมวาดเป็นแนวตั้งให้ใส่ 90 หรือ -90")]
    [SerializeField] private float spriteAngleOffset = 0f;

    [Tooltip("สีหลักของเลเซอร์")]
    [SerializeField] private Color beamColor = new Color(0.4f, 0.9f, 1f, 1f);

    [Tooltip("ความเร็วการเลื่อนของ Texture บน LineRenderer (ให้ดูลำแสงมีพลังงานวิ่ง)")]
    [SerializeField] private float textureScrollSpeed = 3f;

    [Header("Lifetime & Animation")]
    [Tooltip("ระยะเวลาแสดงผลของเลเซอร์ (วินาที)")]
    [SerializeField] private float duration = 0.3f;

    [Tooltip("ให้เลเซอร์ค่อยๆ จางหาย (Fade Out) ก่อนถูกทำลาย")]
    [SerializeField] private bool fadeOut = true;

    [Tooltip("ทำลาย GameObject นี้อัตโนมัติเมื่อหมดเวลา")]
    [SerializeField] private bool autoDestroy = true;

    [Header("Points & Children (Optional - สำหรับจัดตำแหน่งลูกใน Prefab)")]
    [Tooltip("Transform ลูกที่จะให้วางที่จุดเริ่มยิง (Muzzle)")]
    [SerializeField] private Transform muzzlePoint;

    [Tooltip("Transform ลูกที่จะให้วางที่จุดชน/ปลายทาง (Impact)")]
    [SerializeField] private Transform impactPoint;

    [Header("Spawn Extra Prefabs (Optional)")]
    [Tooltip("Prefab ประกายไฟ/เอฟเฟกต์ระเบิดตรงจุดชน (สร้างอัตโนมัติเมื่อชน)")]
    [SerializeField] private GameObject impactEffectPrefab;

    [Tooltip("Prefab ประกายไฟตรงจุดยิง (สร้างอัตโนมัติเมื่อยิง)")]
    [SerializeField] private GameObject muzzleEffectPrefab;
    #endregion

    #region State
    private Vector2 startPosition;
    private Vector2 endPosition;
    private Vector2 shootDirection;
    private float beamDistance;
    private Material lineMaterialInstance;
    #endregion

    private void Awake()
    {
        EnsureComponents();
    }

    private void EnsureComponents()
    {
        if (lineRenderer == null) lineRenderer = GetComponentInChildren<LineRenderer>();
        if (beamSprite == null) beamSprite = GetComponentInChildren<SpriteRenderer>();

        if (visualMode == BeamVisualMode.AutoDetect)
        {
            if (lineRenderer != null) visualMode = BeamVisualMode.LineRenderer;
            else if (beamSprite != null) visualMode = BeamVisualMode.StretchedSprite;
        }

        if (lineRenderer != null && lineRenderer.material != null)
        {
            lineMaterialInstance = lineRenderer.material;
        }
    }

    /// <summary>
    /// เรียกใช้โดย PusherAnimal เมื่อแพะยิงเลเซอร์ เพื่อส่งตำแหน่งต้นทาง-ปลายทาง และทิศทาง
    /// </summary>
    public void Setup(Vector2 startPos, Vector2 endPos, Vector2 direction, RaycastHit2D hitInfo = default)
    {
        EnsureComponents();

        startPosition = startPos;
        endPosition = endPos;
        shootDirection = direction;
        beamDistance = Vector2.Distance(startPos, endPos);

        // วางตำแหน่งจุด Muzzle และ Impact ถ้ามีจัดไว้ใน Prefab
        if (muzzlePoint != null)
        {
            muzzlePoint.position = startPos;
            muzzlePoint.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
        if (impactPoint != null)
        {
            impactPoint.position = endPos;
            if (hitInfo.collider != null && hitInfo.normal != Vector2.zero)
                impactPoint.rotation = Quaternion.FromToRotation(Vector2.up, hitInfo.normal);
            else
                impactPoint.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(-direction.y, -direction.x) * Mathf.Rad2Deg);
        }

        // สร้าง Extra Prefabs (Muzzle & Impact) ถ้ามีใส่ไว้
        if (muzzleEffectPrefab != null)
        {
            Quaternion mRot = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            Instantiate(muzzleEffectPrefab, startPos, mRot);
        }
        if (impactEffectPrefab != null && hitInfo.collider != null)
        {
            Quaternion iRot = hitInfo.normal != Vector2.zero
                ? Quaternion.FromToRotation(Vector2.up, hitInfo.normal)
                : Quaternion.Euler(0, 0, Mathf.Atan2(-direction.y, -direction.x) * Mathf.Rad2Deg);
            Instantiate(impactEffectPrefab, endPos, iRot);
        }

        // จัดการ Visual ลำแสง
        ApplyVisual();

        // เล่น ParticleSystems ทั้งหมดในลูก
        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
        {
            ps.Play();
        }

        StartCoroutine(BeamLifetimeRoutine());
    }

    private void ApplyVisual()
    {
        if (visualMode == BeamVisualMode.LineRenderer && lineRenderer != null)
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, endPosition);
            lineRenderer.startWidth = lineRenderer.endWidth = beamWidth;
            if (lineRenderer.startColor == Color.white && beamColor != Color.white)
            {
                lineRenderer.startColor = lineRenderer.endColor = beamColor;
            }
        }
        else if (visualMode == BeamVisualMode.StretchedSprite && beamSprite != null)
        {
            float angle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
            transform.position = (startPosition + endPosition) * 0.5f;
            transform.rotation = Quaternion.Euler(0, 0, angle + spriteAngleOffset);

            if (beamSprite.drawMode == SpriteDrawMode.Tiled || beamSprite.drawMode == SpriteDrawMode.Sliced)
            {
                beamSprite.size = new Vector2(beamDistance, beamWidth > 0 ? beamWidth : beamSprite.size.y);
            }
            else
            {
                float spriteWidth = (beamSprite.sprite != null && beamSprite.sprite.rect.width > 0f)
                    ? beamSprite.sprite.rect.width / beamSprite.sprite.pixelsPerUnit
                    : 1f;
                Vector3 scale = transform.localScale;
                scale.x = beamDistance / Mathf.Max(0.01f, spriteWidth);
                if (beamWidth > 0) scale.y = beamWidth;
                transform.localScale = scale;
            }

            if (beamSprite.color == Color.white && beamColor != Color.white)
            {
                beamSprite.color = beamColor;
            }
        }
    }

    private IEnumerator BeamLifetimeRoutine()
    {
        float elapsed = 0f;
        Color initialLineStart = (lineRenderer != null) ? lineRenderer.startColor : beamColor;
        Color initialLineEnd = (lineRenderer != null) ? lineRenderer.endColor : beamColor;
        Color initialSpriteColor = (beamSprite != null) ? beamSprite.color : beamColor;
        float initialWidth = beamWidth;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            // เลื่อน Texture บน LineRenderer ให้ดูลำแสงเคลื่อนไหว
            if (lineMaterialInstance != null && textureScrollSpeed != 0f)
            {
                lineMaterialInstance.mainTextureOffset -= new Vector2(textureScrollSpeed * Time.deltaTime, 0f);
            }

            // ค่อยๆ จางหาย (Fade Out ในครึ่งหลังของเวลา)
            if (fadeOut && progress > 0.4f)
            {
                float fadeT = (progress - 0.4f) / 0.6f;
                float alpha = 1f - fadeT;

                if (lineRenderer != null)
                {
                    Color sCol = initialLineStart; sCol.a *= alpha;
                    Color eCol = initialLineEnd; eCol.a *= alpha;
                    lineRenderer.startColor = sCol;
                    lineRenderer.endColor = eCol;
                    lineRenderer.startWidth = lineRenderer.endWidth = initialWidth * (0.3f + 0.7f * alpha);
                }

                if (beamSprite != null)
                {
                    Color c = initialSpriteColor;
                    c.a *= alpha;
                    beamSprite.color = c;
                }
            }

            yield return null;
        }

        if (autoDestroy)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (lineMaterialInstance != null)
        {
            Destroy(lineMaterialInstance);
        }
    }
}
