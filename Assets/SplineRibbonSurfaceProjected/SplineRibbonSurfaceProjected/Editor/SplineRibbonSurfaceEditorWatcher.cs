#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SplineRibbonLightingNew.Editor
{
    /// <summary>
    /// Keeps manually edited Unity spline knots on the reference surface and
    /// rebuilds lighting meshes even when the user is editing the Spline
    /// GameObject itself rather than selecting the generator.
    ///
    /// Polling is deliberately throttled. It runs only in Edit Mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class SplineRibbonSurfaceEditorWatcher
    {
        private static readonly Dictionary<int, int> LastHashes =
            new Dictionary<int, int>();

        private static double _nextCheckTime;

        static SplineRibbonSurfaceEditorWatcher()
        {
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (Application.isPlaying)
                return;

            if (EditorApplication.timeSinceStartup <
                _nextCheckTime)
            {
                return;
            }

            _nextCheckTime =
                EditorApplication.timeSinceStartup +
                0.08;

            SplineRibbonMeshGenerator[] generators =
                Object.FindObjectsByType<SplineRibbonMeshGenerator>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            var aliveIds = new HashSet<int>();

            foreach (SplineRibbonMeshGenerator generator
                     in generators)
            {
                if (generator == null)
                    continue;

                int id =
                    generator.GetInstanceID();

                aliveIds.Add(id);

                int beforeHash =
                    generator.GetEditorStateHash();

                if (!LastHashes.TryGetValue(
                        id,
                        out int previousHash))
                {
                    LastHashes[id] = beforeHash;
                    continue;
                }

                if (beforeHash == previousHash)
                    continue;

                bool shouldSnap =
                    generator.ConstrainSplineKnotsToSurface &&
                    generator.SourceSurface != null;

                if (shouldSnap)
                {
                    SplineRibbonMeshGeneratorEditor
                        .RecordSplineUndo(
                            generator,
                            "Constrain Spline Knots To Surface");

                    bool snapped =
                        generator.SnapSplineKnotsToSurface();

                    if (snapped)
                    {
                        SplineRibbonMeshGeneratorEditor
                            .MarkSplinesDirty(generator);
                    }
                }

                generator.InvalidateCaches();

                if (generator.AutoRebuildWhenSplinesChange)
                {
                    SplineRibbonMeshGeneratorEditor
                        .TryRebuild(generator);
                }

                LastHashes[id] =
                    generator.GetEditorStateHash();

                SceneView.RepaintAll();
            }

            if (LastHashes.Count != aliveIds.Count)
            {
                var removed =
                    new List<int>();

                foreach (int id in LastHashes.Keys)
                {
                    if (!aliveIds.Contains(id))
                        removed.Add(id);
                }

                foreach (int id in removed)
                    LastHashes.Remove(id);
            }
        }
    }
}
#endif
