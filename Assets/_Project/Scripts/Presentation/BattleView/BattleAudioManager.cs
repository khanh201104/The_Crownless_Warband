using System;
using CrownlessWarband.Core.Events;
using UnityEngine;

namespace CrownlessWarband.Presentation.BattleView
{
    /// <summary>
    /// Hệ thống âm thanh chiến đấu tự chủ sử dụng Procedural Waveform Synthesizer (GDD Section 43).
    /// Tự tổng hợp 8 hiệu ứng âm thanh (SFX) bằng AudioClip.Create khi không gắn file âm thanh ngoài,
    /// kèm cơ chế giới hạn tần suất (rate-limiting) tránh rè âm khi chạy tốc độ cao.
    /// </summary>
    public class BattleAudioManager : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private const float MinPlayInterval = 0.035f;

        [Header("Optional Custom Audio Overrides")]
        [SerializeField] private AudioClip customAttackClip = null;
        [SerializeField] private AudioClip customHitClip = null;
        [SerializeField] private AudioClip customHealClip = null;
        [SerializeField] private AudioClip customShieldClip = null;
        [SerializeField] private AudioClip customInterceptClip = null;
        [SerializeField] private AudioClip customStatusClip = null;
        [SerializeField] private AudioClip customStrategyClip = null;
        [SerializeField] private AudioClip customVictoryClip = null;
        [SerializeField] private AudioClip customUiClickClip = null;

        public bool IsMuted { get; set; } = false;
        public float MasterVolume { get; set; } = 0.50f;

        private AudioSource _audioSource;
        private float _lastPlayTime = -10f;

        private AudioClip _attackClip;
        private AudioClip _hitClip;
        private AudioClip _healClip;
        private AudioClip _shieldClip;
        private AudioClip _interceptClip;
        private AudioClip _statusClip;
        private AudioClip _strategyClip;
        private AudioClip _victoryClip;
        private AudioClip _uiClickClip;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;

