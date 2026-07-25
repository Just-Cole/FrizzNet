using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Steamworks;
using FrizzNet.Logging;
using FrizzNet.Messaging;
using FrizzNet.Steam;

namespace FrizzNet.Core
{
    /// <summary>
    /// Singleton manager that records local microphone input via Steam Voice,
    /// routes packets, decompresses incoming voice, applies noise suppression,
    /// and manages active speaker outputs.
    /// </summary>
    [DisallowMultipleComponent]
    public class FrizzVoiceManager : MonoBehaviour
    {
        public static FrizzVoiceManager Instance { get; private set; }

        [Header("Voice Configuration")]
        [Tooltip("Enable voice chat functionality.")]
        [SerializeField] private bool m_EnableVoice = true;

        [Tooltip("If checked, voice is only recorded while holding the Push-to-Talk key.")]
        [SerializeField] private bool m_UsePushToTalk = true;

        [Tooltip("The keyboard key used to talk when Push-to-Talk is enabled.")]
        [SerializeField] private Key m_PushToTalkKey = Key.V;

        [Header("Audio Output Settings")]
        [Tooltip("Enable 3D spatialized audio, locating voices at player GameObject transforms.")]
        [SerializeField] private bool m_SpatialAudio = true;

        [Tooltip("Maximum distance at which other players can be heard when spatial audio is enabled.")]
        [SerializeField] private float m_MaxAudioDistance = 35f;

        [Tooltip("Volume multiplier applied to incoming voice streams.")]
        [Range(0f, 2f)]
        [SerializeField] private float m_VolumeMultiplier = 1.0f;

        [Header("Noise Suppression")]
        [Tooltip("Apply high-pass filtering and an adaptive noise gate to received voice.")]
        [SerializeField] private bool m_EnableNoiseSuppression = true;

        [Tooltip("Drop outbound Steam voice packets that fall below the noise gate after local decode.")]
        [SerializeField] private bool m_GateOutboundPackets = true;

        [Tooltip("Absolute gate threshold in dBFS. Lower values let quieter speech through.")]
        [Range(-70f, -20f)]
        [SerializeField] private float m_AbsoluteGateThresholdDb = -48f;

        [Tooltip("How far above the learned noise floor speech must rise to open the gate (dB).")]
        [Range(2f, 20f)]
        [SerializeField] private float m_GateOpenMarginDb = 8f;

        [Tooltip("High-pass cutoff in Hz. Removes rumble and low fan noise. 0 disables.")]
        [Range(0f, 200f)]
        [SerializeField] private float m_HighPassHz = 90f;

        [Tooltip("How hard to mute noise when the gate is closed (0 = off, 1 = full mute).")]
        [Range(0f, 1f)]
        [SerializeField] private float m_SuppressionStrength = 0.85f;

        [Tooltip("Gate open attack time in milliseconds.")]
        [SerializeField] private float m_GateAttackMs = 8f;

        [Tooltip("Gate close release time in milliseconds.")]
        [SerializeField] private float m_GateReleaseMs = 120f;

        private uint m_OptimalSampleRate;
        private bool m_IsRecording;
        private readonly Dictionary<ulong, FrizzVoiceSpeaker> m_ActiveSpeakers = new Dictionary<ulong, FrizzVoiceSpeaker>();
        private byte[] m_DecompressBuffer;
        private byte[] m_CompressedVoiceBuffer;
        private float[] m_PcmScratch;
        private readonly FrizzVoiceNoiseSuppressor m_ReceiveSuppressor = new FrizzVoiceNoiseSuppressor();
        private readonly FrizzVoiceNoiseSuppressor m_TransmitSuppressor = new FrizzVoiceNoiseSuppressor();

        private const short MSG_VOICE = FrizzSystemMessages.Voice;

