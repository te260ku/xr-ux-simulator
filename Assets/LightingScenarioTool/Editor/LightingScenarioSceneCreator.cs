#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace LightingScenarioTool.Editor
{
    public static class LightingScenarioSceneCreator
    {
        private const string BuildMenuPath = "Tools/Lighting Scenario/Build Complete UI In Current Scene";

        [MenuItem(BuildMenuPath)]
        public static void BuildCompleteUiInCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Build Complete UI In Current Scene can only be executed in Edit Mode.");
                return;
            }

            var app = Object.FindFirstObjectByType<LightingScenarioApp>(FindObjectsInactive.Include);
            if (app == null)
            {
                RemoveStaleRootWithoutAppComponent();
                app = CreateAppRoot();
                Undo.RegisterCreatedObjectUndo(app.gameObject, "Create Lighting Scenario UI");
            }
            else
            {
                Undo.RegisterFullObjectHierarchyUndo(app.gameObject, "Build Complete Lighting Scenario UI");
                EnsureRequiredRootComponents(app.gameObject);
            }

            // The command is the single source of truth for authored UI. Remove any stale
            // missing behaviours first, then regenerate the static scene shell and runtime Prefab Assets deterministically.
            RemoveMissingScriptsRecursively(app.gameObject);
            EnsureEventSystem();

            var prefabCatalog = LightingScenarioUiPrefabBuilder.BuildOrUpdate();
            app.EditorSetUiPrefabCatalog(prefabCatalog);
            app.EditorBuildCompleteUiHierarchy();

            MarkHierarchyDirty(app.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = app.gameObject;

            Debug.Log(
                "Complete Lighting Scenario UI and runtime widget prefabs generated. " +
                "Running the command again updates the prefab assets and replaces the generated UI under LightingScenarioApp with the default layout.");
        }

        private static void MarkHierarchyDirty(GameObject root)
        {
            if (root == null) return;

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == null) continue;
                EditorUtility.SetDirty(transform.gameObject);
                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component != null) EditorUtility.SetDirty(component);
                }
            }
        }

        private static void RemoveMissingScriptsRecursively(GameObject root)
        {
            if (root == null) return;

            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item == null) continue;
                var go = item.gameObject;
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            }
        }

        private static void RemoveStaleRootWithoutAppComponent()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null || root.name != "LightingScenarioApp") continue;
                if (root.GetComponent<LightingScenarioApp>() != null) return;

                Undo.DestroyObjectImmediate(root);
                return;
            }
        }

        private static LightingScenarioApp CreateAppRoot()
        {
            var root = new GameObject(
                "LightingScenarioApp",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(AppTheme),
                typeof(LightingScenarioApp));

            ConfigureAppRoot(root);
            return root.GetComponent<LightingScenarioApp>();
        }

        private static void EnsureRequiredRootComponents(GameObject root)
        {
            if (root == null) return;

            EnsureComponent<Canvas>(root);
            EnsureComponent<CanvasScaler>(root);
            EnsureComponent<GraphicRaycaster>(root);
            EnsureComponent<AppTheme>(root);
            ConfigureAppRoot(root);
        }

        private static T EnsureComponent<T>(GameObject root) where T : Component
        {
            var component = root.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(root);
        }

        private static void ConfigureAppRoot(GameObject root)
        {
            if (root == null) return;

            var canvas = root.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = true;
            }

            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (root.transform is RectTransform rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                eventSystem = go.GetComponent<EventSystem>();
            }

            if (eventSystem.GetComponent<BaseInputModule>() != null) return;

#if ENABLE_INPUT_SYSTEM
            Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
#else
            Undo.AddComponent<StandaloneInputModule>(eventSystem.gameObject);
#endif
        }
    }
}
#endif
