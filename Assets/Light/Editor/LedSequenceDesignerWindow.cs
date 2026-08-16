using UnityEditor;
using UnityEngine;

public sealed class LedSequenceDesignerWindow : EditorWindow
{
    private const float LabelWidth = 160f;
    private const float RulerHeight = 28f;
    private const float RowHeight = 42f;
    private const float ResizeHandleWidth = 8f;

    private LedSequence _sequence;
    private SerializedObject _serializedSequence;
    private SerializedProperty _clipsProperty;

    private int _selectedClipIndex = -1;

    private DragMode _dragMode = DragMode.None;
    private int _draggingClipIndex = -1;

    private float _dragOffsetTime;
    private readonly LedSequencePreviewController
        _previewController = new();

    private float _currentTime;

    private bool _isPlaying;

    private bool _isScrubbing;

    private double _lastEditorTime;

    private enum DragMode
    {
        None,
        Move,
        ResizeRight
    }


    [MenuItem("Window/LED/Sequence Designer")]
    private static void Open()
    {
        var window =
            GetWindow<LedSequenceDesignerWindow>();

        window.titleContent =
            new GUIContent("LED Sequence Designer");
    }

    private void OnEnable()
    {
        EditorApplication.update +=
            OnEditorUpdate;
    }


    private void OnDisable()
    {
        EditorApplication.update -=
            OnEditorUpdate;

        _previewController.Stop();
    }

    private void OnEditorUpdate()
    {
        if (!_isPlaying ||
            _sequence == null)
        {
            return;
        }

        double now =
            EditorApplication.timeSinceStartup;

        float deltaTime =
            (float)(now - _lastEditorTime);

        _lastEditorTime = now;

        float duration =
            Mathf.Max(
                0.1f,
                _sequence.Duration);

        _currentTime += deltaTime;


        if (_sequence.Loop)
        {
            _currentTime =
                Mathf.Repeat(
                    _currentTime,
                    duration);
        }
        else if (_currentTime >= duration)
        {
            _currentTime = duration;
            _isPlaying = false;
        }


        ApplyPreview();

        Repaint();
    }

    private void ApplyPreview()
    {
        if (_sequence == null)
        {
            return;
        }

        _previewController.Apply(
            _sequence,
            _currentTime);
    }

    private void Play(
        float sequenceDuration)
    {
        if (_sequence == null)
        {
            return;
        }

        if (_currentTime >= sequenceDuration)
        {
            _currentTime = 0f;
        }

        _isPlaying = true;

        _lastEditorTime =
            EditorApplication.timeSinceStartup;

        ApplyPreview();
    }


    private void Pause()
    {
        _isPlaying = false;
    }


    private void Stop()
    {
        _isPlaying = false;

        _currentTime = 0f;

        _previewController.Stop();

        Repaint();
    }

    private void DrawPlaybackControls(
        float sequenceDuration)
    {
        EditorGUILayout.BeginHorizontal(
            EditorStyles.toolbar);

        if (!_isPlaying)
        {
            if (GUILayout.Button(
                    "▶ Play",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(60f)))
            {
                Play(sequenceDuration);
            }
        }
        else
        {
            if (GUILayout.Button(
                    "Ⅱ Pause",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(60f)))
            {
                Pause();
            }
        }


        if (GUILayout.Button(
                "■ Stop",
                EditorStyles.toolbarButton,
                GUILayout.Width(60f)))
        {
            Stop();
        }


        GUILayout.Space(10f);

        GUILayout.Label(
            $"{_currentTime:0.00} / " +
            $"{sequenceDuration:0.00} sec",
            EditorStyles.miniLabel);

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
    }


    private void OnGUI()
    {
        DrawToolbar();

        if (_sequence == null)
        {
            EditorGUILayout.HelpBox(
                "LedSequenceを選択してください。",
                MessageType.Info);

            return;
        }

        EnsureSerializedObject();

        _serializedSequence.Update();

        SerializedProperty durationProperty =
            _serializedSequence.FindProperty(
                "_duration");

        float sequenceDuration =
            Mathf.Max(
                0.1f,
                durationProperty.floatValue);

        DrawPlaybackControls(
            sequenceDuration);

        DrawTimeline(
            sequenceDuration);

        _serializedSequence
            .ApplyModifiedProperties();
    }


    // ============================================================
    // Toolbar
    // ============================================================

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(
            EditorStyles.toolbar);

        EditorGUI.BeginChangeCheck();

        LedSequence newSequence =
            (LedSequence)EditorGUILayout.ObjectField(
                _sequence,
                typeof(LedSequence),
                false,
                GUILayout.Width(240f));