        public bool EnableVoice { get => m_EnableVoice; set => m_EnableVoice = value; }
        public bool UsePushToTalk { get => m_UsePushToTalk; set => m_UsePushToTalk = value; }
        public Key PushToTalkKey { get => m_PushToTalkKey; set => m_PushToTalkKey = value; }
        public bool SpatialAudio { get => m_SpatialAudio; set => m_SpatialAudio = value; }
        public float MaxAudioDistance { get => m_MaxAudioDistance; set => m_MaxAudioDistance = value; }
        public float VolumeMultiplier { get => m_VolumeMultiplier; set => m_VolumeMultiplier = value; }

        public bool EnableNoiseSuppression
        {
            get => m_EnableNoiseSuppression;
            set
            {
                m_EnableNoiseSuppression = value;
                ApplyNoiseSettingsToSuppressors();
            }
        }

        public bool GateOutboundPackets { get => m_GateOutboundPackets; set => m_GateOutboundPackets = value; }

        public float AbsoluteGateThresholdDb
        {
            get => m_AbsoluteGateThresholdDb;
            set
            {
                m_AbsoluteGateThresholdDb = value;
                ApplyNoiseSettingsToSuppressors();
            }
        }

        public float GateOpenMarginDb
        {
            get => m_GateOpenMarginDb;
            set
            {
                m_GateOpenMarginDb = value;
                ApplyNoiseSettingsToSuppressors();
            }
        }

        public float HighPassHz
        {
            get => m_HighPassHz;
            set
            {
                m_HighPassHz = value;
                ApplyNoiseSettingsToSuppressors();
            }
        }

        public float SuppressionStrength
        {
            get => m_SuppressionStrength;
            set
            {
                m_SuppressionStrength = Mathf.Clamp01(value);
                ApplyNoiseSettingsToSuppressors();
            }
        }

        public bool IsRecording => m_IsRecording;
        public bool IsNoiseGateOpen => m_TransmitSuppressor.IsGateOpen;
        public IReadOnlyDictionary<ulong, FrizzVoiceSpeaker> ActiveSpeakers => m_ActiveSpeakers;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (SteamManager.Initialized)
            {
                m_OptimalSampleRate = SteamUser.GetVoiceOptimalSampleRate();
                if (m_OptimalSampleRate == 0)
                {
                    m_OptimalSampleRate = 22050;
                }
                FrizzLogger.LogInfo($"SteamVoice optimal sample rate detected: {m_OptimalSampleRate} Hz");
            }
            else
            {
                m_OptimalSampleRate = 22050;
            }

            // Pre-allocate buffers to avoid GC in the voice hot path.
            m_DecompressBuffer = new byte[Mathf.Max(4096, (int)m_OptimalSampleRate * 2)];
            m_CompressedVoiceBuffer = new byte[8192];
            m_PcmScratch = new float[m_OptimalSampleRate];

            ApplyNoiseSettingsToSuppressors();
            m_ReceiveSuppressor.Configure(m_OptimalSampleRate);
            m_TransmitSuppressor.Configure(m_OptimalSampleRate);
        }

