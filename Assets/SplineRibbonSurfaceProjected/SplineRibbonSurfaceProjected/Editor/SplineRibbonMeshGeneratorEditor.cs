#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SplineRibbonLightingNew.Editor
{
    [CustomEditor(typeof(SplineRibbonMeshGenerator))]
    public sealed class SplineRibbonMeshGeneratorEditor :
        UnityEditor.Editor
    {
        private SplineRibbonMeshGenerator Generator =>
            (SplineRibbonMeshGenerator)target;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed =
                EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();

            SplineRibbonMeshGenerator generator =
                Generator;

            if (changed)
            {
                generator.EnsurePointSettings();
                generator.InvalidateCaches();
                TryRebuild(generator);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button(
                    "Snap Spline Knots To Surface"))
            {
                RecordSplineUndo(
                    generator,
                    "Snap Spline Knots To Surface");

                generator.SnapSplineKnotsToSurface();
                EditorUtility.SetDirty(generator);

                MarkSplinesDirty(generator);
                TryRebuild(generator);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Meshes"))
                {
                    TryRebuild(generator);
                }

                if (GUILayout.Button(
                        "Reset Point Offsets"))
                {
                    Undo.RecordObject(
                        generator,
                        "Reset Numbered Point Offsets");

                    generator.ResetPointOffsets();
                    EditorUtility.SetDirty(generator);
                    TryRebuild(generator);
                }
            }

            if (GUILayout.Button(
                    "Clear Generated Meshes"))
            {
                generator.ClearGenerated();
            }

            EditorGUILayout.HelpBox(
                "Workflow:\n" +
                "1. Add a MeshCollider to the existing curved surface.\n" +
                "2. Create Spline A/B manually on that surface.\n" +
                "3. The generated grid is projected onto Source Surface.\n" +
                "4. Drag numbered A/B handles along each spline to adjust unit boundaries.\n\n" +
                "UV0.y remains B/front=0 and A/back=1 after projection.",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            SplineRibbonMeshGenerator generator =
                Generator;

            if (!generator.ShowSceneHandles ||
                generator.SplineA == null ||
                generator.SplineB == null)
            {
                return;
            }

            generator.EnsurePointSettings();
            generator.RebuildArcLengthTables();

            DrawConnections(generator);

            for (int i = 0;
                 i < generator.PointCount;
                 i++)
            {
                DrawPointHandle(
                    generator,
                    SplineRibbonMeshGenerator.SplineSide.A,
                    i);

                DrawPointHandle(
                    generator,
                    SplineRibbonMeshGenerator.SplineSide.B,
                    i);
            }
        }

        private static void DrawConnections(
            SplineRibbonMeshGenerator generator)
        {
            Color previous = Handles.color;

            Handles.color =
                new Color(
                    1f,
                    1f,
                    1f,
                    0.35f);

            for (int i = 0;
                 i < generator.PointCount;
                 i++)
            {
                Vector3 a =
                    generator.GetPointWorld(
                        SplineRibbonMeshGenerator.SplineSide.A,
                        i);

                Vector3 b =
                    generator.GetPointWorld(
                        SplineRibbonMeshGenerator.SplineSide.B,
                        i);

                Handles.DrawDottedLine(
                    a,
                    b,
                    4f);
            }

            Handles.color = previous;
        }

        private static void DrawPointHandle(
            SplineRibbonMeshGenerator generator,
            SplineRibbonMeshGenerator.SplineSide side,
            int pointIndex)
        {
            Vector3 position =
                generator.GetPointWorld(
                    side,
                    pointIndex);

            Vector3 tangent =
                generator.GetPointTangentWorld(
                    side,
                    pointIndex);

            float size =
                HandleUtility.GetHandleSize(position) *
                generator.HandleScreenSize;

            bool draggable =
                generator.IsPointDraggable(
                    pointIndex);

            Color previous = Handles.color;

            Handles.color =
                side ==
                SplineRibbonMeshGenerator.SplineSide.A
                    ? new Color(
                        0.25f,
                        0.75f,
                        1f,
                        1f)
                    : new Color(
                        1f,
                        0.55f,
                        0.25f,
                        1f);

            string prefix =
                side ==
                SplineRibbonMeshGenerator.SplineSide.A
                    ? "A"
                    : "B";

            Handles.Label(
                position +
                Vector3.up * size * 1.4f,
                $"{prefix}{pointIndex}");

            if (!draggable)
            {
                Handles.SphereHandleCap(
                    0,
                    position,
                    Quaternion.identity,
                    size,
                    EventType.Repaint);

                Handles.color = previous;
                return;
            }

            EditorGUI.BeginChangeCheck();

            Vector3 dragged =
                Handles.Slider(
                    position,
                    tangent,
                    size,
                    Handles.SphereHandleCap,
                    0f);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(
                    generator,
                    $"Move {prefix}{pointIndex} Along Spline");

                float logicalDistance =
                    generator
                        .ProjectHandleWorldPositionToLogicalDistance(
                            side,
                            pointIndex,
                            dragged);

                generator.SetPointLogicalDistance(
                    side,
                    pointIndex,
                    logicalDistance);

                EditorUtility.SetDirty(generator);
                TryRebuild(generator);
                SceneView.RepaintAll();
            }

            Handles.color = previous;
        }

        private void HandleUndoRedo()
        {
            Generator.InvalidateCaches();
            TryRebuild(Generator);
            SceneView.RepaintAll();
        }

        internal static void RecordSplineUndo(
            SplineRibbonMeshGenerator generator,
            string undoName)
        {
            if (generator.SplineA != null)
                Undo.RecordObject(generator.SplineA, undoName);

            if (generator.SplineB != null &&
                generator.SplineB != generator.SplineA)
            {
                Undo.RecordObject(generator.SplineB, undoName);
            }
        }

        internal static void MarkSplinesDirty(
            SplineRibbonMeshGenerator generator)
        {
            if (generator.SplineA != null)
                EditorUtility.SetDirty(generator.SplineA);

            if (generator.SplineB != null)
                EditorUtility.SetDirty(generator.SplineB);
        }

        internal static void TryRebuild(
            SplineRibbonMeshGenerator generator)
        {
            if (generator == null ||
                generator.SourceSurface == null ||
                generator.SplineA == null ||
                generator.SplineB == null)
            {
                return;
            }

            try
            {
                generator.Rebuild();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(
                    exception,
                    generator);
            }
        }
    }
}
#endif