            GenerateAllClipsIfNeeded();
        }

        private void OnDestroy()
        {
            ReleaseProceduralClip(_attackClip, customAttackClip);
            ReleaseProceduralClip(_hitClip, customHitClip);
            ReleaseProceduralClip(_healClip, customHealClip);
            ReleaseProceduralClip(_shieldClip, customShieldClip);
            ReleaseProceduralClip(_interceptClip, customInterceptClip);
            ReleaseProceduralClip(_statusClip, customStatusClip);
            ReleaseProceduralClip(_strategyClip, customStrategyClip);
            ReleaseProceduralClip(_victoryClip, customVictoryClip);
            ReleaseProceduralClip(_uiClickClip, customUiClickClip);
        }

        public void PlayForCombatEvent(CombatEvent evt)
        {
            if (evt == null || IsMuted || _audioSource == null) return;

            AudioClip clipToPlay = null;
            float volumeScale = 1.0f;

            switch (evt.EventType)
            {
                case CombatEventType.SkillUsed:
                    clipToPlay = _attackClip;
                    volumeScale = 0.75f;
                    break;
                case CombatEventType.DamageDealt:
                    clipToPlay = _hitClip;
                    volumeScale = 0.90f;
                    break;
                case CombatEventType.HealApplied:
                    clipToPlay = _healClip;
                    volumeScale = 0.80f;
                    break;
                case CombatEventType.ShieldApplied:
                    clipToPlay = _shieldClip;
                    volumeScale = 0.80f;
                    break;
                case CombatEventType.InterceptionTriggered:
                    clipToPlay = _interceptClip;
                    volumeScale = 0.95f;
                    break;
                case CombatEventType.StatusApplied:
                    clipToPlay = _statusClip;
                    volumeScale = 0.75f;
                    break;
                case CombatEventType.SquadStrategyChanged:
                    clipToPlay = _strategyClip;
                    volumeScale = 0.85f;
                    break;
                case CombatEventType.UnitDied:
                    clipToPlay = _hitClip;
                    volumeScale = 1.00f;
                    break;
                case CombatEventType.BattleEnded:
                    clipToPlay = _victoryClip;
                    volumeScale = 1.00f;
                    break;
            }

            if (clipToPlay != null)
            {
                TryPlayOneShot(clipToPlay, volumeScale);
            }
        }

        public void PlayUiClick()
        {
            if (IsMuted || _audioSource == null || _uiClickClip == null) return;
            TryPlayOneShot(_uiClickClip, 0.55f);
        }

        private void TryPlayOneShot(AudioClip clip, float volumeScale)
        {
            float now = Time.unscaledTime;
            if (now - _lastPlayTime < MinPlayInterval) return;

            _lastPlayTime = now;
            _audioSource.PlayOneShot(clip, Mathf.Clamp01(MasterVolume * volumeScale));
        }

        private void GenerateAllClipsIfNeeded()
        {
            _attackClip = customAttackClip != null ? customAttackClip : CreateSweepClip("SFX_Attack", 300f, 110f, 0.10f, 0.55f);
            _hitClip = customHitClip != null ? customHitClip : CreateHitImpactClip("SFX_Hit", 165f, 55f, 0.12f, 0.65f);
            _healClip = customHealClip != null ? customHealClip : CreateTwoToneClip("SFX_Heal", 440f, 660f, 0.18f, 0.50f);
            _shieldClip = customShieldClip != null ? customShieldClip : CreateChordClip("SFX_Shield", 330f, 495f, 0.16f, 0.45f);
            _interceptClip = customInterceptClip != null ? customInterceptClip : CreateSweepClip("SFX_Intercept", 520f, 780f, 0.18f, 0.60f);
            _statusClip = customStatusClip != null ? customStatusClip : CreateModulatedPulseClip("SFX_Status", 360f, 0.16f, 0.50f);
            _strategyClip = customStrategyClip != null ? customStrategyClip : CreateTwoToneClip("SFX_Strategy", 620f, 820f, 0.14f, 0.50f);
            _victoryClip = customVictoryClip != null ? customVictoryClip : CreateArpeggioClip("SFX_Victory", 523f, 659f, 784f, 0.35f, 0.60f);
            _uiClickClip = customUiClickClip != null ? customUiClickClip : CreateSweepClip("SFX_UiClick", 880f, 640f, 0.035f, 0.35f);
        }

        private static AudioClip CreateSweepClip(string clipName, float startHz, float endHz, float duration, float amplitude)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float freq = Mathf.Lerp(startHz, endHz, t);
                phase += (2f * Mathf.PI * freq) / SampleRate;
                float env = 1f - t;
                samples[i] = Mathf.Sin(phase) * amplitude * env;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateHitImpactClip(string clipName, float startHz, float endHz, float duration, float amplitude)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            float phase = 0f;
            uint noiseSeed = 2463534242u;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float freq = Mathf.Lerp(startHz, endHz, t);
                phase += (2f * Mathf.PI * freq) / SampleRate;

                // Xorshift pseudo-random nhẹ cho cảm giác va chạm giòn mà không cấp phát bộ nhớ
                noiseSeed ^= noiseSeed << 13;
                noiseSeed ^= noiseSeed >> 17;
                noiseSeed ^= noiseSeed << 5;
                float whiteNoise = ((noiseSeed & 0xFFFF) / 32768f) - 1f;

                float env = (1f - t) * (1f - t);
                float tone = Mathf.Sin(phase) * 0.72f + whiteNoise * 0.28f;
                samples[i] = tone * amplitude * env;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateTwoToneClip(string clipName, float tone1Hz, float tone2Hz, float duration, float amplitude)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float freq = t < 0.48f ? tone1Hz : tone2Hz;
                phase += (2f * Mathf.PI * freq) / SampleRate;
                float env = Mathf.Sin(t * Mathf.PI);
                samples[i] = Mathf.Sin(phase) * amplitude * env;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateChordClip(string clipName, float freqA, float freqB, float duration, float amplitude)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            float phaseA = 0f;
            float phaseB = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                phaseA += (2f * Mathf.PI * freqA) / SampleRate;
                phaseB += (2f * Mathf.PI * freqB) / SampleRate;
                float env = 1f - t;
                samples[i] = (Mathf.Sin(phaseA) * 0.55f + Mathf.Sin(phaseB) * 0.45f) * amplitude * env;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateModulatedPulseClip(string clipName, float baseHz, float duration, float amplitude)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            float phase = 0f;
            float modPhase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                phase += (2f * Mathf.PI * baseHz) / SampleRate;
                modPhase += (2f * Mathf.PI * 24f) / SampleRate;
                float pulse = 0.5f + 0.5f * Mathf.Sin(modPhase);
                float env = 1f - t;
                samples[i] = Mathf.Sin(phase) * pulse * amplitude * env;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateArpeggioClip(string clipName, float hz1, float hz2, float hz3, float duration, float amplitude)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float freq = t < 0.33f ? hz1 : (t < 0.66f ? hz2 : hz3);
                phase += (2f * Mathf.PI * freq) / SampleRate;
                float env = t < 0.85f ? 1f : Mathf.Clamp01((1f - t) / 0.15f);
                samples[i] = Mathf.Sin(phase) * amplitude * env;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void ReleaseProceduralClip(AudioClip activeClip, AudioClip customOverride)
        {
            if (activeClip != null && activeClip != customOverride)
            {
                Destroy(activeClip);
            }
        }
    }
}
