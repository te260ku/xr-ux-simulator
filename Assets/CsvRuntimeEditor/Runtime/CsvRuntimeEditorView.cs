using UnityEngine;
using UnityEngine.UI;

namespace RuntimeCsvEditor
{
    public sealed class CsvRuntimeEditorView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private InputField pathInput;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button addRowButton;
        [SerializeField] private Button deleteRowButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private ScrollRect tableScrollRect;
        [SerializeField] private RectTransform tableContent;
        [SerializeField] private Text statusText;
        [SerializeField] private Text summaryText;

        public GameObject PanelRoot => panelRoot;
        public InputField PathInput => pathInput;
        public Button LoadButton => loadButton;
        public Button SaveButton => saveButton;
        public Button AddRowButton => addRowButton;
        public Button DeleteRowButton => deleteRowButton;
        public Button CloseButton => closeButton;
        public ScrollRect TableScrollRect => tableScrollRect;
        public RectTransform TableContent => tableContent;
        public Text StatusText => statusText;
        public Text SummaryText => summaryText;

#if UNITY_EDITOR
        public void EditorConfigure(
            GameObject configuredPanelRoot,
            InputField configuredPathInput,
            Button configuredLoadButton,
            Button configuredSaveButton,
            Button configuredAddRowButton,
            Button configuredDeleteRowButton,
            Button configuredCloseButton,
            ScrollRect configuredTableScrollRect,
            RectTransform configuredTableContent,
            Text configuredStatusText,
            Text configuredSummaryText)
        {
            panelRoot = configuredPanelRoot;
            pathInput = configuredPathInput;
            loadButton = configuredLoadButton;
            saveButton = configuredSaveButton;
            addRowButton = configuredAddRowButton;
            deleteRowButton = configuredDeleteRowButton;
            closeButton = configuredCloseButton;
            tableScrollRect = configuredTableScrollRect;
            tableContent = configuredTableContent;
            statusText = configuredStatusText;
            summaryText = configuredSummaryText;
        }
#endif
    }
}
