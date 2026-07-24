using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using FrizzNet.Logging;
using FrizzNet.Messaging;
using FrizzNet.Steam;

namespace FrizzNet.Core
{
    /// <summary>
    /// Singleton manager component that handles recording local microphone voice input, 
    /// routing packets, decompressing incoming voice, and managing active speaker outputs.
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
        [SerializeField] private KeyCode m_PushToTalkKey = KeyCode.V;

        [Header("Audio Output Settings")]
        [Tooltip("Enable 3D spatialized audio, locating voices at player GameObject transforms.")]
        [SerializeField] private bool m_SpatialAudio = true;

        [Tooltip("Maximum distance at which other players can be heard when spatial audio is enabled.")]
        [SerializeField] private float m_MaxAudioDistance = 35f;

        [Tooltip("Volume multiplier applied to incoming voice streams.")]
        [Range(0f, 2f)]
        [SerializeField] private float m_VolumeMultiplier = 1.0f;

        private uint m_OptimalSampleRate;
        private bool m_IsRecording;
        private readonly Dictionary<ulong, FrizzVoiceSpeaker> m_ActiveSpeakers = new Dictionary<ulong, FrizzVoiceSpeaker>();
        private byte[] m_DecompressBuffer;

        private const short MSG_VOICE = FrizzSystemMessages.Voice;

        // Public getters for settings monitoring
        public bool EnableVoice { get => m_EnableVoice; set => m_EnableVoice = value; }
        public bool UsePushToTalk { get => m_UsePushToTalk; set => m_UsePushToTalk = value; }
        public KeyCode PushToTalkKey { get => m_PushToTalkKey; set => m_PushToTalkKey = value; }
        public bool SpatialAudio { get => m_SpatialAudio; set => m_SpatialAudio = value; }
        public float MaxAudioDistance { get => m_MaxAudioDistance; set => m_MaxAudioDistance = value; }
        public float VolumeMultiplier { get => m_VolumeMultiplier; set => m_VolumeMultiplier = value; }
        public bool IsRecording => m_IsRecording;
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
                if (m_OptimalSampleRate == 0) m_OptimalSampleRate = 22050; // Fallback
                FrizzLogger.LogInfo($"SteamVoice optimal sample rate detected: {m_OptimalSampleRate} Hz");
            }
            else
            {
                m_OptimalSampleRate = 22050;
            }

            // Pre-allocate decompression buffer to prevent GC allocations in voice stream hot paths
            m_DecompressBuffer = new byte[m_OptimalSampleRate * 2];
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

        private void Update()
        {
            if (!m_EnableVoice || !SteamManager.Initialized || !FrizzLobby.InLobby)
            {
                if (m_IsRecording) StopRecording();
                return;
            }

            bool wantRecord = !m_UsePushToTalk || Input.GetKey(m_PushToTalkKey);

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

        private void LateUpdate()
        {
            if (!m_EnableVoice || !m_SpatialAudio) return;

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
            if (availableResult == EVoiceResult.k_EVoiceResultOK && pcbCompressed > 0)
            {
                byte[] compressedBuffer = new byte[pcbCompressed];
                EVoiceResult voiceResult = SteamUser.GetVoice(true, compressedBuffer, pcbCompressed, out uint bytesWritten);
                if (voiceResult == EVoiceResult.k_EVoiceResultOK && bytesWritten > 0)
                {
                    // Truncate buffer if bytesWritten is less than size
                    byte[] packetBuffer = compressedBuffer;
                    if (bytesWritten < pcbCompressed)
                    {
                        packetBuffer = new byte[bytesWritten];
                        System.Buffer.BlockCopy(compressedBuffer, 0, packetBuffer, 0, (int)bytesWritten);
                    }

                    SendVoicePacket(packetBuffer);
                }
            }
        }

        private void SendVoicePacket(byte[] voiceData)
        {
            if (NetworkManager.Instance == null || (!NetworkManager.Instance.IsHost && !NetworkManager.Instance.IsClient)) return;

            using (MessageWriter writer = new MessageWriter())
            {
                writer.WriteLong((long)SteamUser.GetSteamID().m_SteamID);
                writer.WriteInt(voiceData.Length);
                writer.WriteRawBytes(voiceData);

                // Send voice unreliably over raw P2P sockets
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
        /// Receives compressed voice bytes, decompresses them, and routes them to the sender's speaker.
        /// </summary>
        public void ReceiveVoiceData(ulong senderId, byte[] compressedData)
        {
            if (!m_EnableVoice) return;

            if (m_DecompressBuffer == null)
            {
                m_DecompressBuffer = new byte[m_OptimalSampleRate * 2];
            }

            // Decompress audio using Steam User API into the shared pre-allocated buffer
            EVoiceResult result = SteamUser.DecompressVoice(
                compressedData,
                (uint)compressedData.Length,
                m_DecompressBuffer,
                (uint)m_DecompressBuffer.Length,
                out uint bytesWritten,
                m_OptimalSampleRate
            );

            if (result == EVoiceResult.k_EVoiceResultOK && bytesWritten > 0)
            {
                // Convert raw 16-bit PCM bytes to float samples
                int sampleCount = (int)(bytesWritten / 2);
                float[] samples = new float[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    short pcmSample = (short)(m_DecompressBuffer[i * 2] | (m_DecompressBuffer[i * 2 + 1] << 8));
                    samples[i] = (pcmSample / 32768.0f) * m_VolumeMultiplier;
                }

                // Route to active speaker
                FrizzVoiceSpeaker speaker = GetOrCreateSpeaker(senderId);
                speaker.EnqueueSamples(samples);

                // Update spatial 3D placement if enabled
                if (m_SpatialAudio)
                {
                    UpdateSpeakerPosition(senderId, speaker);
                }
            }
        }

        private FrizzVoiceSpeaker GetOrCreateSpeaker(ulong senderId)
        {
            // Clean out dead speakers first
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
            if (NetworkManager.Instance == null) return;

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
            else
            {
                // Fallback: place it on the main camera listener to make it sound non-spatial/direct if target is missing
                if (Camera.main != null)
                {
                    speaker.transform.position = Camera.main.transform.position;
                }
            }
        }
    }
}
