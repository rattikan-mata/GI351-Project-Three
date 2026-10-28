using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// หน้าเมนูหลักแบบ "Press any button to continue": กดปุ่มใดก็ได้ (คีย์บอร์ด เมาส์ จอยสติ๊ก) แล้วไปด่านแรก
/// ข้อความกะพริบเบาๆ ให้รู้ว่ากดได้
/// </summary>
public class PressAnyButton : MonoBehaviour
{
    [SerializeField] private string firstSceneName = "Stage 1";
    [Tooltip("ข้อความที่กะพริบ (ว่างไว้ = ไม่กะพริบ)")]
    [SerializeField] private TMP_Text promptText;
    [Tooltip("(ไม่บังคับ) วัสดุของฟอนต์ที่มีขอบขาว/เงา ใช้แทนวัสดุเริ่มต้น")]
    [SerializeField] private Material promptMaterial;
    [SerializeField, Min(0.1f)] private float pulseSpeed = 2.2f;
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.35f;
    [Tooltip("รอสักครู่หลังเปิดหน้าก่อนรับการกด (วินาที) กันกดค้างมาจากหน้าก่อน")]
    [SerializeField, Min(0f)] private float inputDelay = 0.4f;

    private float startTime;
    private bool leaving;

    private void Awake()
    {
        if (promptText != null && promptMaterial != null) promptText.fontSharedMaterial = promptMaterial;
    }

    private void OnEnable()
    {
        startTime = Time.unscaledTime;
        leaving = false;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (promptText != null)
        {
            float k = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            var c = promptText.color;
            c.a = Mathf.Lerp(minAlpha, 1f, k);
            promptText.color = c;
        }

        if (leaving || Time.unscaledTime - startTime < inputDelay) return;
        if (AnyButtonPressedThisFrame()) Continue();
    }

    private static bool AnyButtonPressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.middleButton.wasPressedThisFrame)) return true;
        foreach (var pad in Gamepad.all)
            if (pad.wasUpdatedThisFrame && pad.allControls.Count > 0)
                foreach (var control in pad.allControls)
                    if (control is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame) return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
        return false;
    }

    private void Continue()
    {
        leaving = true;
        if (AudioUI.Instance != null) AudioUI.Instance.PlayBtnPlay();
        HUDStarSystem.ResetStarsForNewStage();
        SceneManager.LoadScene(firstSceneName);
    }
}
