using UnityEngine;
using UnityEditor;
using System.IO;
using FrizzNet.Core;
using FrizzNet.Samples;

namespace FrizzNet.Samples.Editor
{
    /// <summary>
    /// Editor tool to procedurally generate ready-to-use sample prefabs for the LobbyExample scene,
    /// complete with correct materials, colliders, rigidbodies, and FrizzNet identity scripts.
    /// </summary>
    public static class FrizzNetPrefabGenerator
    {
        [MenuItem("Tools/FrizzNet/Generate Sample Prefabs")]
        public static void GeneratePrefabs()
        {
            string folderPath = "Assets/FrizzNet/Samples/LobbyExample/Prefabs";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // --- 1. Generate Player Prefab ---
            string playerPrefabPath = $"{folderPath}/FrizzNetDemoPlayer.prefab";
            GameObject playerGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerGo.name = "FrizzNetDemoPlayer";

            // Configure Trigger Collider
            BoxCollider bc = playerGo.GetComponent<BoxCollider>();
            if (bc != null)
            {
                bc.isTrigger = true;
            }

            // Configure Kinematic Rigidbody
            Rigidbody playerRb = playerGo.AddComponent<Rigidbody>();
            playerRb.isKinematic = true;
            playerRb.useGravity = false;

            // Add FrizzNet Components
            playerGo.AddComponent<NetworkIdentity>();
            playerGo.AddComponent<FrizzNetworkTransform>();
            playerGo.AddComponent<DemoPlayerController>();

            // Save player prefab
            PrefabUtility.SaveAsPrefabAsset(playerGo, playerPrefabPath);
            Object.DestroyImmediate(playerGo);

            // --- 2. Generate Emissive Cyan Material ---
            string materialPath = $"{folderPath}/CyanNeon.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0f, 0.9f, 1f);
                mat.SetFloat("_Metallic", 0.1f);
                mat.SetFloat("_Glossiness", 0.8f);
                
                // Emissive neon glow parameters
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0f, 0.45f, 0.5f));
                
                AssetDatabase.CreateAsset(mat, materialPath);
            }

            // --- 3. Generate Resource Prefab ---
            string resourcePrefabPath = $"{folderPath}/FrizzNetDemoResource.prefab";
            GameObject resourceGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            resourceGo.name = "FrizzNetDemoResource";

            // Configure Trigger Collider
            SphereCollider sc = resourceGo.GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
            }

            // Apply Cyan Neon Material
            Renderer r = resourceGo.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
            }

            // Add FrizzNet Components
            resourceGo.AddComponent<NetworkIdentity>();
            resourceGo.AddComponent<DemoResource>();

            // Save resource prefab
            PrefabUtility.SaveAsPrefabAsset(resourceGo, resourcePrefabPath);
            Object.DestroyImmediate(resourceGo);

            // Refresh AssetDatabase and alert user
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("FrizzNet Prefabs", 
                $"Sample prefabs generated successfully at:\n{folderPath}\n\nMake sure to register them in the NetworkManager's Spawnable Prefabs list!", 
                "OK");
        }
    }
}
