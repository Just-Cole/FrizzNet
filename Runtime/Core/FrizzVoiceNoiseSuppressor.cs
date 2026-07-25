using UnityEngine;

namespace FrizzNet.Core
{
    /// <summary>
    /// Lightweight real-time noise suppressor for mono float PCM voice streams.
    /// Combines a high-pass filter, adaptive noise-floor estimate, and soft noise gate.
    /// Designed for Steam Voice decompressed output without native plugins.
    /// </summary>
    public sealed class FrizzVoiceNoiseSuppressor
    {
        private float m_SampleRate = 22050f;
        private float m_HighPassY;
        private float m_PreviousInput;
        private float m_HighPassAlpha;
        private float m_GateGain = 1f;
        private float m_NoiseFloorRms = 0.02f;
        private float m_AttackCoef = 0.1f;
        private float m_ReleaseCoef = 0.01f;

        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gate opens when signal RMS exceeds the noise floor by this margin (dB).
        /// </summary>
        public float GateOpenMarginDb { get; set; } = 8f;

        /// <summary>
        /// Absolute minimum open threshold in dBFS.
        /// </summary>
        public float AbsoluteGateThresholdDb { get; set; } = -48f;

        /// <summary>
        /// How quickly the gate opens when speech is detected (milliseconds).
        /// </summary>
        public float AttackMs { get; set; } = 8f;

        /// <summary>
        /// How quickly the gate closes after speech ends (milliseconds).
        /// </summary>
        public float ReleaseMs { get; set; } = 120f;

        /// <summary>
        /// High-pass cutoff used to remove rumble / fan thump (Hz). Set 0 to disable.
        /// </summary>
        public float HighPassHz { get; set; } = 90f;

        /// <summary>
        /// How aggressively signal below the open threshold is attenuated (0 = off, 1 = hard mute).
        /// </summary>
        public float SuppressionStrength { get; set; } = 0.85f;

        /// <summary>
        /// Adaptive noise-floor learning rate (0-1). Higher adapts faster to room noise.
        /// </summary>
        public float NoiseFloorAdaptRate { get; set; } = 0.03f;

        public float LastRms { get; private set; }
        public float NoiseFloorRms => m_NoiseFloorRms;
        public bool IsGateOpen => m_GateGain > 0.25f;

        public void Configure(uint sampleRate)
        {
            m_SampleRate = Mathf.Max(8000f, sampleRate);
            RecalculateCoefficients();
        }

        public void RecalculateCoefficients()
        {
            float attackSec = Mathf.Max(0.001f, AttackMs * 0.001f);
            float releaseSec = Mathf.Max(0.001f, ReleaseMs * 0.001f);
            m_AttackCoef = 1f - Mathf.Exp(-1f / (attackSec * m_SampleRate));
            m_ReleaseCoef = 1f - Mathf.Exp(-1f / (releaseSec * m_SampleRate));

            if (HighPassHz <= 0f)
            {
                m_HighPassAlpha = 0f;
            }
            else
            {
                float rc = 1f / (2f * Mathf.PI * HighPassHz);
                float dt = 1f / m_SampleRate;
                m_HighPassAlpha = rc / (rc + dt);
            }
        }

        /// <summary>
        /// Processes samples in place. Returns false when the frame is fully suppressed
        /// (useful for transmit-side packet dropping).
        /// </summary>
        public bool Process(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return false;
            }

            return Process(samples, samples.Length);
        }

        /// <summary>
        /// Processes the first <paramref name="count"/> samples in place.
        /// </summary>
        public bool Process(float[] samples, int count)
        {
            if (samples == null || count <= 0)
            {
                return false;
            }

            count = Mathf.Min(count, samples.Length);

            if (!Enabled)
            {
                LastRms = MeasureRms(samples, count);
                return LastRms > 0.0001f;
            }

            double energy = 0.0;
            for (int i = 0; i < count; i++)
            {
                float input = samples[i];
                float output = input;

                if (m_HighPassAlpha > 0f)
                {
                    output = m_HighPassAlpha * (m_HighPassY + input - m_PreviousInput);
                    m_HighPassY = output;
                    m_PreviousInput = input;
                    samples[i] = output;
                }

                energy += output * (double)output;
            }

            float rms = Mathf.Sqrt((float)(energy / count));
            LastRms = rms;

            float openThreshold = Mathf.Max(
                DbToLinear(AbsoluteGateThresholdDb),
                m_NoiseFloorRms * DbToLinear(GateOpenMarginDb));

            if (rms < openThreshold * 0.85f)
            {
                m_NoiseFloorRms = Mathf.Lerp(m_NoiseFloorRms, Mathf.Max(0.0005f, rms), NoiseFloorAdaptRate);
            }

            float targetGain = rms >= openThreshold ? 1f : (1f - Mathf.Clamp01(SuppressionStrength));
            float coef = targetGain > m_GateGain ? m_AttackCoef : m_ReleaseCoef;

            for (int i = 0; i < count; i++)
            {
                m_GateGain += (targetGain - m_GateGain) * coef;
                samples[i] *= m_GateGain;
            }

            return m_GateGain > 0.05f && rms >= openThreshold * 0.5f;
        }

        public void Reset()
        {
            m_HighPassY = 0f;
            m_PreviousInput = 0f;
            m_GateGain = 1f;
            m_NoiseFloorRms = 0.02f;
            LastRms = 0f;
        }

        private static float MeasureRms(float[] samples, int count)
        {
            double energy = 0.0;
            int n = Mathf.Min(count, samples.Length);
            for (int i = 0; i < n; i++)
            {
                float s = samples[i];
                energy += s * (double)s;
            }

            return Mathf.Sqrt((float)(energy / Mathf.Max(1, n)));
        }

        private static float DbToLinear(float db)
        {
            return Mathf.Pow(10f, db * 0.05f);
        }
    }
}