        if (EditorGUI.EndChangeCheck())
        {
            SetSequence(newSequence);
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(
                "Use Selected",
                EditorStyles.toolbarButton))
        {
            SetSequence(
                Selection.activeObject
                as LedSequence);
        }

        EditorGUILayout.EndHorizontal();
    }


    private void SetSequence(
        LedSequence sequence)
    {
        _sequence = sequence;

        _selectedClipIndex = -1;
        _draggingClipIndex = -1;
        _dragMode = DragMode.None;

        if (_sequence == null)
        {
            _serializedSequence = null;
            _clipsProperty = null;
            return;
        }

        _serializedSequence =
            new SerializedObject(_sequence);

        _clipsProperty =
            _serializedSequence.FindProperty(
                "_clips");

        Repaint();
    }


    private void EnsureSerializedObject()
    {
        if (_serializedSequence != null)
        {
            return;
        }

        _serializedSequence =
            new SerializedObject(_sequence);

        _clipsProperty =
            _serializedSequence.FindProperty(
                "_clips");
    }


    // ============================================================
    // Timeline
    // ============================================================

    private void DrawTimeline(
        float sequenceDuration)
    {
        float height =
            RulerHeight +
            _clipsProperty.arraySize *
            RowHeight;

        Rect fullRect =
            GUILayoutUtility.GetRect(
                0f,
                height,
                GUILayout.ExpandWidth(true));

        float timelineWidth =
            Mathf.Max(
                1f,
                fullRect.width - LabelWidth);

        Rect rulerRect =
            new Rect(
                fullRect.x + LabelWidth,
                fullRect.y,
                timelineWidth,
                RulerHeight);

        DrawRuler(
            rulerRect,
            sequenceDuration);

        float y =
            rulerRect.yMax;

        for (int i = 0;
            i < _clipsProperty.arraySize;
            i++)
        {
            Rect rowRect =
                new Rect(
                    fullRect.x,
                    y,
                    fullRect.width,
                    RowHeight);

            DrawClipRow(
                i,
                rowRect,
                sequenceDuration);

            y += RowHeight;
        }

        DrawPlayhead(
            rulerRect,
            y,
            sequenceDuration);

        HandleScrubbing(
            rulerRect,
            sequenceDuration);

        HandleMouseUp();
    }


    private void DrawPlayhead(
        Rect rulerRect,
        float bottom,
        float sequenceDuration)
    {
        float x =
            TimeToPixel(
                _currentTime,
                rulerRect,
                sequenceDuration);

        // 縦線
        EditorGUI.DrawRect(
            new Rect(
                x - 1f,
                rulerRect.y,
                2f,
                bottom - rulerRect.y),
            new Color(
                1f,
                0.35f,
                0.25f));

        // 上の三角形代わりのHandle
        EditorGUI.DrawRect(
            new Rect(
                x - 4f,
                rulerRect.y,
                8f,
                8f),
            new Color(
                1f,
                0.35f,
                0.25f));
    }

    private void HandleScrubbing(
        Rect rulerRect,
        float sequenceDuration)
    {
        Event current =
            Event.current;


        // ----------------------------
        // Scrub開始
        // ----------------------------

        if (current.type == EventType.MouseDown &&
            current.button == 0 &&
            rulerRect.Contains(
                current.mousePosition))
        {
            _isPlaying = false;

            _isScrubbing = true;

            SetCurrentTimeFromMouse(
                current.mousePosition.x,
                rulerRect,
                sequenceDuration);

            current.Use();

            return;
        }


        // ----------------------------
        // Scrub中
        // ----------------------------

        if (current.type == EventType.MouseDrag &&
            current.button == 0 &&
            _isScrubbing)
        {
            SetCurrentTimeFromMouse(
                current.mousePosition.x,
                rulerRect,
                sequenceDuration);

            current.Use();

            return;
        }


        // ----------------------------
        // Scrub終了
        // ----------------------------

        if (current.type == EventType.MouseUp &&
            current.button == 0 &&
            _isScrubbing)
        {
            _isScrubbing = false;

            current.Use();
        }
    }


    private void SetCurrentTimeFromMouse(
        float mouseX,
        Rect rulerRect,
        float sequenceDuration)
    {
        _currentTime =
            Mathf.Clamp(
                PixelToTime(
                    mouseX,
                    rulerRect,
                    sequenceDuration),
                0f,
                sequenceDuration);

        ApplyPreview();

        Repaint();
    }


    // ============================================================
    // Ruler
    // ============================================================

    private void DrawRuler(
        Rect rect,
        float duration)
    {
        EditorGUI.DrawRect(
            rect,
            new Color(
                0.16f,
                0.16f,
                0.16f));

        int divisionCount =
            Mathf.Max(
                1,
                Mathf.CeilToInt(duration));

        for (int second = 0;
             second <= divisionCount;
             second++)
        {
            float t =
                Mathf.Min(
                    second,
                    duration);

            float normalized =
                t / duration;

            float x =
                rect.x +
                normalized *
                rect.width;

            EditorGUI.DrawRect(
                new Rect(
                    x,
                    rect.yMax - 8f,
                    1f,
                    8f),
                Color.gray);

            GUI.Label(
                new Rect(
                    x + 3f,
                    rect.y,
                    50f,
                    20f),
                $"{t:0.#}s",
                EditorStyles.miniLabel);

            if (t >= duration)
            {
                break;
            }
        }
    }


    // ============================================================
    // Clip row
    // ============================================================

    private void DrawClipRow(
        int index,
        Rect rowRect,
        float sequenceDuration)
    {
        SerializedProperty clipProperty =
            _clipsProperty
                .GetArrayElementAtIndex(index);

        SerializedProperty startProperty =
            clipProperty.FindPropertyRelative(
                "_startTime");

        SerializedProperty durationProperty =
            clipProperty.FindPropertyRelative(
                "_duration");

        SerializedProperty effectProperty =
            clipProperty.FindPropertyRelative(
                "_effectType");

        SerializedProperty targetProperty =
            clipProperty.FindPropertyRelative(
                "_targetLedIds");

        DrawRowBackground(
            rowRect,
            index);

        // ----------------------------
        // 左側ラベル
        // ----------------------------

        Rect labelRect =
            new Rect(
                rowRect.x,
                rowRect.y,
                LabelWidth,
                rowRect.height);

        string effectName =
            effectProperty.enumDisplayNames[
                effectProperty.enumValueIndex];

        string label =
            $"{effectName}  ·  LED x{targetProperty.arraySize}";

        GUI.Label(
            labelRect,
            label,
            EditorStyles.label);


        // ----------------------------
        // Timeline領域
        // ----------------------------

        Rect timelineRect =
            new Rect(
                LabelWidth,
                rowRect.y,
                rowRect.width - LabelWidth,
                rowRect.height);

        DrawTimelineBackground(
            timelineRect,
            sequenceDuration);


        // ----------------------------
        // Clip
        // ----------------------------

        Rect clipRect =
            GetClipRect(
                timelineRect,
                startProperty.floatValue,
                durationProperty.floatValue,
                sequenceDuration);

        DrawClip(
            clipRect,
            effectName,
            index);

        HandleClipInput(
            index,
            clipRect,
            timelineRect,
            sequenceDuration,
            startProperty,
            durationProperty);
    }


    private void DrawRowBackground(
        Rect rowRect,
        int index)
    {
        Color color =
            index % 2 == 0
                ? new Color(
                    0.20f,
                    0.20f,
                    0.20f)
                : new Color(
                    0.18f,
                    0.18f,
                    0.18f);

        EditorGUI.DrawRect(
            rowRect,
            color);
    }


    private static void DrawTimelineBackground(
        Rect timelineRect,
        float duration)
    {
        EditorGUI.DrawRect(
            timelineRect,
            new Color(
                0.12f,
                0.12f,
                0.12f));

        int secondCount =
            Mathf.CeilToInt(duration);

        for (int second = 0;
             second <= secondCount;
             second++)
        {
            float time =
                Mathf.Min(
                    second,
                    duration);

            float x =
                TimeToPixel(
                    time,
                    timelineRect,
                    duration);

            EditorGUI.DrawRect(
                new Rect(
                    x,
                    timelineRect.y,
                    1f,
                    timelineRect.height),
                new Color(
                    0.3f,
                    0.3f,
                    0.3f,
                    0.4f));

            if (time >= duration)
            {
                break;
            }
        }
    }


    private static Rect GetClipRect(
        Rect timelineRect,
        float startTime,
        float clipDuration,
        float sequenceDuration)
    {
        float startX =
            TimeToPixel(
                startTime,
                timelineRect,
                sequenceDuration);

        float endX =
            TimeToPixel(
                startTime + clipDuration,
                timelineRect,
                sequenceDuration);

        return new Rect(
            startX,
            timelineRect.y + 5f,
            Mathf.Max(
                4f,
                endX - startX),
            timelineRect.height - 10f);
    }


    private void DrawClip(
        Rect clipRect,
        string effectName,
        int index)
    {
        bool selected =
            index == _selectedClipIndex;

        Color color =
            selected
                ? new Color(
                    0.30f,
                    0.55f,
                    0.85f)
                : new Color(
                    0.25f,
                    0.42f,
                    0.65f);

        EditorGUI.DrawRect(
            clipRect,
            color);

        GUI.Label(
            new Rect(
                clipRect.x + 6f,
                clipRect.y,
                clipRect.width - 12f,
                clipRect.height),
            effectName,
            EditorStyles.whiteLabel);

        Rect resizeHandle =
            new Rect(
                clipRect.xMax -
                ResizeHandleWidth,
                clipRect.y,
                ResizeHandleWidth,
                clipRect.height);

        EditorGUI.DrawRect(
            resizeHandle,
            new Color(
                1f,
                1f,
                1f,
                0.15f));

        EditorGUIUtility.AddCursorRect(
            clipRect,
            MouseCursor.MoveArrow);

        EditorGUIUtility.AddCursorRect(
            resizeHandle,
            MouseCursor.ResizeHorizontal);
    }


    // ============================================================
    // Input
    // ============================================================

    private void HandleClipInput(
        int index,
        Rect clipRect,
        Rect timelineRect,
        float sequenceDuration,
        SerializedProperty startProperty,
        SerializedProperty durationProperty)
    {
        Event current =
            Event.current;

        Rect resizeRect =
            new Rect(
                clipRect.xMax -
                ResizeHandleWidth,
                clipRect.y,
                ResizeHandleWidth,
                clipRect.height);

        // ----------------------------
        // Mouse Down
        // ----------------------------

        if (current.type ==
                EventType.MouseDown &&
            current.button == 0 &&
            clipRect.Contains(
                current.mousePosition))
        {
            _selectedClipIndex =
                index;

            _draggingClipIndex =
                index;

            if (resizeRect.Contains(
                    current.mousePosition))
            {
                _dragMode =
                    DragMode.ResizeRight;
            }
            else
            {
                _dragMode =
                    DragMode.Move;

                float mouseTime =
                    PixelToTime(
                        current.mousePosition.x,
                        timelineRect,
                        sequenceDuration);

                _dragOffsetTime =
                    mouseTime -
                    startProperty.floatValue;
            }

            Undo.RecordObject(
                _sequence,
                "Edit LED Effect Clip");

            current.Use();

            Repaint();

            return;
        }


        // ----------------------------
        // Mouse Drag
        // ----------------------------

        if (current.type !=
                EventType.MouseDrag ||
            current.button != 0 ||
            _draggingClipIndex != index)
        {
            return;
        }

        float currentTime =
            PixelToTime(
                current.mousePosition.x,
                timelineRect,
                sequenceDuration);

        switch (_dragMode)
        {
            case DragMode.Move:
            {
                float maxStart =
                    Mathf.Max(
                        0f,
                        sequenceDuration -
                        durationProperty.floatValue);

                float newStart =
                    Mathf.Clamp(
                        currentTime -
                        _dragOffsetTime,
                        0f,
                        maxStart);

                startProperty.floatValue =
                    newStart;

                break;
            }

            case DragMode.ResizeRight:
            {
                float maxDuration =
                    sequenceDuration -
                    startProperty.floatValue;

                float newDuration =
                    Mathf.Clamp(
                        currentTime -
                        startProperty.floatValue,
                        0.01f,
                        maxDuration);

                durationProperty.floatValue =
                    newDuration;

                break;
            }
        }

        _serializedSequence
            .ApplyModifiedProperties();

        current.Use();

        Repaint();
    }


    private void HandleMouseUp()
    {
        Event current =
            Event.current;

        if (current.type !=
            EventType.MouseUp ||
            current.button != 0)
        {
            return;
        }

        if (_dragMode ==
            DragMode.None)
        {
            return;
        }

        _dragMode =
            DragMode.None;

        _draggingClipIndex =
            -1;

        current.Use();

        Repaint();
    }


    // ============================================================
    // Coordinate conversion
    // ============================================================

    private static float TimeToPixel(
        float time,
        Rect rect,
        float duration)
    {
        float normalized =
            Mathf.Clamp01(
                time / duration);

        return rect.x +
               normalized *
               rect.width;
    }


    private static float PixelToTime(
        float pixelX,
        Rect rect,
        float duration)
    {
        float normalized =
            Mathf.InverseLerp(
                rect.x,
                rect.xMax,
                pixelX);

        return normalized *
               duration;
    }
}