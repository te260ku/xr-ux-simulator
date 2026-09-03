using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace RuntimeCsvEditor
{
    /// <summary>
    /// Runtime CSV editor whose UI is entirely prefab based.
    /// No uGUI hierarchy/components are created with AddComponent at runtime.
    /// Only row/cell prefab instances are created to match the loaded CSV.
    /// </summary>
    public sealed class CsvRuntimeEditorUGUI : MonoBehaviour
    {
        [Header("File")]
        [Tooltip("Absolute path, or a path relative to the Unity project root in Editor / executable folder in a standalone build.")]
        [SerializeField] private string csvPath = "Config/actuation_patterns.csv";
        [SerializeField] private bool loadOnStart = true;

        [Header("Display")]
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F10;
        [SerializeField] private float columnWidth = 190f;
        [SerializeField] private float rowHeight = 38f;
        [SerializeField] private float rowNumberWidth = 54f;

        [Header("Prefab UI")]
        [SerializeField] private CsvRuntimeEditorView view;
        [SerializeField] private CsvTableRowView rowPrefab;
        [SerializeField] private CsvHeaderCellView headerCellPrefab;
        [SerializeField] private CsvTextCellView textCellPrefab;
        [SerializeField] private CsvEnumCellView enumCellPrefab;

        [Header("Enum Columns")]
        [Tooltip("If enabled, a CSV column whose header matches an enum type name is rendered as a Dropdown.")]
        [SerializeField] private bool autoDetectEnumColumns = true;
        [Tooltip("Optional per-column override. enumTypeName can be a full type name (Namespace.MyEnum) or a unique simple enum type name.")]
        [SerializeField] private List<EnumColumnOverride> enumColumnOverrides = new();

        private CsvTableData table = new();
        private int selectedRow = -1;
        private readonly List<CsvTableRowView> generatedRows = new();
        private readonly Dictionary<int, EnumColumnInfo> enumColumns = new();

        [Serializable]
        private sealed class EnumColumnOverride
        {
            public string columnName;
            public string enumTypeName;
        }

        private sealed class EnumColumnInfo
        {
            public List<string> Values;
        }

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            BindViewEvents();
            view.PathInput.text = csvPath;
            SetVisible(showOnStart);

            if (loadOnStart)
                LoadCsv();
        }

        private void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
                Toggle();
        }

        private void OnDestroy()
        {
            UnbindViewEvents();
        }

        public void Show() => SetVisible(true);
        public void Hide() => SetVisible(false);
        public void Toggle() => SetVisible(!view.PanelRoot.activeSelf);

        public void LoadCsv()
        {
            try
            {
                string path = view.PathInput != null ? view.PathInput.text : csvPath;
                string resolvedPath = ResolvePath(path);

                table = CsvTableData.Load(resolvedPath);
                selectedRow = -1;
                csvPath = path;

                RebuildEnumColumnCache();
                RebuildTable();
                view.TableScrollRect.normalizedPosition = new Vector2(0f, 1f);
                SetStatus($"Loaded: {resolvedPath}   Enum columns: {enumColumns.Count}");
            }
            catch (Exception ex)
            {
                SetStatus($"Load failed: {ex.Message}");
                Debug.LogException(ex, this);
            }
        }

        public void SaveCsv()
        {
            try
            {
                if (table.ColumnCount <= 0)
                    throw new InvalidOperationException("保存するCSVが読み込まれていません。");

                string path = view.PathInput != null ? view.PathInput.text : csvPath;
                string resolvedPath = ResolvePath(path);
                table.Save(resolvedPath);
                csvPath = path;
                SetStatus($"Saved: {resolvedPath}");
            }
            catch (Exception ex)
            {
                SetStatus($"Save failed: {ex.Message}");
                Debug.LogException(ex, this);
            }
        }

        public void AddRow()
        {
            if (table.ColumnCount <= 0)
            {
                SetStatus("ヘッダーのあるCSVを先に読み込んでください。");
                return;
            }

            table.AddEmptyRow();
            selectedRow = table.Rows.Count - 1;
            RebuildTable();
            SetStatus($"行を追加しました。Row {selectedRow + 1}");
        }

        public void DeleteSelectedRow()
        {
            if (selectedRow < 0 || selectedRow >= table.Rows.Count)
            {
                SetStatus("削除する行を左端の行番号から選択してください。");
                return;
            }

            table.RemoveRowAt(selectedRow);
            selectedRow = Mathf.Min(selectedRow, table.Rows.Count - 1);
            RebuildTable();
            SetStatus("選択行を削除しました。");
        }

        private bool ValidateReferences()
        {
            bool valid = view != null &&
                         rowPrefab != null &&
                         headerCellPrefab != null &&
                         textCellPrefab != null &&
                         enumCellPrefab != null;

            if (!valid)
                Debug.LogError("CsvRuntimeEditorUGUI: Prefab UI references are missing. Use the provided CsvRuntimeEditor prefab or assign all references in the Inspector.", this);

            return valid;
        }

        private void BindViewEvents()
        {
            view.LoadButton.onClick.AddListener(LoadCsv);
            view.SaveButton.onClick.AddListener(SaveCsv);
            view.AddRowButton.onClick.AddListener(AddRow);
            view.DeleteRowButton.onClick.AddListener(DeleteSelectedRow);
            view.CloseButton.onClick.AddListener(Hide);
        }

        private void UnbindViewEvents()
        {
            if (view == null)
                return;

            view.LoadButton.onClick.RemoveListener(LoadCsv);
            view.SaveButton.onClick.RemoveListener(SaveCsv);
            view.AddRowButton.onClick.RemoveListener(AddRow);
            view.DeleteRowButton.onClick.RemoveListener(DeleteSelectedRow);
            view.CloseButton.onClick.RemoveListener(Hide);
        }

        private void SetVisible(bool visible)
        {
            if (view?.PanelRoot != null)
                view.PanelRoot.SetActive(visible);
        }

        private void RebuildTable()
        {
            ClearGeneratedRows();

            if (table.ColumnCount <= 0)
            {
                ResizeTableContent(rowNumberWidth + columnWidth, rowHeight);
                UpdateSummary();
                return;
            }

            float totalWidth = rowNumberWidth + table.ColumnCount * columnWidth;
            int visualRowCount = 1 + table.Rows.Count;
            float totalHeight = visualRowCount * rowHeight;
            ResizeTableContent(totalWidth, totalHeight);

            CreateHeaderRow(totalWidth);
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                CreateDataRow(rowIndex, totalWidth);

            UpdateSummary();
        }

        private void CreateHeaderRow(float totalWidth)
        {
            var rowView = Instantiate(rowPrefab, view.TableContent);
            rowView.name = "Header";
            rowView.SetupHeader("#", totalWidth, rowHeight, rowNumberWidth);
            PositionRow(rowView.RectTransform, 0, totalWidth);
            generatedRows.Add(rowView);

            for (int columnIndex = 0; columnIndex < table.ColumnCount; columnIndex++)
            {
                string header = string.IsNullOrWhiteSpace(table.Headers[columnIndex])
                    ? "(Unnamed)"
                    : table.Headers[columnIndex];

                if (enumColumns.ContainsKey(columnIndex))
                    header += "  ▼";

                var cell = Instantiate(headerCellPrefab, rowView.CellsRoot);
                cell.SetValue(header);
                cell.SetWidth(columnWidth);
                PositionCell(cell.RectTransform, columnIndex);
            }
        }

        private void CreateDataRow(int rowIndex, float totalWidth)
        {
            var row = table.Rows[rowIndex];
            while (row.Count < table.ColumnCount)
                row.Add(string.Empty);

            var rowView = Instantiate(rowPrefab, view.TableContent);
            rowView.name = $"Row_{rowIndex}";
            rowView.SetupDataRow((rowIndex + 1).ToString(), totalWidth, rowHeight, rowNumberWidth, rowIndex == selectedRow);
            PositionRow(rowView.RectTransform, rowIndex + 1, totalWidth);
            generatedRows.Add(rowView);

            int capturedRow = rowIndex;
            rowView.RowSelectButton.onClick.AddListener(() => SelectRow(capturedRow));

            for (int columnIndex = 0; columnIndex < table.ColumnCount; columnIndex++)
            {
                int capturedColumn = columnIndex;

                if (enumColumns.TryGetValue(capturedColumn, out var enumColumn))
                {
                    var cell = Instantiate(enumCellPrefab, rowView.CellsRoot);
                    cell.SetWidth(columnWidth);
                    PositionCell(cell.RectTransform, columnIndex);
                    cell.SetOptions(row[capturedColumn] ?? string.Empty, enumColumn.Values, value =>
                    {
                        if (capturedRow < table.Rows.Count && capturedColumn < table.Rows[capturedRow].Count)
                            table.Rows[capturedRow][capturedColumn] = value;
                    });
                }
                else
                {
                    var cell = Instantiate(textCellPrefab, rowView.CellsRoot);
                    cell.SetWidth(columnWidth);
                    PositionCell(cell.RectTransform, columnIndex);
                    cell.SetValue(row[capturedColumn] ?? string.Empty, value =>
                    {
                        if (capturedRow < table.Rows.Count && capturedColumn < table.Rows[capturedRow].Count)
                            table.Rows[capturedRow][capturedColumn] = value;
                    });
                }
            }
        }

        private void SelectRow(int rowIndex)
        {
            selectedRow = rowIndex;
            RebuildTable();
        }

        private void PositionRow(RectTransform rect, int visualRowIndex, float width)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, rowHeight);
            rect.anchoredPosition = new Vector2(0f, -visualRowIndex * rowHeight);
        }

        private void PositionCell(RectTransform rect, int columnIndex)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(columnIndex * columnWidth, 0f);
            rect.sizeDelta = new Vector2(columnWidth, 0f);
        }

        private void RebuildEnumColumnCache()
        {
            enumColumns.Clear();

            for (int columnIndex = 0; columnIndex < table.Headers.Count; columnIndex++)
            {
                string header = table.Headers[columnIndex]?.Trim();
                if (string.IsNullOrEmpty(header))
                    continue;

                string requestedTypeName = null;
                for (int i = 0; i < enumColumnOverrides.Count; i++)
                {
                    var binding = enumColumnOverrides[i];
                    if (binding == null)
                        continue;

                    if (string.Equals(binding.columnName?.Trim(), header, StringComparison.Ordinal))
                    {
                        requestedTypeName = binding.enumTypeName?.Trim();
                        break;
                    }
                }

                Type enumType = null;
                if (!string.IsNullOrEmpty(requestedTypeName))
                {
                    enumType = FindEnumType(requestedTypeName, out string error);
                    if (enumType == null)
                        Debug.LogWarning($"CSV enum column '{header}' could not resolve enum type '{requestedTypeName}': {error}", this);
                }
                else if (autoDetectEnumColumns)
                {
                    enumType = FindEnumType(header, out string error);
                    if (enumType == null && !string.IsNullOrEmpty(error))
                        Debug.LogWarning($"CSV enum auto-detection skipped column '{header}': {error}", this);
                }

                if (enumType == null)
                    continue;

                enumColumns[columnIndex] = new EnumColumnInfo
                {
                    Values = new List<string>(Enum.GetNames(enumType))
                };
            }
        }

        private static Type FindEnumType(string typeName, out string error)
        {
            error = null;
            var matches = new List<Type>();
            bool fullNameRequested = typeName.Contains(".");

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                catch
                {
                    continue;
                }

                if (types == null)
                    continue;

                foreach (var type in types)
                {
                    if (type == null || !type.IsEnum)
                        continue;

                    bool matchesName = fullNameRequested
                        ? string.Equals(type.FullName, typeName, StringComparison.Ordinal)
                        : string.Equals(type.Name, typeName, StringComparison.Ordinal);

                    if (matchesName)
                        matches.Add(type);
                }
            }

            if (matches.Count == 1)
                return matches[0];

            if (matches.Count == 0)
            {
                error = fullNameRequested
                    ? "No enum with that full type name is loaded."
                    : null;
                return null;
            }

            error = $"Multiple enum types named '{typeName}' are loaded. Use Enum Column Overrides and specify the full type name.";
            return null;
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("CSV path is empty.");

            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            string baseDirectory = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(baseDirectory, path));
        }

        private void ClearGeneratedRows()
        {
            foreach (var row in generatedRows)
            {
                if (row != null)
                    Destroy(row.gameObject);
            }

            generatedRows.Clear();
        }

        private void ResizeTableContent(float width, float height)
        {
            view.TableContent.sizeDelta = new Vector2(width, height);
        }

        private void UpdateSummary()
        {
            if (view.SummaryText != null)
                view.SummaryText.text = $"Rows: {table.Rows.Count}   Columns: {table.ColumnCount}";
        }

        private void SetStatus(string message)
        {
            if (view.StatusText != null)
                view.StatusText.text = message;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CsvRuntimeEditorView configuredView,
            CsvTableRowView configuredRowPrefab,
            CsvHeaderCellView configuredHeaderCellPrefab,
            CsvTextCellView configuredTextCellPrefab,
            CsvEnumCellView configuredEnumCellPrefab)
        {
            view = configuredView;
            rowPrefab = configuredRowPrefab;
            headerCellPrefab = configuredHeaderCellPrefab;
            textCellPrefab = configuredTextCellPrefab;
            enumCellPrefab = configuredEnumCellPrefab;
        }
#endif
    }
}
