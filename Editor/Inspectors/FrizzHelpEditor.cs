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
            GUILayout.Space(6);

            // Styled box card
            GUILayout.BeginVertical("box");
            GUILayout.Space(4);

            // Title with neon green color
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };
            headerStyle.normal.textColor = new Color(0.22f, 1f, 0.08f); // Neon Green

            GUILayout.Label("⚡ FRIZZNET COMPONENT GUIDE", headerStyle);
            GUILayout.Space(4);

            // Body text
            GUIStyle descStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 11,
                richText = true
            };
            descStyle.normal.textColor = new Color(0.85f, 0.85f, 0.87f); // Off-white

            GUILayout.Label(description, descStyle);

            // Documentation Link Button
            if (!string.IsNullOrEmpty(docLink))
            {
                GUILayout.Space(6);
                GUIStyle linkStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fontStyle = FontStyle.Bold,
                    fixedWidth = 160f,
                    fixedHeight = 20f
                };
                
                // Color button neon blue
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
                if (GUILayout.Button("Read API Documentation ↗", linkStyle))
                {
                    Application.OpenURL(docLink);
                }
                GUI.backgroundColor = oldBg;
            }

            GUILayout.Space(4);
            GUILayout.EndVertical();
            GUILayout.Space(6);
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
}
