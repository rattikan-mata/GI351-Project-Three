using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// เสียงสายพานแบบ "เสียงเปิดเครื่อง + เสียงวนลูป" ที่ใช้ร่วมกันทุกสายพานในฉาก
/// - ตอนมีสายพานเริ่มทำงาน: เล่นเสียงเปิดเครื่อง (Start) 1 ครั้ง แล้วเสียงลูปต่อทันทีแบบไม่มีช่องว่าง (นัดเวลาด้วย dspTime ตรงระดับ sample) และวนลูปไปเรื่อยๆ
/// - สายพานหลายตัวทำงานพร้อมกันก็ได้เสียงชุดเดียว (ไม่ซ้อนกันจนดังขึ้น) เสียงหยุดเมื่อไม่มีสายพานตัวไหนทำงานแล้ว
/// ถูกสร้างขึ้นเองตอนเล่น ไม่ต้องวางในฉาก
/// </summary>
public class BeltAudioPlayer : MonoBehaviour
{
    private const double StartLeadSeconds = 0.03;

    private static BeltAudioPlayer instance;

    private readonly HashSet<ConveyorBelt> active = new HashSet<ConveyorBelt>();
    private AudioSource introSource;
    private AudioSource loopSource;
    private AudioClip introClip;
    private AudioClip loopClip;
    private float userVolume = 1f;
    private bool playing;

    #region API
    /// <summary>สายพานเริ่มทำงาน (เรียกซ้ำได้ ไม่เริ่มเสียงซ้อน)</summary>
    public static void Register(ConveyorBelt belt, AudioClip start, AudioClip loop, float volume)
    {
        if (belt == null || loop == null) return;
        var player = GetOrCreate();
        player.active.Add(belt);
        if (!player.playing) player.Begin(start, loop, volume);
    }

    /// <summary>สายพานหยุดทำงาน หรือถูกปิด/ลบ</summary>
    public static void Unregister(ConveyorBelt belt)
    {
        if (instance == null || belt == null) return;
        instance.active.Remove(belt);
        if (instance.active.Count == 0) instance.StopAll();
    }
    #endregion

    #region Lifecycle
    private static BeltAudioPlayer GetOrCreate()
    {
        if (instance != null) return instance;
        var go = new GameObject("BeltAudioPlayer");
        instance = go.AddComponent<BeltAudioPlayer>();
        instance.introSource = instance.NewSource();
        instance.loopSource = instance.NewSource();
        return instance;
    }

    private AudioSource NewSource()
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        return src;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (!playing) return;

        active.RemoveWhere(b => b == null || !b.isActiveAndEnabled || !b.IsPowered);
        if (active.Count == 0)
        {
            StopAll();
            return;
        }

        ApplyVolumes();
    }
    #endregion

    #region Playback
    private void Begin(AudioClip start, AudioClip loop, float volume)
    {
        introClip = start;
        loopClip = loop;
        userVolume = volume;
        playing = true;
        ApplyVolumes();

        double t0 = AudioSettings.dspTime + StartLeadSeconds;
        double loopAt = t0;

        if (introClip != null)
        {
            introSource.clip = introClip;
            introSource.loop = false;
            introSource.PlayScheduled(t0);
            // ความยาวเสียงเปิดเครื่องนับเป็น sample จริง เสียงลูปจึงเริ่มต่อพอดี
            loopAt = t0 + (double)introClip.samples / introClip.frequency;
        }

        loopSource.clip = loopClip;
        loopSource.loop = true;
        loopSource.PlayScheduled(loopAt);
    }

    private void StopAll()
    {
        playing = false;
        introSource.Stop();
        loopSource.Stop();
    }

    private void ApplyVolumes()
    {
        introSource.volume = ResolveVolume(introClip);
        loopSource.volume = ResolveVolume(loopClip);
    }

    private float ResolveVolume(AudioClip clip)
    {
        var am = AudioManager.Instance;
        if (am == null) return userVolume;
        if (am.IsMuted) return 0f;
        return am.MasterVolume * am.SfxVolume * am.GetClipBaseVolume(clip) * userVolume;
    }
    #endregion
}
