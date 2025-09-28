using System.Collections;
using UnityEngine;

namespace ResourceAnalyze.AnalyzeResourcesSystem.Runtime
{
    // Basic Resources loading
    public class ResourcesExample : MonoBehaviour
    { 
        public string texturesPlayerAvatar = "Textures/PlayerAvatar";
        public string spritesCharacters = "Sprites/Characters";
        public string texturesLargeTexture = "Textures/LargeTexture";
        
        /// <summary>
        /// Synchronous loading
        /// </summary>
        [ContextMenu("LoadTextureSync")]
        void LoadTextureSync()
        {
            // Path relative to any Resources folder
            Texture2D texture = Resources.Load<Texture2D>(texturesPlayerAvatar);

            if (texture != null)
            {
                GetComponent<Renderer>().material.mainTexture = texture;
            }
        }


        /// <summary>
        /// Asynchronous loading
        /// </summary>
        /// <returns></returns>
        [ContextMenu("LoadTextureAsync")]
        IEnumerator LoadTextureAsync()
        {
            ResourceRequest request = Resources.LoadAsync<Texture2D>(texturesPlayerAvatar);

            while (!request.isDone)
            {
                float progress = request.progress;
                Debug.Log($"Loading progress: {progress * 100}%");
                yield return null;
            }

            Texture2D texture = request.asset as Texture2D;
            if (texture != null)
            {
                GetComponent<Renderer>().material.mainTexture = texture;
            }
        }
        
        /// <summary>
        /// Loading all assets of type
        /// </summary>
        [ContextMenu("LoadAllSprites")]
        void LoadAllSprites()
        {
            Sprite[] allSprites = Resources.LoadAll<Sprite>(spritesCharacters);
            foreach (var sprite in allSprites)
            {
                Debug.Log($"Loaded sprite: {sprite.name}");
            }
        }


        /// <summary>
        /// Memory management
        /// </summary>
        [ContextMenu("UnloadUnusedAssets")]
        void UnloadUnusedAssets()
        {
            // Unload a specific asset
            Texture2D texture = Resources.Load<Texture2D>(texturesLargeTexture);
            // Use the texture...

            // Explicitly unload
            Resources.UnloadAsset(texture);

            // Or unload all unused assets
            Resources.UnloadUnusedAssets();
        }

    }
}