        private void OnDestroy()
        {
            if (m_IsRecording && SteamManager.Initialized)
            {
                SteamUser.StopVoiceRecording();
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnValidate()
        {
            ApplyNoiseSettingsToSuppressors();
            if (m_OptimalSampleRate > 0)
            {
                m_ReceiveSuppressor.Configure(m_OptimalSampleRate);
                m_TransmitSuppressor.Configure(m_OptimalSampleRate);
            }
        }

        private void Update()
        {
            if (!m_EnableVoice || !SteamManager.Initialized || !FrizzLobby.InLobby)
            {
                if (m_IsRecording)
                {
                    StopRecording();
                }
                return;
            }

            bool wantRecord = !m_UsePushToTalk || IsPushToTalkHeld();

            if (wantRecord && !m_IsRecording)
            {
                StartRecording();
            }
            else if (!wantRecord && m_IsRecording)
            {
                StopRecording();
            }

            if (m_IsRecording)
            {
                PollAndSendVoice();
            }
        }

        private bool IsPushToTalkHeld()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyboard[m_PushToTalkKey].isPressed;
        }

        private void LateUpdate()
        {
            if (!m_EnableVoice || !m_SpatialAudio)
            {
                return;
            }

            foreach (KeyValuePair<ulong, FrizzVoiceSpeaker> pair in m_ActiveSpeakers)
            {
                if (pair.Value != null)
                {
                    UpdateSpeakerPosition(pair.Key, pair.Value);
                }
            }
        }

        private void StartRecording()
        {
            SteamUser.StartVoiceRecording();
            m_IsRecording = true;
            m_TransmitSuppressor.Reset();
            FrizzLogger.LogInfo("Voice Recording started.");
        }

        private void StopRecording()
        {
            SteamUser.StopVoiceRecording();
            m_IsRecording = false;
            FrizzLogger.LogInfo("Voice Recording stopped.");
        }

        private void PollAndSendVoice()
        {
            EVoiceResult availableResult = SteamUser.GetAvailableVoice(out uint pcbCompressed);
            if (availableResult != EVoiceResult.k_EVoiceResultOK || pcbCompressed == 0)
            {
                return;
            }

            if (m_CompressedVoiceBuffer == null || m_CompressedVoiceBuffer.Length < pcbCompressed)
            {
                m_CompressedVoiceBuffer = new byte[Mathf.NextPowerOfTwo((int)pcbCompressed)];
            }

            EVoiceResult voiceResult = SteamUser.GetVoice(true, m_CompressedVoiceBuffer, pcbCompressed, out uint bytesWritten);
            if (voiceResult != EVoiceResult.k_EVoiceResultOK || bytesWritten == 0)
            {
                return;
            }

            if (m_EnableNoiseSuppression && m_GateOutboundPackets)
            {
                if (!TryDecompressToScratch(m_CompressedVoiceBuffer, (int)bytesWritten, applyVolume: false, out int sampleCount))
                {
                    SendVoicePacket(m_CompressedVoiceBuffer, (int)bytesWritten);
                    return;
                }

                if (!m_TransmitSuppressor.Process(m_PcmScratch, sampleCount))
                {
                    return;
                }
            }

            SendVoicePacket(m_CompressedVoiceBuffer, (int)bytesWritten);
        }

        private void SendVoicePacket(byte[] voiceData, int length)
        {
            if (NetworkManager.Instance == null || (!NetworkManager.Instance.IsHost && !NetworkManager.Instance.IsClient))
            {
                return;
            }

            using (MessageWriter writer = new MessageWriter())
            {
                writer.WriteLong((long)SteamUser.GetSteamID().m_SteamID);
                writer.WriteInt(length);
                writer.WriteRawBytes(voiceData, 0, length);

                if (NetworkManager.Instance.IsHost)
                {
                    NetworkManager.Instance.SendToAll(MSG_VOICE, writer, false);
                }
                else if (NetworkManager.Instance.IsClient)
                {
                    NetworkManager.Instance.SendToServer(MSG_VOICE, writer, false);
                }
            }
        }

        /// <summary>
        /// Receives compressed voice bytes, decompresses them, applies noise suppression,
        /// and routes them to the sender's speaker.
        /// </summary>
        public void ReceiveVoiceData(ulong senderId, byte[] compressedData)
        {
            if (!m_EnableVoice || compressedData == null || compressedData.Length == 0)
            {
                return;
            }

            if (!TryDecompressToScratch(compressedData, compressedData.Length, applyVolume: true, out int sampleCount))
            {
                return;
            }

            if (m_EnableNoiseSuppression)
            {
                if (!m_ReceiveSuppressor.Process(m_PcmScratch, sampleCount))
                {
                    return;
                }
            }

            FrizzVoiceSpeaker speaker = GetOrCreateSpeaker(senderId);
            speaker.EnqueueSamples(m_PcmScratch, sampleCount);

            if (m_SpatialAudio)
            {
                UpdateSpeakerPosition(senderId, speaker);
            }
        }

        private bool TryDecompressToScratch(byte[] compressedData, int compressedLength, bool applyVolume, out int sampleCount)
        {
            sampleCount = 0;

            if (m_DecompressBuffer == null)
            {
                m_DecompressBuffer = new byte[Mathf.Max(4096, (int)m_OptimalSampleRate * 2)];
            }

            EVoiceResult result = SteamUser.DecompressVoice(
                compressedData,
                (uint)compressedLength,
                m_DecompressBuffer,
                (uint)m_DecompressBuffer.Length,
                out uint bytesWritten,
                m_OptimalSampleRate
            );

            if (result != EVoiceResult.k_EVoiceResultOK || bytesWritten == 0)
            {
                return false;
            }

            sampleCount = (int)(bytesWritten / 2);
            if (m_PcmScratch == null || m_PcmScratch.Length < sampleCount)
            {
                m_PcmScratch = new float[Mathf.NextPowerOfTwo(Mathf.Max(sampleCount, 256))];
            }

            float volume = applyVolume ? m_VolumeMultiplier : 1f;
            for (int i = 0; i < sampleCount; i++)
            {
                short pcmSample = (short)(m_DecompressBuffer[i * 2] | (m_DecompressBuffer[i * 2 + 1] << 8));
                m_PcmScratch[i] = (pcmSample / 32768.0f) * volume;
            }

            return true;
        }

        private void ApplyNoiseSettingsToSuppressors()
        {
            ApplyNoiseSettings(m_ReceiveSuppressor);
            ApplyNoiseSettings(m_TransmitSuppressor);
        }

        private void ApplyNoiseSettings(FrizzVoiceNoiseSuppressor suppressor)
        {
            if (suppressor == null)
            {
                return;
            }

            suppressor.Enabled = m_EnableNoiseSuppression;
            suppressor.AbsoluteGateThresholdDb = m_AbsoluteGateThresholdDb;
            suppressor.GateOpenMarginDb = m_GateOpenMarginDb;
            suppressor.HighPassHz = m_HighPassHz;
            suppressor.SuppressionStrength = m_SuppressionStrength;
            suppressor.AttackMs = m_GateAttackMs;
            suppressor.ReleaseMs = m_GateReleaseMs;
            suppressor.RecalculateCoefficients();
        }

        private FrizzVoiceSpeaker GetOrCreateSpeaker(ulong senderId)
        {
            if (m_ActiveSpeakers.TryGetValue(senderId, out FrizzVoiceSpeaker speaker) && speaker == null)
            {
                m_ActiveSpeakers.Remove(senderId);
            }

            if (!m_ActiveSpeakers.TryGetValue(senderId, out speaker) || speaker == null)
            {
                GameObject speakerGo = new GameObject($"FrizzVoiceSpeaker_{senderId}");
                speakerGo.transform.SetParent(transform);

                AudioSource source = speakerGo.AddComponent<AudioSource>();
                source.spatialize = m_SpatialAudio;
                source.spatialBlend = m_SpatialAudio ? 1.0f : 0.0f;
                source.maxDistance = m_MaxAudioDistance;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.loop = true;

                speaker = speakerGo.AddComponent<FrizzVoiceSpeaker>();
                speaker.Initialize(senderId, source, m_OptimalSampleRate);
                m_ActiveSpeakers[senderId] = speaker;
            }

            return speaker;
        }

        private void UpdateSpeakerPosition(ulong senderId, FrizzVoiceSpeaker speaker)
        {
            if (NetworkManager.Instance == null)
            {
                return;
            }

            Transform targetTransform = null;
            foreach (var identity in NetworkManager.Instance.NetworkObjects.Values)
            {
                if (identity != null && identity.OwnerConnectionId == senderId)
                {
                    targetTransform = identity.transform;
                    break;
                }
            }

            if (targetTransform != null)
            {
                speaker.transform.position = targetTransform.position;
            }
            else if (Camera.main != null)
            {
                speaker.transform.position = Camera.main.transform.position;
            }
        }
    }
}
