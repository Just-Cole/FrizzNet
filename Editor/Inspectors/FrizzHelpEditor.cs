using UnityEditor;
using UnityEngine;
using System.Reflection;
using FrizzNet.Core;
using FrizzNet.Steam;

namespace FrizzNet.Editor.Inspectors
{
    /// <summary>
    /// Base custom inspector editor that looks for FrizzHelpAttribute on target components
    /// and renders a stylized, neon-accented description banner at the top of their Inspector.
    /// </summary>
    public class FrizzHelpEditorBase : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // 1. Retrieve the FrizzHelpAttribute using reflection
            var targetType = target.GetType();
            var helpAttribute = targetType.GetCustomAttribute<FrizzHelpAttribute>();

            if (helpAttribute != null)
            {
                DrawHelpBanner(helpAttribute.Description, helpAttribute.DocLink);
            }

            // 2. Draw standard inspector variables
            DrawDefaultInspector();
        }

        private void DrawHelpBanner(string description, string docLink)
        {
            GUILayout.Space(4);

            // Start a vertical layout with custom padding
            Rect rect = EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(12, 12, 8, 8) });
            
            // Draw a solid flat dark background card
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.17f));
            
            // Draw a left accent strip (Neon Green)
            Rect accentRect = new Rect(rect.x, rect.y, 4f, rect.height);
            EditorGUI.DrawRect(accentRect, new Color(0.22f, 1f, 0.08f));

            // Header Row
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6); // spacing from left accent strip
            
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 10,
                richText = true
            };
            headerStyle.normal.textColor = new Color(0.22f, 1f, 0.08f); // Neon Green
            
            GUILayout.Label("⚡ FRIZZNET COMPONENT GUIDE", headerStyle);
            
            // Draw documentation link in the top-right corner to save space
            if (!string.IsNullOrEmpty(docLink))
            {
                GUILayout.FlexibleSpace();
                GUIStyle linkStyle = new GUIStyle(EditorStyles.linkLabel)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleRight
                };
                if (GUILayout.Button("Read Docs ↗", linkStyle))
                {
                    OpenLocalDocumentation(docLink);
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Description body
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6);
            
            GUIStyle descStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 11
            };
            descStyle.normal.textColor = new Color(0.85f, 0.85f, 0.87f); // Off-white
            
            GUILayout.Label(description, descStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(8);
        }

        private void OpenLocalDocumentation(string filename)
        {
            string[] parts = filename.Split('#');
            string baseFile = parts[0];
            string hash = parts.Length > 1 ? "#" + parts[1] : "";

            string filenameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(baseFile);
            string[] guids = AssetDatabase.FindAssets(filenameWithoutExt);
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(baseFile))
                {
                    string absPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", path));
                    Application.OpenURL("file:///" + absPath.Replace("\\", "/") + hash);
                    return;
                }
            }
            
            Debug.LogError($"[FrizzNet] Could not find documentation file: {baseFile}");
        }
    }

    // --- Custom Inspector Subclasses ---

    [CustomEditor(typeof(NetworkManager))]
    [CanEditMultipleObjects]
    public class NetworkManagerEditor : FrizzHelpEditorBase { }

    [CustomEditor(typeof(SteamTransport))]
    [CanEditMultipleObjects]
    public class SteamTransportEditor : FrizzHelpEditorBase { }

    [CustomEditor(typeof(NetworkIdentity))]
    [CanEditMultipleObjects]
    public class NetworkIdentityEditor : FrizzHelpEditorBase { }

    [CustomEditor(typeof(FrizzNetworkTransform))]
    [CanEditMultipleObjects]
    public class FrizzNetworkTransformEditor : FrizzHelpEditorBase { }

    [CustomEditor(typeof(FrizzVoiceManager))]
    [CanEditMultipleObjects]
    public class FrizzVoiceManagerEditor : FrizzHelpEditorBase { }

    [CustomEditor(typeof(FrizzPlayerSpawner))]
    [CanEditMultipleObjects]
    public class FrizzPlayerSpawnerEditor : FrizzHelpEditorBase { }
}
