using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ผูกแถบเลื่อน (Slider) เข้ากับระดับเสียงของ AudioManager: ใช้ในหน้า Info ที่เปิดจากปุ่ม Tab
/// - Bgm  = เสียงเพลงพื้นหลัง (AudioManager.BgmVolume)
/// - Game = เสียงเอฟเฟกต์ในเกมทั้งหมด (AudioManager.SfxVolume)
/// ค่าถูกบันทึกลง PlayerPrefs โดย AudioManager เอง จึงจำค่าข้ามด่านและข้ามการเปิดเกม
/// ใช้ได้ตอนเกมหยุด (Time.timeScale = 0) เพราะ Slider ไม่พึ่งเวลาเกม
/// </summary>
[RequireComponent(typeof(Slider))]
public class VolumeSliderBinder : MonoBehaviour, IPointerUpHandler
{
    public enum Channel { Bgm, Game }

    [SerializeField] private Channel channel = Channel.Game;
    [Tooltip("(ไม่บังคับ) ข้อความแสดงเปอร์เซ็นต์ข้างแถบเลื่อน")]
    [SerializeField] private TMP_Text valueLabel;
    [Tooltip("เล่นเสียงตัวอย่างตอนปล่อยเมาส์ (เฉพาะช่อง Game) เพื่อให้ได้ยินระดับเสียงที่ตั้ง ว่างไว้ = ไม่เล่น")]
    [SerializeField] private string previewSound = "TrayDrop";

    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    private void OnEnable()
    {
        slider.onValueChanged.AddListener(OnValueChanged);
        Refresh();
    }

    private void OnDisable()
    {
        slider.onValueChanged.RemoveListener(OnValueChanged);
    }

    /// <summary>ดึงค่าจริงจาก AudioManager มาใส่แถบเลื่อน (ไม่ยิงเหตุการณ์ซ้ำ)</summary>
    private void Refresh()
    {
        var audio = AudioManager.Instance;
        slider.interactable = audio != null;
        float v = audio == null ? 1f : (channel == Channel.Bgm ? audio.BgmVolume : audio.SfxVolume);
        slider.SetValueWithoutNotify(v);
        UpdateLabel(v);
    }

    private void OnValueChanged(float value)
    {
        var audio = AudioManager.Instance;
        if (audio != null)
        {
            if (channel == Channel.Bgm) audio.SetBGMVolume(value);
            else audio.SetSFXVolume(value);
        }
        UpdateLabel(value);
    }

    private void UpdateLabel(float value)
    {
        if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (channel != Channel.Game || string.IsNullOrEmpty(previewSound)) return;
        var audio = AudioManager.Instance;
        if (audio != null) audio.PlaySFX(previewSound, 1f, 0f, true);
    }
}
