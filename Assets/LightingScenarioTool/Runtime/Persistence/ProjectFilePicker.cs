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
        /// <summary>
        /// Non-empty only when the most recent native Windows dialog failed because of an OS error.
        /// Empty means either success or normal user cancellation.
        /// </summary>
        public static string LastErrorMessage { get; private set; }

        public static bool TryPickOpenProjectFile(string currentPath, out string selectedPath)
        {
            LastErrorMessage = null;
            var initialDirectory = GetInitialDirectory(currentPath);
#if UNITY_EDITOR
            var path = EditorUtility.OpenFilePanel("Open Lighting Scenario Project", initialDirectory, "json");
            return NormalizeSelection(path, out selectedPath);
#elif UNITY_STANDALONE_WIN
            return TryPickWindowsOpen(
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
            LastErrorMessage = null;
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
            LastErrorMessage = null;
            var initialDirectory = GetInitialDirectory(currentPath);
#if UNITY_EDITOR
            var path = EditorUtility.OpenFilePanelWithFilters(
                "Select Preview Background Image",
                initialDirectory,
                new[] { "Image files", "png,jpg,jpeg", "All files", "*" });
            return NormalizeSelection(path, out selectedPath);
#elif UNITY_STANDALONE_WIN
            return TryPickWindowsOpen(
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

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(documents) && Directory.Exists(documents))
                return documents;

            return Environment.CurrentDirectory;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // OPENFILENAMEW.  All string members are IntPtr intentionally.
        // This avoids runtime/IL2CPP marshalling of String/StringBuilder fields inside
        // a non-blittable structure.  Buffers are allocated and freed explicitly below.
        [StructLayout(LayoutKind.Sequential)]
        private struct OpenFileNameNative
        {
            public uint lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public IntPtr lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public uint nMaxCustFilter;
            public uint nFilterIndex;
            public IntPtr lpstrFile;
            public uint nMaxFile;
            public IntPtr lpstrFileTitle;
            public uint nMaxFileTitle;
            public IntPtr lpstrInitialDir;
            public IntPtr lpstrTitle;
            public uint Flags;
            public ushort nFileOffset;
            public ushort nFileExtension;
            public IntPtr lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
            public IntPtr pvReserved;
            public uint dwReserved;
            public uint FlagsEx;
        }

        private const uint OfnOverwritePrompt = 0x00000002;
        private const uint OfnNoChangeDir = 0x00000008;
        private const uint OfnPathMustExist = 0x00000800;
        private const uint OfnFileMustExist = 0x00001000;
        private const uint OfnExplorer = 0x00080000;

        private const int MaxFileChars = 32768;
        private const int MaxFileTitleChars = 1024;

        [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileNameW(ref OpenFileNameNative openFileName);

        [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSaveFileNameW(ref OpenFileNameNative openFileName);

        [DllImport("comdlg32.dll", ExactSpelling = true)]
        private static extern uint CommDlgExtendedError();

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetActiveWindow();

        private static bool TryPickWindowsOpen(
            string initialDirectory,
            string title,
            string filter,
            string defaultExtension,
            out string selectedPath)
        {
            return TryPickWindowsCore(
                save: false,
                initialDirectory: initialDirectory,
                defaultFileName: null,
                title: title,
                filter: filter,
                defaultExtension: defaultExtension,
                out selectedPath);
        }

        private static bool TryPickWindowsSave(
            string initialDirectory,
            string defaultFileName,
            string title,
            string filter,
            string defaultExtension,
            out string selectedPath)
        {
            return TryPickWindowsCore(
                save: true,
                initialDirectory: initialDirectory,
                defaultFileName: defaultFileName,
                title: title,
                filter: filter,
                defaultExtension: defaultExtension,
                out selectedPath);
        }

        private static bool TryPickWindowsCore(
            bool save,
            string initialDirectory,
            string defaultFileName,
            string title,
            string filter,
            string defaultExtension,
            out string selectedPath)
        {
            selectedPath = null;
            LastErrorMessage = null;

            IntPtr filterPtr = IntPtr.Zero;
            IntPtr filePtr = IntPtr.Zero;
            IntPtr fileTitlePtr = IntPtr.Zero;
            IntPtr initialDirPtr = IntPtr.Zero;
            IntPtr titlePtr = IntPtr.Zero;
            IntPtr defExtPtr = IntPtr.Zero;

            try
            {
                filterPtr = AllocUnicodeString(filter);
                filePtr = AllocUnicodeBuffer(MaxFileChars, defaultFileName);
                fileTitlePtr = AllocUnicodeBuffer(MaxFileTitleChars, null);
                initialDirPtr = AllocUnicodeString(initialDirectory);
                titlePtr = AllocUnicodeString(title);
                defExtPtr = AllocUnicodeString(defaultExtension);

                var owner = GetForegroundWindow();
                if (owner == IntPtr.Zero)
                    owner = GetActiveWindow();

                var dialog = new OpenFileNameNative
                {
                    lStructSize = (uint)Marshal.SizeOf(typeof(OpenFileNameNative)),
                    hwndOwner = owner,
                    hInstance = IntPtr.Zero,
                    lpstrFilter = filterPtr,
                    lpstrCustomFilter = IntPtr.Zero,
                    nMaxCustFilter = 0,
                    nFilterIndex = 1,
                    lpstrFile = filePtr,
                    nMaxFile = MaxFileChars,
                    lpstrFileTitle = fileTitlePtr,
                    nMaxFileTitle = MaxFileTitleChars,
                    lpstrInitialDir = initialDirPtr,
                    lpstrTitle = titlePtr,
                    Flags = OfnExplorer | OfnPathMustExist | OfnNoChangeDir |
                            (save ? OfnOverwritePrompt : OfnFileMustExist),
                    nFileOffset = 0,
                    nFileExtension = 0,
                    lpstrDefExt = defExtPtr,
                    lCustData = IntPtr.Zero,
                    lpfnHook = IntPtr.Zero,
                    lpTemplateName = IntPtr.Zero,
                    pvReserved = IntPtr.Zero,
                    dwReserved = 0,
                    FlagsEx = 0
                };

                var success = save
                    ? GetSaveFileNameW(ref dialog)
                    : GetOpenFileNameW(ref dialog);

                if (!success)
                {
                    SetCommonDialogError(save ? "Save As" : "Open");
                    return false;
                }

                var path = Marshal.PtrToStringUni(dialog.lpstrFile);
                if (string.IsNullOrWhiteSpace(path))
                {
                    LastErrorMessage = "The Windows file dialog returned an empty file path.";
                    return false;
                }

                selectedPath = Path.GetFullPath(path.Trim());
                if (save && string.IsNullOrEmpty(Path.GetExtension(selectedPath)))
                    selectedPath += ".json";

                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = "Windows file dialog failed: " + ex.Message;
                selectedPath = null;
                return false;
            }
            finally
            {
                FreeIfAllocated(filterPtr);
                FreeIfAllocated(filePtr);
                FreeIfAllocated(fileTitlePtr);
                FreeIfAllocated(initialDirPtr);
                FreeIfAllocated(titlePtr);
                FreeIfAllocated(defExtPtr);
            }
        }

        private static IntPtr AllocUnicodeString(string value)
        {
            if (string.IsNullOrEmpty(value)) return IntPtr.Zero;

            // Marshal.StringToHGlobalUni stops conceptually at a terminating NUL for
            // normal strings.  Filters intentionally contain embedded NULs, so copy
            // the raw UTF-16 bytes ourselves to preserve the complete filter list.
            var bytes = Encoding.Unicode.GetBytes(value + "\0");
            var ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }

        private static IntPtr AllocUnicodeBuffer(int charCapacity, string initialValue)
        {
            var byteCount = checked(charCapacity * 2);
            var ptr = Marshal.AllocHGlobal(byteCount);

            // Marshal.AllocHGlobal does not zero memory.  OPENFILENAME requires a
            // NUL-terminated writable buffer, so initialise the whole buffer.
            var zero = new byte[byteCount];
            Marshal.Copy(zero, 0, ptr, byteCount);

            if (!string.IsNullOrEmpty(initialValue))
            {
                var safeValue = initialValue.Length >= charCapacity
                    ? initialValue.Substring(0, charCapacity - 1)
                    : initialValue;
                var bytes = Encoding.Unicode.GetBytes(safeValue);
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
            }

            return ptr;
        }

        private static void SetCommonDialogError(string operation)
        {
            var code = CommDlgExtendedError();
            // 0 means the user cancelled/closed the dialog normally.
            if (code != 0)
                LastErrorMessage = operation + " file dialog failed (CommDlgExtendedError=0x" + code.ToString("X8") + ").";
        }

        private static void FreeIfAllocated(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
        }
#endif
    }
}
