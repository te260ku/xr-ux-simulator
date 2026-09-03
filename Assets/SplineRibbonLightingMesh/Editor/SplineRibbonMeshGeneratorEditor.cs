#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SplineRibbonLighting.Editor
{
    [CustomEditor(typeof(SplineRibbonMeshGenerator))]
    public sealed class SplineRibbonMeshGeneratorEditor : UnityEditor.Editor
    {
        private SplineRibbonMeshGenerator Generator => (SplineRibbonMeshGenerator)target;

        private void OnEnable() => Undo.undoRedoPerformed += HandleUndoRedo;
        private void OnDisable() => Undo.undoRedoPerformed -= HandleUndoRedo;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            SplineRibbonMeshGenerator generator = Generator;
            if (changed)
            {
                generator.EnsurePointSettings();
                TryRebuild(generator);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Meshes")) TryRebuild(generator);
                if (GUILayout.Button("Reset Point Offsets"))
                {
                    Undo.RecordObject(generator, "Reset Spline Point Offsets");
                    generator.ResetPointOffsets();
                    EditorUtility.SetDirty(generator);
                    TryRebuild(generator);
                    SceneView.RepaintAll();
                }
            }
            if (GUILayout.Button("Clear Generated Meshes")) generator.ClearGenerated();
        }

        private void OnSceneGUI()
        {
            SplineRibbonMeshGenerator generator = Generator;
            if (!generator.ShowSceneHandles || generator.SplineA == null || generator.SplineB == null) return;

            generator.EnsurePointSettings();
            generator.RebuildArcLengthTables();
            DrawBoundaryConnections(generator);

            for (int i = 0; i < generator.PointCount; i++)
            {
                DrawPoint(generator, SplineRibbonMeshGenerator.SplineSide.A, i);
                DrawPoint(generator, SplineRibbonMeshGenerator.SplineSide.B, i);
            }
        }

        private static void DrawBoundaryConnections(SplineRibbonMeshGenerator generator)
        {
            Color old = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            for (int i = 0; i < generator.PointCount; i++)
            {
                Vector3 a = generator.GetPointWorld(SplineRibbonMeshGenerator.SplineSide.A, i);
                Vector3 b = generator.GetPointWorld(SplineRibbonMeshGenerator.SplineSide.B, i);
                Handles.DrawDottedLine(a, b, 4f);
            }
            Handles.color = old;
        }

        private static void DrawPoint(SplineRibbonMeshGenerator generator, SplineRibbonMeshGenerator.SplineSide side, int pointIndex)
        {
            Vector3 position = generator.GetPointWorld(side, pointIndex);
            Vector3 tangent = generator.GetPointTangentWorld(side, pointIndex);
            float size = HandleUtility.GetHandleSize(position) * generator.HandleScreenSize;
            bool draggable = generator.IsPointDraggable(pointIndex);

            Color old = Handles.color;
            Handles.color = side == SplineRibbonMeshGenerator.SplineSide.A
                ? new Color(0.25f, 0.75f, 1f, 1f)
                : new Color(1f, 0.55f, 0.25f, 1f);

            string prefix = side == SplineRibbonMeshGenerator.SplineSide.A ? "A" : "B";
            Handles.Label(position + Vector3.up * size * 1.5f, $"{prefix}{pointIndex}");

            if (!draggable)
            {
                Handles.SphereHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
                Handles.color = old;
                return;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 dragged = Handles.Slider(position, tangent, size, Handles.SphereHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(generator, $"Move {prefix}{pointIndex} Along Spline");
                float distance = generator.ProjectHandleWorldPositionToLogicalDistance(side, pointIndex, dragged);
                generator.SetPointLogicalDistance(side, pointIndex, distance);
                EditorUtility.SetDirty(generator);
                TryRebuild(generator);
                SceneView.RepaintAll();
            }

            Handles.color = old;
        }

        private void HandleUndoRedo()
        {
            TryRebuild(Generator);
            SceneView.RepaintAll();
        }

        private static void TryRebuild(SplineRibbonMeshGenerator generator)
        {
            if (generator == null || generator.SplineA == null || generator.SplineB == null) return;
            try { generator.Rebuild(); }
            catch (System.Exception e) { Debug.LogException(e, generator); }
        }
    }
}
#endif
