using System;
using System.IO;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LightingScenarioTool
{
    internal static class ProjectFilePicker
    {
        public static bool TryPickOpenProjectFile(string currentPath, out string selectedPath)
        {
            var initialDirectory = GetInitialDirectory(currentPath);
#if UNITY_EDITOR
            var path = EditorUtility.OpenFilePanel("Open Lighting Scenario Project", initialDirectory, "json");
            return NormalizeSelection(path, out selectedPath);
#elif UNITY_STANDALONE_WIN
            return TryPickWindows(
                initialDirectory,
                "Open Lighting Scenario Project",
                "Lighting Scenario Project (*.json)\0*.json\0JSON files (*.json)\0*.json\0All files (*.*)\0*.*\0\0",
                "json",
                out selectedPath);
#else
            selectedPath = null;
            return false;
#endif
        }

        public static bool TryPickSaveProjectFile(string currentPath, out string selectedPath)
        {
            var initialDirectory = GetInitialDirectory(currentPath);
            var defaultName = GetInitialFileName(currentPath, "scenario.json");
#if UNITY_EDITOR
            var editorDefaultName = Path.GetFileNameWithoutExtension(defaultName);
            if (string.IsNullOrWhiteSpace(editorDefaultName)) editorDefaultName = "scenario";
            var path = EditorUtility.SaveFilePanel(
                "Save Lighting Scenario Project",
                initialDirectory,
                editorDefaultName,
                "json");
            return NormalizeSelection(path, out selectedPath);
#elif UNITY_STANDALONE_WIN
            return TryPickWindowsSave(
                initialDirectory,
                defaultName,
                "Save Lighting Scenario Project",
                "Lighting Scenario Project (*.json)\0*.json\0JSON files (*.json)\0*.json\0All files (*.*)\0*.*\0\0",
                "json",
                out selectedPath);
#else
            selectedPath = null;
            return false;
#endif
        }

        public static bool TryPickOpenImageFile(string currentPath, out string selectedPath)
        {
            var initialDirectory = GetInitialDirectory(currentPath);
#if UNITY_EDITOR
            var path = EditorUtility.OpenFilePanelWithFilters(
                "Select Preview Background Image",
                initialDirectory,
                new[] { "Image files", "png,jpg,jpeg", "All files", "*" });
            return NormalizeSelection(path, out selectedPath);
#elif UNITY_STANDALONE_WIN
            return TryPickWindows(
                initialDirectory,
                "Select Preview Background Image",
                "Image files (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0PNG (*.png)\0*.png\0JPEG (*.jpg;*.jpeg)\0*.jpg;*.jpeg\0All files (*.*)\0*.*\0\0",
                null,
                out selectedPath);
#else
            selectedPath = null;
            return false;
#endif
        }

        private static bool NormalizeSelection(string path, out string selectedPath)
        {
            selectedPath = null;
            if (string.IsNullOrEmpty(path)) return false;
            selectedPath = Path.GetFullPath(path);
            return true;
        }

        private static string GetInitialFileName(string currentPath, string fallback)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(currentPath))
                {
                    var fileName = Path.GetFileName(currentPath.Trim());
                    if (!string.IsNullOrWhiteSpace(fileName)) return fileName;
                }
            }
            catch
            {
                // Use the fallback name.
            }
            return fallback;
        }

        private static string GetInitialDirectory(string currentPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(currentPath))
                {
                    var full = Path.GetFullPath(currentPath);
                    if (Directory.Exists(full)) return full;
                    var directory = Path.GetDirectoryName(full);
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) return directory;
                }
            }
            catch
            {
                // Fall through to the default directory.
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int OfnPathMustExist = 0x00000800;
        private const int OfnFileMustExist = 0x00001000;
        private const int OfnExplorer = 0x00080000;
        private const int OfnNoChangeDir = 0x00000008;
        private const int OfnOverwritePrompt = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class OpenFileName
        {
            public int structSize = 0;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public string filter = null;
            public string customFilter = null;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public StringBuilder file = null;
            public int maxFile = 0;
            public StringBuilder fileTitle = null;
            public int maxFileTitle = 0;
            public string initialDir = null;
            public string title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public string defExt = null;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string templateName = null;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileName([In, Out] OpenFileName openFileName);

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSaveFileName([In, Out] OpenFileName openFileName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        private static bool TryPickWindows(
            string initialDirectory,
            string title,
            string filter,
            string defaultExtension,
            out string selectedPath)
        {
            selectedPath = null;
            var buffer = new StringBuilder(4096);
            var dialog = new OpenFileName
            {
                structSize = Marshal.SizeOf(typeof(OpenFileName)),
                dlgOwner = GetActiveWindow(),
                filter = filter,
                filterIndex = 1,
                file = buffer,
                maxFile = buffer.Capacity,
                initialDir = initialDirectory,
                title = title,
                defExt = defaultExtension,
                flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir
            };

            if (!GetOpenFileName(dialog)) return false;
            if (dialog.file == null || dialog.file.Length == 0) return false;
            selectedPath = dialog.file.ToString();
            return true;
        }

        private static bool TryPickWindowsSave(
            string initialDirectory,
            string defaultFileName,
            string title,
            string filter,
            string defaultExtension,
            out string selectedPath)
        {
            selectedPath = null;
            var buffer = new StringBuilder(4096);
            if (!string.IsNullOrWhiteSpace(defaultFileName))
                buffer.Append(defaultFileName);

            var dialog = new OpenFileName
            {
                structSize = Marshal.SizeOf(typeof(OpenFileName)),
                dlgOwner = GetActiveWindow(),
                filter = filter,
                filterIndex = 1,
                file = buffer,
                maxFile = buffer.Capacity,
                initialDir = initialDirectory,
                title = title,
                defExt = defaultExtension,
                flags = OfnExplorer | OfnPathMustExist | OfnNoChangeDir | OfnOverwritePrompt
            };

            if (!GetSaveFileName(dialog)) return false;
            if (dialog.file == null || dialog.file.Length == 0) return false;
            selectedPath = Path.GetFullPath(dialog.file.ToString());
            if (string.IsNullOrEmpty(Path.GetExtension(selectedPath)))
                selectedPath += ".json";
            return true;
        }
#endif
    }
}
