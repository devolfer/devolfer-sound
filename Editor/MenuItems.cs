using UnityEditor;
using UnityEngine;

namespace Devolfer.Sound
{
    public class MenuItems
    {
        [MenuItem("GameObject/Audio/Sound Manager", priority = 1, secondaryPriority = 0)]
        private static void CreateSoundManager()
        {
            GameObject newGameObject = new("SoundManager", typeof(SoundManager));
            Selection.activeGameObject = newGameObject;

            SoundManager[] managers = Object.FindObjectsByType<SoundManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (managers.Length > 1)
                Debug.Log($"There are more than one {nameof(SoundManager)} instances in the scene ({managers.Length})");
        }
        
        [MenuItem("GameObject/Audio/Sound Emitter", priority = 1, secondaryPriority = 1)]
        private static void CreateSoundEmitter()
        {
            GameObject newGameObject = new("SoundEmitter", typeof(SoundEmitter));
            Selection.activeGameObject = newGameObject;
        }
    }
}