#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LightingScenarioTool.Editor
{
    public static class LightingScenarioSceneCreator
    {
        private const string ScenePath = "Assets/LightingScenarioTool/LightingScenarioDemo.unity";

        [MenuItem("Tools/Lighting Scenario/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(
                "LightingScenarioApp",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(LightingScenarioApp));

            var directory = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            Debug.Log("Lighting Scenario demo scene created: " + ScenePath);
        }
    }
}
#endif
