using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using FrizzNet.Messaging;
using FrizzNet.Logging;

namespace FrizzNet.Core
{
    /// <summary>
    /// Host-authoritative networked scene loading. Persists across scenes when placed on the NetworkManager object.
    /// </summary>
    [DisallowMultipleComponent]
    public class FrizzNetworkSceneManager : MonoBehaviour
    {
        public static FrizzNetworkSceneManager Instance { get; private set; }

        [Header("Scene Settings")]
        [Tooltip("If true, automatically registers the scene load handler on the NetworkManager.")]
        [SerializeField] private bool m_AutoRegister = true;

        public static event Action<string, LoadSceneMode> OnSceneLoadStarted;
        public static event Action<string, LoadSceneMode> OnSceneLoadCompleted;

        private bool m_IsLoading;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (m_AutoRegister && NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RegisterHandler(FrizzSystemMessages.SceneLoad, HandleSceneLoadMessage);
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.UnregisterHandler(FrizzSystemMessages.SceneLoad);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Loads a scene on the host and instructs all clients to load the same scene.
        /// </summary>
        public void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsHost)
            {
                FrizzLogger.LogError("Only the host can initiate networked scene loads.");
                return;
            }

            BroadcastSceneLoad(sceneName, mode);
            LoadSceneLocal(sceneName, mode);
        }

        /// <summary>
        /// Loads a scene locally without notifying remote clients.
        /// </summary>
        public void LoadSceneLocal(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (m_IsLoading) return;
            m_IsLoading = true;

            OnSceneLoadStarted?.Invoke(sceneName, mode);
            SceneManager.LoadScene(sceneName, mode);
            OnSceneLoadCompleted?.Invoke(sceneName, mode);
            m_IsLoading = false;
        }

        private void BroadcastSceneLoad(string sceneName, LoadSceneMode mode)
        {
            using (MessageWriter writer = new MessageWriter())
            {
                writer.WriteString(sceneName);
                writer.WriteInt((int)mode);
                NetworkManager.Instance.SendToAll(FrizzSystemMessages.SceneLoad, writer, true);
            }
        }

        private void HandleSceneLoadMessage(ulong connectionId, MessageReader reader)
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                return;
            }

            string sceneName = reader.ReadString();
            LoadSceneMode mode = (LoadSceneMode)reader.ReadInt();
            LoadSceneLocal(sceneName, mode);
        }
    }
}
