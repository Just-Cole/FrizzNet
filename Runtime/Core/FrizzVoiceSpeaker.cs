using System.Collections.Generic;
using UnityEngine;

namespace FrizzNet.Core
{
    /// <summary>
    /// Component representing a remote player's voice playback stream.
    /// Uses a procedural streaming AudioClip and thread-safe sample queue to play audio smoothly.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public class FrizzVoiceSpeaker : MonoBehaviour
    {
        private ulong m_SenderId;
        private AudioSource m_AudioSource;
        private AudioClip m_AudioClip;
        private readonly Queue<float> m_SampleQueue = new Queue<float>();
        private readonly object m_QueueLock = new object();
        private uint m_SampleRate;
        private float m_LastDataReceivedTime;

        // Properties
        public ulong SenderId => m_SenderId;
        public bool IsPlaying => m_AudioSource != null && m_AudioSource.isPlaying && (Time.time - m_LastDataReceivedTime < 0.2f);

        /// <summary>
        /// Initializes the speaker with client info and creates a procedural audio clip.
        /// </summary>
        public void Initialize(ulong senderId, AudioSource source, uint sampleRate)
        {
            m_SenderId = senderId;
            m_AudioSource = source;
            m_SampleRate = sampleRate;
            m_LastDataReceivedTime = Time.time;

            // Create a procedural streaming audio clip (1-second circular buffer size)
            m_AudioClip = AudioClip.Create($"VoiceClip_{senderId}", (int)sampleRate, 1, (int)sampleRate, true, OnAudioRead);
            m_AudioSource.clip = m_AudioClip;
            m_AudioSource.Play();
        }

        /// <summary>
        /// Enqueues decompressed float audio samples to be played by the AudioSource.
        /// </summary>
        public void EnqueueSamples(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            EnqueueSamples(samples, samples.Length);
        }

        /// <summary>
        /// Enqueues the first <paramref name="count"/> decompressed samples.
        /// </summary>
        public void EnqueueSamples(float[] samples, int count)
        {
            if (samples == null || count <= 0)
            {
                return;
            }

            count = Mathf.Min(count, samples.Length);

            lock (m_QueueLock)
            {
                // Cap queue to 2 seconds of audio to prevent delay/echo buildup from network jitter
                if (m_SampleQueue.Count > m_SampleRate * 2)
                {
                    m_SampleQueue.Clear();
                }

                for (int i = 0; i < count; i++)
                {
                    m_SampleQueue.Enqueue(samples[i]);
                }
            }
            m_LastDataReceivedTime = Time.time;
        }

        /// <summary>
        /// PCMReaderCallback called by Unity's audio thread when it needs more samples.
        /// </summary>
        private void OnAudioRead(float[] data)
        {
            lock (m_QueueLock)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (m_SampleQueue.Count > 0)
                    {
                        data[i] = m_SampleQueue.Dequeue();
                    }
                    else
                    {
                        data[i] = 0f; // Output silence if we have run out of buffered voice packets
                    }
                }
            }
        }

        private void Update()
        {
            // Auto-cleanup speaker if silent/inactive for more than 5 seconds (player stopped talking / left)
            if (Time.time - m_LastDataReceivedTime > 5f)
            {
                if (m_AudioSource != null)
                {
                    m_AudioSource.Stop();
                }
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (m_AudioSource != null)
            {
                m_AudioSource.Stop();
            }
            if (m_AudioClip != null)
            {
                Destroy(m_AudioClip);
            }
            lock (m_QueueLock)
            {
                m_SampleQueue.Clear();
            }
        }
    }
}
