#if UNITY_EDITOR

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.UI;
using TMPro;

public sealed class FigmaDataDownloaderWindow : EditorWindow
{
    private const string ApiBaseUrl = "https://api.figma.com/v1";
    private const string OutputRoot = "Assets/FigmaData";

    private string _token;
private string _fileKey;
private bool _isDownloading;
private readonly List<PageInfo> _pages = new();
private readonly List<FrameInfo> _frames = new();

private int _selectedPageIndex;
private int _selectedFrameIndex;
private bool _isDocumentLoaded;
private FrameAnalysis _frameAnalysis;
private bool _showNodeTypeBreakdown = true;
private UiImportDocument _intermediateDocument;
private string _intermediateOutputPath;
private RectTransform _generationParent;

    [MenuItem("Tools/Figma/Data Downloader")]
    private static void Open()
    {
        GetWindow<FigmaDataDownloaderWindow>("Figma Downloader");
    }

    private void OnGUI()
{
    EditorGUILayout.LabelField(
        "Figma Data Downloader",
        EditorStyles.boldLabel);

    EditorGUILayout.Space();

    _token = EditorGUILayout.PasswordField(
        "Personal Access Token",
        _token);

    _fileKey = EditorGUILayout.TextField(
        "File Key",
        _fileKey);

    EditorGUILayout.Space();

    EditorGUI.BeginDisabledGroup(_isDownloading);

    if (GUILayout.Button("Download Document JSON"))
    {
        DownloadDocumentJson();
    }

    if (GUILayout.Button("Download Images From Cached JSON"))
{
    DownloadImagesFromCache();
}


    EditorGUI.EndDisabledGroup();

    if (_isDownloading)
    {
        EditorGUILayout.HelpBox(
            "Downloading Figma data...",
            MessageType.Info);
    }

    EditorGUILayout.Space();
EditorGUILayout.LabelField(
    "Cached Figma Document",
    EditorStyles.boldLabel);

EditorGUI.BeginDisabledGroup(
    _isDownloading ||
    string.IsNullOrWhiteSpace(_fileKey));

if (GUILayout.Button("Load Cached Document"))
{
    LoadCachedDocument();
}

EditorGUI.EndDisabledGroup();

if (_isDocumentLoaded)
{
    DrawDocumentSelection();
}
}



    private void LoadCachedDocument()
{
    try
    {
        ValidateFileKey();

        string fileKey = _fileKey.Trim();
        string documentPath =
            $"{OutputRoot}/{fileKey}/document.json";

        if (!File.Exists(documentPath))
        {
            throw new FileNotFoundException(
                "document.json was not found.",
                documentPath);
        }

        string json = File.ReadAllText(
            documentPath,
            Encoding.UTF8);

        JObject root = JObject.Parse(json);

        JObject document = root["document"] as JObject;

        if (document == null)
        {
            throw new InvalidOperationException(
                "The document node was not found.");
        }

        JArray pageNodes = document["children"] as JArray;

        if (pageNodes == null)
        {
            throw new InvalidOperationException(
                "The document does not contain any pages.");
        }

        _pages.Clear();
        _frames.Clear();

        foreach (JToken pageToken in pageNodes)
        {
            if (pageToken is not JObject pageNode)
            {
                continue;
            }

            string nodeType =
                pageNode.Value<string>("type");

            if (nodeType != "CANVAS")
            {
                continue;
            }

            string pageName =
                pageNode.Value<string>("name")
                ?? "(Unnamed Page)";

            _pages.Add(
                new PageInfo(
                    pageName,
                    pageNode));
        }

        if (_pages.Count == 0)
        {
            throw new InvalidOperationException(
                "No Figma pages were found.");
        }

        _isDocumentLoaded = true;

        SelectPage(0);

        Debug.Log(
            $"Loaded Figma document: {_pages.Count} pages.");
    }
    catch (Exception exception)
    {
        _isDocumentLoaded = false;
        _pages.Clear();
        _frames.Clear();

        Debug.LogException(exception);
    }

    Repaint();
}



private void DrawDocumentSelection()
{
    if (_pages.Count == 0)
    {
        EditorGUILayout.HelpBox(
            "No pages were found.",
            MessageType.Warning);

        return;
    }

    string[] pageNames = new string[_pages.Count];

    for (int i = 0; i < _pages.Count; i++)
    {
        pageNames[i] = _pages[i].Name;
    }

    int newPageIndex = EditorGUILayout.Popup(
        "Page",
        _selectedPageIndex,
        pageNames);

    if (newPageIndex != _selectedPageIndex)
    {
        SelectPage(newPageIndex);
    }

    EditorGUILayout.LabelField(
        "Frames",
        _frames.Count.ToString());

    if (_frames.Count == 0)
    {
        EditorGUILayout.HelpBox(
            "No importable frames were found on this page.",
            MessageType.Info);

        return;
    }

    string[] frameNames = new string[_frames.Count];

    for (int i = 0; i < _frames.Count; i++)
    {
        FrameInfo frame = _frames[i];

        frameNames[i] =
            $"{frame.Path}  ({frame.Width:0.#} × {frame.Height:0.#})";
    }

    int newFrameIndex = EditorGUILayout.Popup(
    "Frame",
    _selectedFrameIndex,
    frameNames);

if (newFrameIndex != _selectedFrameIndex)
{
    _selectedFrameIndex = newFrameIndex;
    _frameAnalysis = null;
    _intermediateDocument = null;
    _intermediateOutputPath = null;
}

    FrameInfo selectedFrame =
        _frames[_selectedFrameIndex];

    EditorGUILayout.Space();

    using (new EditorGUI.DisabledScope(true))
    {
        EditorGUILayout.TextField(
            "Node ID",
            selectedFrame.Id);

        EditorGUILayout.TextField(
            "Frame Name",
            selectedFrame.Name);

        EditorGUILayout.TextField(
            "Size",
            $"{selectedFrame.Width:0.##} × " +
            $"{selectedFrame.Height:0.##}");
    }

    EditorGUILayout.Space();

using (new EditorGUI.DisabledScope(_isDownloading))
{
    if (GUILayout.Button(
            "Download Images in Selected Frame"))
    {
        DownloadSelectedFrameImages();
    }
}

    EditorGUILayout.Space();

    if (GUILayout.Button("Analyze Selected Frame"))
    {
        AnalyzeSelectedFrame();
    }

    if (_frameAnalysis != null)
    {
        DrawFrameAnalysis(_frameAnalysis);
    }

    EditorGUILayout.Space();

    if (GUILayout.Button("Build Intermediate Model"))
    {
        BuildIntermediateModel();
    }

    if (_intermediateDocument != null)
    {
        EditorGUILayout.HelpBox(
            $"Intermediate model generated.\n" +
            $"Root: {_intermediateDocument.RootNodeName}\n" +
            $"Nodes: {CountNodes(_intermediateDocument.Root)}\n" +
            $"Output: {_intermediateOutputPath}",
            MessageType.Info);
    }


    EditorGUILayout.Space();

    EditorGUILayout.LabelField(
        "RectTransform Generation",
        EditorStyles.boldLabel);

    _generationParent =
        (RectTransform)EditorGUILayout.ObjectField(
            "Generation Parent",
            _generationParent,
            typeof(RectTransform),
            true);

    using (new EditorGUI.DisabledScope(
            _intermediateDocument == null ||
            _generationParent == null))
    {
        if (GUILayout.Button(
                "Generate RectTransform Hierarchy"))
        {
            GenerateRectTransformHierarchy();
        }
    }
}




    private static void CollectImageRefs(
    JObject node,
    ISet<string> imageRefs)
{
    // 非表示ノード以下はUnityでも生成しないため除外する。
    if (node.Value<bool?>("visible") == false)
    {
        return;
    }

    JArray fills =
        node["fills"] as JArray;

    if (fills != null)
    {
        foreach (JToken fillToken in fills)
        {
            if (fillToken is not JObject fill)
            {
                continue;
            }

            if (fill.Value<bool?>("visible") == false)
            {
                continue;
            }

            if (fill.Value<string>("type") != "IMAGE")
            {
                continue;
            }

            string imageRef =
                fill.Value<string>("imageRef");

            if (!string.IsNullOrWhiteSpace(imageRef))
            {
                imageRefs.Add(imageRef);
            }
        }
    }

    JArray children =
        node["children"] as JArray;

    if (children == null)
    {
        return;
    }

    foreach (JToken childToken in children)
    {
        if (childToken is JObject childNode)
        {
            CollectImageRefs(
                childNode,
                imageRefs);
        }
    }
}




    private async void DownloadSelectedFrameImages()
{
    if (_isDownloading)
    {
        return;
    }

    _isDownloading = true;
    Repaint();

    try
    {
        ValidateInput();

        if (_frames.Count == 0)
        {
            throw new InvalidOperationException(
                "No frame is selected.");
        }

        FrameInfo selectedFrame =
            _frames[_selectedFrameIndex];

        var requiredImageRefs =
            new HashSet<string>(
                StringComparer.Ordinal);

        CollectImageRefs(
            selectedFrame.Node,
            requiredImageRefs);

        string fileKey = _fileKey.Trim();
        string outputDirectory =
            $"{OutputRoot}/{fileKey}";

        string imageDirectory =
            $"{outputDirectory}/Images";

        // 前回選択したFrameの画像を残さない。
        ResetImageDirectory(imageDirectory);

        if (requiredImageRefs.Count == 0)
        {
            WriteText(
                $"{outputDirectory}/image-map.json",
                "{}");

            WriteText(
                $"{outputDirectory}/selected-image-urls.json",
                "{}");

            AssetDatabase.Refresh();

            Debug.Log(
                $"The selected frame contains no image fills.\n" +
                $"Frame: {selectedFrame.Name}");

            return;
        }

        // このAPIは全imageRefのURLを返すが、
        // 実際にダウンロードするのは選択Frame内の画像だけ。
        string imageFillJson =
            await DownloadApiText(
                $"{ApiBaseUrl}/files/{fileKey}/images",
                _token.Trim());

        JObject response =
            JObject.Parse(imageFillJson);

        JObject allImages =
            response["images"] as JObject
            ?? response["meta"]?["images"] as JObject;

        if (allImages == null)
        {
            throw new InvalidOperationException(
                "The image URL map was not found " +
                "in the Figma API response.");
        }

        var localImageMap =
            new JObject();

        var selectedImageUrls =
            new JObject();

        int current = 0;
        int total = requiredImageRefs.Count;

        foreach (string imageRef in requiredImageRefs)
        {
            current++;

            JToken urlToken =
                allImages[imageRef];

            string imageUrl =
                urlToken?.Type == JTokenType.String
                    ? urlToken.Value<string>()
                    : null;

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                Debug.LogWarning(
                    $"Image URL was not returned.\n" +
                    $"ImageRef: {imageRef}");

                continue;
            }

            selectedImageUrls[imageRef] =
                imageUrl;

            EditorUtility.DisplayProgressBar(
                "Downloading Selected Frame Images",
                $"{current} / {total}",
                (float)current / total);

            DownloadedFile downloadedFile =
                await DownloadBinary(imageUrl);

            string extension =
                DetermineExtension(
                    downloadedFile.ContentType,
                    imageUrl);

            string fileName =
                MakeSafeFileName(imageRef) +
                extension;

            string imagePath =
                $"{imageDirectory}/{fileName}";

            File.WriteAllBytes(
                imagePath,
                downloadedFile.Data);

            localImageMap[imageRef] =
                $"Images/{fileName}";
        }

        WriteText(
            $"{outputDirectory}/image-map.json",
            localImageMap.ToString(
                Formatting.Indented));

        WriteText(
            $"{outputDirectory}/selected-image-urls.json",
            selectedImageUrls.ToString(
                Formatting.Indented));

        AssetDatabase.Refresh();

        Debug.Log(
            $"Selected frame images downloaded.\n" +
            $"Frame: {selectedFrame.Name}\n" +
            $"Referenced images: {requiredImageRefs.Count}\n" +
            $"Downloaded images: {localImageMap.Count}");
    }
    catch (Exception exception)
    {
        Debug.LogException(exception);
    }
    finally
    {
        EditorUtility.ClearProgressBar();

        _isDownloading = false;
        Repaint();
    }
}




    private static void ResetImageDirectory(
    string imageDirectory)
{
    if (AssetDatabase.IsValidFolder(imageDirectory))
    {
        AssetDatabase.DeleteAsset(imageDirectory);
    }
    else if (Directory.Exists(imageDirectory))
    {
        Directory.Delete(
            imageDirectory,
            recursive: true);
    }

    Directory.CreateDirectory(imageDirectory);
}




    private void GenerateRectTransformHierarchy()
{
    try
    {
        if (_intermediateDocument?.Root == null)
        {
            throw new InvalidOperationException(
                "Build the intermediate model first.");
        }

        if (_generationParent == null)
        {
            throw new InvalidOperationException(
                "Generation Parent is not assigned.");
        }

        if (EditorUtility.IsPersistent(_generationParent))
        {
            throw new InvalidOperationException(
                "Generation Parent must be a scene object.");
        }

        string fileKey =
            _intermediateDocument.FileKey;

        Dictionary<string, string> imageMap =
            LoadImageMap(fileKey);

        Undo.IncrementCurrentGroup();

        int undoGroup =
            Undo.GetCurrentGroup();

        Undo.SetCurrentGroupName(
            "Generate Figma UI");

        RectTransform generatedRoot =
            CreateRectTransformNode(
                _intermediateDocument.Root,
                _generationParent,
                isRoot: true,
                fileKey,
                imageMap);

        Undo.CollapseUndoOperations(undoGroup);

        Selection.activeGameObject =
            generatedRoot.gameObject;

        EditorGUIUtility.PingObject(
            generatedRoot.gameObject);

        Debug.Log(
            $"Figma hierarchy generated.\n" +
            $"Root: {generatedRoot.name}\n" +
            $"Nodes: {CountNodes(_intermediateDocument.Root)}");
    }
    catch (Exception exception)
    {
        Debug.LogException(exception);
    }
}




    private static RectTransform CreateRectTransformNode(
    UiNodeData node,
    RectTransform parent,
    bool isRoot,
    string fileKey,
    IReadOnlyDictionary<string, string> imageMap)
{
    string objectName =
        string.IsNullOrWhiteSpace(node.Name)
            ? $"({node.SourceType})"
            : node.Name;

    var gameObject = new GameObject(
        objectName,
        typeof(RectTransform));

    Undo.RegisterCreatedObjectUndo(
        gameObject,
        "Create Figma UI Node");

    var rectTransform =
        gameObject.GetComponent<RectTransform>();

    Undo.SetTransformParent(
        rectTransform,
        parent,
        "Parent Figma UI Node");

    ApplyRectTransform(
        rectTransform,
        node,
        isRoot);

    ApplySolidFill(
        gameObject,
        node);

    ApplyImageFill(
        gameObject,
        node,
        fileKey,
        imageMap);

    ApplyText(
        gameObject,
        node);

    ApplyContentClip(
        gameObject,
        node);

    ApplyNodeOpacity(
        gameObject,
        node);

    foreach (UiNodeData child in node.Children)
    {
        CreateRectTransformNode(
            child,
            rectTransform,
            isRoot: false,
            fileKey,
            imageMap);
    }

    // 子要素より前面にStrokeを描画する。
    CreateStrokeOverlay(
        node,
        parent,
        isRoot);

    return rectTransform;
}




    private static void CreateStrokeOverlay(
    UiNodeData node,
    RectTransform parent,
    bool isRoot)
{
    if (!node.HasStroke)
    {
        return;
    }

    if (!SupportsRectangularStroke(node.SourceType))
    {
        return;
    }

    var strokeObject = new GameObject(
        $"__Stroke_{node.Name}",
        typeof(RectTransform));

    Undo.RegisterCreatedObjectUndo(
        strokeObject,
        "Create Figma Stroke");

    RectTransform strokeRect =
        strokeObject.GetComponent<RectTransform>();

    Undo.SetTransformParent(
        strokeRect,
        parent,
        "Parent Figma Stroke");

    // 元ノードと同じ位置、サイズ、回転にする。
    ApplyRectTransform(
        strokeRect,
        node,
        isRoot);

    ApplyStrokeOpacity(
        strokeObject,
        node);

    Color color =
        new Color(
            Mathf.Clamp01(node.StrokeColor.R),
            Mathf.Clamp01(node.StrokeColor.G),
            Mathf.Clamp01(node.StrokeColor.B),
            Mathf.Clamp01(node.StrokeColor.A));

    float topOutward =
        CalculateOutwardAmount(
            node.StrokeTop,
            node.StrokeAlign);

    float rightOutward =
        CalculateOutwardAmount(
            node.StrokeRight,
            node.StrokeAlign);

    float bottomOutward =
        CalculateOutwardAmount(
            node.StrokeBottom,
            node.StrokeAlign);

    float leftOutward =
        CalculateOutwardAmount(
            node.StrokeLeft,
            node.StrokeAlign);

    float horizontalWidth =
        node.Width +
        leftOutward +
        rightOutward;

    float verticalHeight =
        node.Height +
        topOutward +
        bottomOutward;

    if (node.StrokeTop > 0f)
    {
        CreateStrokeEdge(
            strokeRect,
            "Top",
            new Vector2(
                (node.Width +
                 rightOutward -
                 leftOutward) * 0.5f,
                topOutward -
                node.StrokeTop * 0.5f),
            new Vector2(
                horizontalWidth,
                node.StrokeTop),
            color);
    }

    if (node.StrokeBottom > 0f)
    {
        CreateStrokeEdge(
            strokeRect,
            "Bottom",
            new Vector2(
                (node.Width +
                 rightOutward -
                 leftOutward) * 0.5f,
                -node.Height +
                node.StrokeBottom * 0.5f -
                bottomOutward),
            new Vector2(
                horizontalWidth,
                node.StrokeBottom),
            color);
    }

    if (node.StrokeLeft > 0f)
    {
        CreateStrokeEdge(
            strokeRect,
            "Left",
            new Vector2(
                node.StrokeLeft * 0.5f -
                leftOutward,
                (topOutward -
                 node.Height -
                 bottomOutward) * 0.5f),
            new Vector2(
                node.StrokeLeft,
                verticalHeight),
            color);
    }

    if (node.StrokeRight > 0f)
    {
        CreateStrokeEdge(
            strokeRect,
            "Right",
            new Vector2(
                node.Width -
                node.StrokeRight * 0.5f +
                rightOutward,
                (topOutward -
                 node.Height -
                 bottomOutward) * 0.5f),
            new Vector2(
                node.StrokeRight,
                verticalHeight),
            color);
    }
}




    private static bool SupportsRectangularStroke(
    string sourceType)
{
    switch (sourceType)
    {
        case "FRAME":
        case "RECTANGLE":
        case "COMPONENT":
        case "COMPONENT_SET":
        case "INSTANCE":
        case "SECTION":
        case "GROUP":
            return true;

        default:
            return false;
    }
}



    private static float CalculateOutwardAmount(
    float weight,
    UiStrokeAlign align)
{
    switch (align)
    {
        case UiStrokeAlign.Outside:
            return weight;

        case UiStrokeAlign.Center:
            return weight * 0.5f;

        case UiStrokeAlign.Inside:
        default:
            return 0f;
    }
}




    private static void CreateStrokeEdge(
    RectTransform parent,
    string edgeName,
    Vector2 position,
    Vector2 size,
    Color color)
{
    var edgeObject = new GameObject(
        edgeName,
        typeof(RectTransform),
        typeof(Image));

    Undo.RegisterCreatedObjectUndo(
        edgeObject,
        "Create Figma Stroke Edge");

    RectTransform edgeRect =
        edgeObject.GetComponent<RectTransform>();

    Undo.SetTransformParent(
        edgeRect,
        parent,
        "Parent Figma Stroke Edge");

    edgeRect.anchorMin =
        new Vector2(0f, 1f);

    edgeRect.anchorMax =
        new Vector2(0f, 1f);

    edgeRect.pivot =
        new Vector2(0.5f, 0.5f);

    edgeRect.anchoredPosition =
        position;

    edgeRect.sizeDelta =
        new Vector2(
            Mathf.Max(0f, size.x),
            Mathf.Max(0f, size.y));

    edgeRect.localRotation =
        Quaternion.identity;

    edgeRect.localScale =
        Vector3.one;

    Image image =
        edgeObject.GetComponent<Image>();

    image.color = color;
    image.type = Image.Type.Simple;
    image.raycastTarget = false;
}




    private static void ApplyStrokeOpacity(
    GameObject strokeObject,
    UiNodeData node)
{
    float opacity =
        Mathf.Clamp01(node.Opacity);

    if (Mathf.Approximately(opacity, 1f))
    {
        return;
    }

    CanvasGroup canvasGroup =
        Undo.AddComponent<CanvasGroup>(
            strokeObject);

    canvasGroup.alpha = opacity;
    canvasGroup.ignoreParentGroups = false;
}




    private static void ApplyContentClip(
    GameObject gameObject,
    UiNodeData node)
{
    if (!node.ClipsContent)
    {
        return;
    }

    if (gameObject.TryGetComponent<RectMask2D>(out _))
    {
        return;
    }

    RectMask2D rectMask =
        Undo.AddComponent<RectMask2D>(
            gameObject);

    rectMask.padding = Vector4.zero;
}



    private static void ApplyImageFill(
    GameObject gameObject,
    UiNodeData node,
    string fileKey,
    IReadOnlyDictionary<string, string> imageMap)
{
    if (string.IsNullOrWhiteSpace(node.ImageRef))
    {
        return;
    }

    if (!imageMap.TryGetValue(
            node.ImageRef,
            out string relativePath))
    {
        Debug.LogWarning(
            $"Image reference was not found in image-map.json.\n" +
            $"Node: {node.Name}\n" +
            $"ImageRef: {node.ImageRef}");

        return;
    }

    string assetPath =
        $"{OutputRoot}/{fileKey}/{relativePath}"
            .Replace('\\', '/');

    Sprite sprite =
        LoadSprite(assetPath);

    if (sprite == null)
    {
        Debug.LogWarning(
            $"Sprite could not be loaded: {assetPath}");

        return;
    }

    RectTransform parentRect =
        gameObject.GetComponent<RectTransform>();

    RectTransform clipRect =
        CreateImageClipObject(
            parentRect);

    CreateImageVisual(
        clipRect,
        node,
        sprite);
}




    private static Sprite LoadSprite(
    string assetPath)
{
    Sprite sprite =
        AssetDatabase.LoadAssetAtPath<Sprite>(
            assetPath);

    if (sprite != null)
    {
        return sprite;
    }

    TextureImporter importer =
        AssetImporter.GetAtPath(assetPath)
        as TextureImporter;

    if (importer == null)
    {
        Debug.LogWarning(
            $"The image is not a supported texture asset: " +
            $"{assetPath}");

        return null;
    }

    bool settingsChanged = false;

    if (importer.textureType !=
        TextureImporterType.Sprite)
    {
        importer.textureType =
            TextureImporterType.Sprite;

        settingsChanged = true;
    }

    if (importer.spriteImportMode !=
        SpriteImportMode.Single)
    {
        importer.spriteImportMode =
            SpriteImportMode.Single;

        settingsChanged = true;
    }

    if (importer.mipmapEnabled)
    {
        importer.mipmapEnabled = false;
        settingsChanged = true;
    }

    if (!importer.alphaIsTransparency)
    {
        importer.alphaIsTransparency = true;
        settingsChanged = true;
    }

    if (settingsChanged)
    {
        importer.SaveAndReimport();
    }

    return AssetDatabase.LoadAssetAtPath<Sprite>(
        assetPath);
}




    private static RectTransform CreateImageClipObject(
    RectTransform parent)
{
    var clipObject = new GameObject(
        "__ImageFill",
        typeof(RectTransform),
        typeof(RectMask2D));

    Undo.RegisterCreatedObjectUndo(
        clipObject,
        "Create Figma Image Fill");

    var rectTransform =
        clipObject.GetComponent<RectTransform>();

    Undo.SetTransformParent(
        rectTransform,
        parent,
        "Parent Figma Image Fill");

    rectTransform.anchorMin =
        Vector2.zero;

    rectTransform.anchorMax =
        Vector2.one;

    rectTransform.pivot =
        new Vector2(0.5f, 0.5f);

    rectTransform.offsetMin =
        Vector2.zero;

    rectTransform.offsetMax =
        Vector2.zero;

    rectTransform.localRotation =
        Quaternion.identity;

    rectTransform.localScale =
        Vector3.one;

    return rectTransform;
}




    private static void CreateImageVisual(
    RectTransform clipRect,
    UiNodeData node,
    Sprite sprite)
{
    var imageObject = new GameObject(
        "Image",
        typeof(RectTransform),
        typeof(Image));

    Undo.RegisterCreatedObjectUndo(
        imageObject,
        "Create Figma Image");

    var rectTransform =
        imageObject.GetComponent<RectTransform>();

    Undo.SetTransformParent(
        rectTransform,
        clipRect,
        "Parent Figma Image");

    rectTransform.anchorMin =
        new Vector2(0.5f, 0.5f);

    rectTransform.anchorMax =
        new Vector2(0.5f, 0.5f);

    rectTransform.pivot =
        new Vector2(0.5f, 0.5f);

    rectTransform.anchoredPosition =
        Vector2.zero;

    rectTransform.localRotation =
        Quaternion.identity;

    rectTransform.localScale =
        Vector3.one;

    rectTransform.sizeDelta =
        CalculateImageSize(
            node,
            sprite);

    Image image =
        imageObject.GetComponent<Image>();

    image.sprite = sprite;
    image.type = Image.Type.Simple;
    image.preserveAspect = false;
    image.raycastTarget = false;

    image.color = new Color(
    1f,
    1f,
    1f,
    Mathf.Clamp01(node.ImageOpacity));
}




    private static void ApplyNodeOpacity(
    GameObject gameObject,
    UiNodeData node)
{
    float opacity =
        Mathf.Clamp01(node.Opacity);

    if (Mathf.Approximately(opacity, 1f))
    {
        return;
    }

    CanvasGroup canvasGroup =
        Undo.AddComponent<CanvasGroup>(
            gameObject);

    canvasGroup.alpha = opacity;

    // 親CanvasGroupの影響を受ける。
    canvasGroup.ignoreParentGroups = false;
}




    private static Vector2 CalculateImageSize(
    UiNodeData node,
    Sprite sprite)
{
    float targetWidth =
        Mathf.Max(node.Width, 0.01f);

    float targetHeight =
        Mathf.Max(node.Height, 0.01f);

    float spriteWidth =
        Mathf.Max(sprite.rect.width, 0.01f);

    float spriteHeight =
        Mathf.Max(sprite.rect.height, 0.01f);

    float targetAspect =
        targetWidth / targetHeight;

    float spriteAspect =
        spriteWidth / spriteHeight;

    string scaleMode =
        node.ImageScaleMode
            ?.ToUpperInvariant()
        ?? "FILL";

    switch (scaleMode)
    {
        case "FIT":
            return CalculateFitSize(
                targetWidth,
                targetHeight,
                targetAspect,
                spriteAspect);

        case "CROP":
            Debug.LogWarning(
                $"CROP is temporarily treated as FILL: " +
                $"{node.Name}");

            return CalculateFillSize(
                targetWidth,
                targetHeight,
                targetAspect,
                spriteAspect);

        case "TILE":
            Debug.LogWarning(
                $"TILE is temporarily treated as FILL: " +
                $"{node.Name}");

            return CalculateFillSize(
                targetWidth,
                targetHeight,
                targetAspect,
                spriteAspect);

        case "FILL":
        default:
            return CalculateFillSize(
                targetWidth,
                targetHeight,
                targetAspect,
                spriteAspect);
    }
}




    private static Vector2 CalculateFitSize(
    float targetWidth,
    float targetHeight,
    float targetAspect,
    float spriteAspect)
{
    if (spriteAspect > targetAspect)
    {
        return new Vector2(
            targetWidth,
            targetWidth / spriteAspect);
    }

    return new Vector2(
        targetHeight * spriteAspect,
        targetHeight);
}




    private static Vector2 CalculateFillSize(
    float targetWidth,
    float targetHeight,
    float targetAspect,
    float spriteAspect)
{
    if (spriteAspect > targetAspect)
    {
        return new Vector2(
            targetHeight * spriteAspect,
            targetHeight);
    }

    return new Vector2(
        targetWidth,
        targetWidth / spriteAspect);
}



    private static Dictionary<string, string> LoadImageMap(
    string fileKey)
{
    var result =
        new Dictionary<string, string>();

    string mapPath =
        $"{OutputRoot}/{fileKey}/image-map.json";

    if (!File.Exists(mapPath))
    {
        Debug.LogWarning(
            $"image-map.json was not found: {mapPath}");

        return result;
    }

    string json =
        File.ReadAllText(
            mapPath,
            Encoding.UTF8);

    JObject root =
        JObject.Parse(json);

    foreach (JProperty property in root.Properties())
    {
        string relativePath =
            property.Value.Value<string>();

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            continue;
        }

        result[property.Name] =
            relativePath;
    }

    return result;
}



    private static void ApplyText(
    GameObject gameObject,
    UiNodeData node)
{
    if (node.Kind != UiNodeKind.Text ||
        node.Text == null)
    {
        return;
    }

    TextMeshProUGUI text =
        Undo.AddComponent<TextMeshProUGUI>(
            gameObject);

    text.text =
        node.Text.Characters
        ?? string.Empty;

    TMP_FontAsset defaultFont =
        TMP_Settings.defaultFontAsset;

    if (defaultFont != null)
    {
        text.font = defaultFont;
    }
    else
    {
        Debug.LogWarning(
            $"TMP default font asset is not configured: " +
            $"{node.Name}");
    }

    text.fontSize =
        node.Text.FontSize > 0f
            ? node.Text.FontSize
            : 14f;

    text.enableAutoSizing = false;
    text.autoSizeTextContainer = false;

    // Figma上の文字列をそのまま表示する。
    // <b>などをTMPタグとして解釈させない。
    text.richText = false;

    text.horizontalAlignment =
        ConvertHorizontalAlignment(
            node.Text.HorizontalAlignment);

    text.verticalAlignment =
        ConvertVerticalAlignment(
            node.Text.VerticalAlignment);

    text.fontStyle =
        ConvertFontStyle(
            node.Text.FontWeight);

    text.color =
        ReadTextColor(node);

    text.raycastTarget = false;

    // Figmaのテキスト領域は既にRectTransformへ
    // 反映しているため、TMP側でサイズ変更させない。
    text.margin = Vector4.zero;
}



    private static Color ReadTextColor(
    UiNodeData node)
{
    if (!node.HasSolidFill)
    {
        return Color.black;
    }

    return new Color(
        Mathf.Clamp01(node.SolidFill.R),
        Mathf.Clamp01(node.SolidFill.G),
        Mathf.Clamp01(node.SolidFill.B),
        Mathf.Clamp01(node.SolidFill.A));
}



    private static HorizontalAlignmentOptions
    ConvertHorizontalAlignment(
        string figmaAlignment)
{
    switch (figmaAlignment)
    {
        case "CENTER":
            return HorizontalAlignmentOptions.Center;

        case "RIGHT":
            return HorizontalAlignmentOptions.Right;

        case "JUSTIFIED":
            return HorizontalAlignmentOptions.Justified;

        case "LEFT":
        default:
            return HorizontalAlignmentOptions.Left;
    }
}



    private static VerticalAlignmentOptions
    ConvertVerticalAlignment(
        string figmaAlignment)
{
    switch (figmaAlignment)
    {
        case "CENTER":
            return VerticalAlignmentOptions.Middle;

        case "BOTTOM":
            return VerticalAlignmentOptions.Bottom;

        case "TOP":
        default:
            return VerticalAlignmentOptions.Top;
    }
}


    private static FontStyles ConvertFontStyle(
    float figmaFontWeight)
{
    return figmaFontWeight >= 600f
        ? FontStyles.Bold
        : FontStyles.Normal;
}




    private static void ApplySolidFill(
    GameObject gameObject,
    UiNodeData node)
{
    if (!node.HasSolidFill)
    {
        return;
    }

    bool supportsBackgroundFill =
        node.Kind == UiNodeKind.Container ||
        node.Kind == UiNodeKind.Rectangle;

    if (!supportsBackgroundFill)
    {
        return;
    }

    Image image =
        Undo.AddComponent<Image>(gameObject);

    image.color = new Color(
        Mathf.Clamp01(node.SolidFill.R),
        Mathf.Clamp01(node.SolidFill.G),
        Mathf.Clamp01(node.SolidFill.B),
        Mathf.Clamp01(node.SolidFill.A));

    image.type = Image.Type.Simple;
    image.preserveAspect = false;
    image.raycastTarget = false;
}




    private static void ApplyRectTransform(
    RectTransform rectTransform,
    UiNodeData node,
    bool isRoot)
{
    // 全ノードの基準を左上へ統一する。
    rectTransform.anchorMin =
        new Vector2(0f, 1f);

    rectTransform.anchorMax =
        new Vector2(0f, 1f);

    rectTransform.pivot =
        new Vector2(0f, 1f);

    rectTransform.sizeDelta =
        new Vector2(
            Mathf.Max(0f, node.Width),
            Mathf.Max(0f, node.Height));

    float x = isRoot
        ? 0f
        : node.X;

    // Figmaは下方向が正、
    // Unity UIは上方向が正なので反転する。
    float y = isRoot
        ? 0f
        : -node.Y;

    rectTransform.anchoredPosition3D =
        new Vector3(x, y, 0f);

    // Figmaの正方向とUnityの見た目上の
    // 回転方向を合わせるため符号を反転する。
    rectTransform.localRotation =
        Quaternion.Euler(
            0f,
            0f,
            -node.RotationDegrees);

    rectTransform.localScale =
        Vector3.one;
}



    private void BuildIntermediateModel()
{
    try
    {
        ValidateFileKey();

        if (_frames.Count == 0)
        {
            throw new InvalidOperationException(
                "No frame is selected.");
        }

        FrameInfo selectedFrame =
            _frames[_selectedFrameIndex];

        UiNodeData root = ConvertNode(
            selectedFrame.Node,
            isRoot: true,
            parentBounds: null);

        if (root == null)
        {
            throw new InvalidOperationException(
                "The selected frame could not be converted.");
        }

        _intermediateDocument =
            new UiImportDocument
            {
                FileKey = _fileKey.Trim(),
                RootNodeId = selectedFrame.Id,
                RootNodeName = selectedFrame.Name,
                Root = root
            };

        string safeNodeId =
            MakeSafeFileName(selectedFrame.Id);

        _intermediateOutputPath =
            $"{OutputRoot}/{_fileKey.Trim()}/" +
            $"intermediate-{safeNodeId}.json";

        string json = JsonConvert.SerializeObject(
            _intermediateDocument,
            Formatting.Indented);

        WriteText(
            _intermediateOutputPath,
            json);

        AssetDatabase.Refresh();

        Debug.Log(
            $"Intermediate model generated.\n" +
            $"Nodes: {CountNodes(root)}\n" +
            $"Output: {_intermediateOutputPath}");
    }
    catch (Exception exception)
    {
        _intermediateDocument = null;
        _intermediateOutputPath = null;

        Debug.LogException(exception);
    }

    Repaint();
}




    private static UiNodeData ConvertNode(
    JObject source,
    bool isRoot,
    BoundsData? parentBounds)
{
    if (source.Value<bool?>("visible") == false)
    {
        return null;
    }

    string sourceType =
        source.Value<string>("type")
        ?? "UNKNOWN";

    // SLICEはUI生成に使用しない。
    if (sourceType == "SLICE")
    {
        return null;
    }

    BoundsData? bounds =
        ReadBounds(source);

    var destination = new UiNodeData
    {
        Id = source.Value<string>("id")
             ?? string.Empty,

        Name = source.Value<string>("name")
               ?? "(Unnamed)",

        SourceType = sourceType,

        Kind = DetermineNodeKind(
            source,
            sourceType),

        Opacity =
            source.Value<float?>("opacity")
            ?? 1f,

        ClipsContent =
            source.Value<bool?>("clipsContent")
            ?? false,

        CornerRadius =
            source.Value<float?>("cornerRadius")
            ?? 0f,

        LayoutMode =
            ReadLayoutMode(source),

        ItemSpacing =
            source.Value<float?>("itemSpacing")
            ?? 0f,

        PaddingLeft =
            source.Value<float?>("paddingLeft")
            ?? 0f,

        PaddingRight =
            source.Value<float?>("paddingRight")
            ?? 0f,

        PaddingTop =
            source.Value<float?>("paddingTop")
            ?? 0f,

        PaddingBottom =
            source.Value<float?>("paddingBottom")
            ?? 0f
    };

    ReadTransform(
        source,
        destination,
        isRoot,
        bounds,
        parentBounds);

    ReadFills(
    source,
    destination);

    ReadStroke(
        source,
        destination);

    if (sourceType == "TEXT")
    {
        destination.Text =
            ReadTextData(source);
    }

    JArray children =
        source["children"] as JArray;

    if (children != null)
    {
        foreach (JToken childToken in children)
        {
            if (childToken is not JObject childSource)
            {
                continue;
            }

            UiNodeData child = ConvertNode(
                childSource,
                isRoot: false,
                parentBounds: bounds);

            if (child != null)
            {
                destination.Children.Add(child);
            }
        }
    }

    return destination;
}



    private static void ReadStroke(
    JObject source,
    UiNodeData destination)
{
    JArray strokes =
        source["strokes"] as JArray;

    if (strokes == null)
    {
        return;
    }

    JObject solidStroke = null;

    foreach (JToken strokeToken in strokes)
    {
        if (strokeToken is not JObject stroke)
        {
            continue;
        }

        if (stroke.Value<bool?>("visible") == false)
        {
            continue;
        }

        if (stroke.Value<string>("type") != "SOLID")
        {
            continue;
        }

        solidStroke = stroke;
        break;
    }

    if (solidStroke == null)
    {
        return;
    }

    float uniformWeight =
        Mathf.Max(
            0f,
            source.Value<float?>("strokeWeight") ?? 0f);

    float top = uniformWeight;
    float right = uniformWeight;
    float bottom = uniformWeight;
    float left = uniformWeight;

    JObject individualWeights =
        source["individualStrokeWeights"] as JObject;

    if (individualWeights != null)
    {
        top = Mathf.Max(
            0f,
            individualWeights.Value<float?>("top")
            ?? uniformWeight);

        right = Mathf.Max(
            0f,
            individualWeights.Value<float?>("right")
            ?? uniformWeight);

        bottom = Mathf.Max(
            0f,
            individualWeights.Value<float?>("bottom")
            ?? uniformWeight);

        left = Mathf.Max(
            0f,
            individualWeights.Value<float?>("left")
            ?? uniformWeight);
    }

    if (top <= 0f &&
        right <= 0f &&
        bottom <= 0f &&
        left <= 0f)
    {
        return;
    }

    JObject color =
        solidStroke["color"] as JObject;

    if (color == null)
    {
        return;
    }

    float paintOpacity =
        solidStroke.Value<float?>("opacity")
        ?? 1f;

    destination.HasStroke = true;

    destination.StrokeColor =
        new UiColorData
        {
            R = color.Value<float?>("r") ?? 0f,
            G = color.Value<float?>("g") ?? 0f,
            B = color.Value<float?>("b") ?? 0f,

            A =
                (color.Value<float?>("a") ?? 1f)
                * paintOpacity
        };

    destination.StrokeTop = top;
    destination.StrokeRight = right;
    destination.StrokeBottom = bottom;
    destination.StrokeLeft = left;

    destination.StrokeAlign =
        ReadStrokeAlign(
            source.Value<string>("strokeAlign"));
}



    private static UiStrokeAlign ReadStrokeAlign(
    string strokeAlign)
{
    switch (strokeAlign)
    {
        case "OUTSIDE":
            return UiStrokeAlign.Outside;

        case "CENTER":
            return UiStrokeAlign.Center;

        case "INSIDE":
        default:
            return UiStrokeAlign.Inside;
    }
}



    private static UiNodeKind DetermineNodeKind(
    JObject node,
    string sourceType)
{
    if (sourceType == "TEXT")
    {
        return UiNodeKind.Text;
    }

    if (HasImageFill(node))
    {
        return UiNodeKind.Image;
    }

    switch (sourceType)
    {
        case "FRAME":
        case "GROUP":
        case "SECTION":
        case "COMPONENT":
        case "COMPONENT_SET":
        case "INSTANCE":
            return UiNodeKind.Container;

        case "RECTANGLE":
            return UiNodeKind.Rectangle;

        case "ELLIPSE":
        case "VECTOR":
        case "BOOLEAN_OPERATION":
        case "LINE":
        case "POLYGON":
        case "STAR":
            return UiNodeKind.Vector;

        default:
            return UiNodeKind.Unsupported;
    }
}




    private static void ReadTransform(
    JObject source,
    UiNodeData destination,
    bool isRoot,
    BoundsData? bounds,
    BoundsData? parentBounds)
{
    JObject size =
        source["size"] as JObject;

    destination.Width =
        size?.Value<float?>("x")
        ?? bounds?.Width
        ?? 0f;

    destination.Height =
        size?.Value<float?>("y")
        ?? bounds?.Height
        ?? 0f;

    if (isRoot)
    {
        destination.X = 0f;
        destination.Y = 0f;
        destination.RotationDegrees = 0f;
        return;
    }

    JArray transform =
        source["relativeTransform"] as JArray;

    if (transform != null &&
        transform.Count >= 2)
    {
        JArray row0 =
            transform[0] as JArray;

        JArray row1 =
            transform[1] as JArray;

        if (row0 != null &&
            row1 != null &&
            row0.Count >= 3 &&
            row1.Count >= 3)
        {
            float matrix00 =
                ReadArrayFloat(row0, 0);

            float matrix10 =
                ReadArrayFloat(row1, 0);

            destination.X =
                ReadArrayFloat(row0, 2);

            destination.Y =
                ReadArrayFloat(row1, 2);

            destination.RotationDegrees =
                Mathf.Atan2(
                    matrix10,
                    matrix00)
                * Mathf.Rad2Deg;

            return;
        }
    }

    // relativeTransformがない場合のフォールバック
    if (bounds.HasValue &&
        parentBounds.HasValue)
    {
        destination.X =
            bounds.Value.X -
            parentBounds.Value.X;

        destination.Y =
            bounds.Value.Y -
            parentBounds.Value.Y;
    }
}



    private static float ReadArrayFloat(
    JArray array,
    int index)
{
    if (array == null ||
        index < 0 ||
        index >= array.Count)
    {
        return 0f;
    }

    return array[index].Value<float?>()
           ?? 0f;
}

private static BoundsData? ReadBounds(
    JObject node)
{
    JObject bounds =
        node["absoluteBoundingBox"] as JObject;

    if (bounds == null)
    {
        return null;
    }

    return new BoundsData
    {
        X = bounds.Value<float?>("x") ?? 0f,
        Y = bounds.Value<float?>("y") ?? 0f,
        Width = bounds.Value<float?>("width") ?? 0f,
        Height = bounds.Value<float?>("height") ?? 0f
    };
}





    private void AnalyzeSelectedFrame()
{
    if (_frames.Count == 0)
    {
        return;
    }

    FrameInfo selectedFrame =
        _frames[_selectedFrameIndex];

    var analysis = new FrameAnalysis();

    AnalyzeNode(
        selectedFrame.Node,
        analysis);

    _frameAnalysis = analysis;

    Debug.Log(
        $"Frame analyzed: {selectedFrame.Name}\n" +
        $"Visible nodes: {analysis.VisibleNodes}\n" +
        $"Unsupported nodes: {analysis.UnsupportedNodes}");
}



    private static void AnalyzeNode(
    JObject node,
    FrameAnalysis analysis)
{
    analysis.VisitedNodes++;

    // 非表示ノード以下は生成対象にしない。
    if (node.Value<bool?>("visible") == false)
    {
        analysis.HiddenSubtreeRoots++;
        return;
    }

    analysis.VisibleNodes++;

    string nodeType =
        node.Value<string>("type")
        ?? "UNKNOWN";

    AddNodeTypeCount(
        analysis,
        nodeType);

    ClassifyNode(
        nodeType,
        analysis);

    if (HasImageFill(node))
    {
        analysis.ImageFillNodes++;
    }

    string layoutMode =
        node.Value<string>("layoutMode");

    if (layoutMode == "HORIZONTAL" ||
        layoutMode == "VERTICAL" ||
        layoutMode == "GRID")
    {
        analysis.AutoLayoutNodes++;
    }

    if (node.Value<bool?>("isMask") == true)
    {
        analysis.MaskNodes++;
    }

    JArray effects =
        node["effects"] as JArray;

    if (effects != null &&
        effects.Count > 0)
    {
        analysis.EffectNodes++;
    }

    JArray children =
        node["children"] as JArray;

    if (children == null)
    {
        return;
    }

    foreach (JToken childToken in children)
    {
        if (childToken is JObject childNode)
        {
            AnalyzeNode(
                childNode,
                analysis);
        }
    }
}


    private static void ClassifyNode(
    string nodeType,
    FrameAnalysis analysis)
{
    switch (nodeType)
    {
        // RectTransform階層として扱う候補
        case "FRAME":
        case "GROUP":
        case "SECTION":
        case "COMPONENT":
        case "COMPONENT_SET":
        case "INSTANCE":
            analysis.StructureNodes++;
            break;

        // uGUI Imageへ直接変換する候補
        case "RECTANGLE":
            analysis.RectangleNodes++;
            break;

        // TextMeshProUGUIへ変換する候補
        case "TEXT":
            analysis.TextNodes++;
            break;

        // SVGまたはラスター画像として扱う候補
        case "ELLIPSE":
        case "VECTOR":
        case "BOOLEAN_OPERATION":
        case "LINE":
        case "POLYGON":
        case "STAR":
            analysis.VectorGraphicNodes++;
            break;

        // Unity生成には使用しない
        case "SLICE":
            analysis.IgnoredNodes++;
            break;

        // 現段階では変換方針未定
        default:
            analysis.UnsupportedNodes++;
            analysis.UnsupportedTypes.Add(nodeType);
            break;
    }
}


    private static void AddNodeTypeCount(
    FrameAnalysis analysis,
    string nodeType)
{
    if (analysis.NodeTypeCounts.TryGetValue(
            nodeType,
            out int count))
    {
        analysis.NodeTypeCounts[nodeType] =
            count + 1;
    }
    else
    {
        analysis.NodeTypeCounts[nodeType] = 1;
    }
}


    private static bool HasImageFill(
    JObject node)
{
    JArray fills =
        node["fills"] as JArray;

    if (fills == null)
    {
        return false;
    }

    foreach (JToken fillToken in fills)
    {
        if (fillToken is not JObject fill)
        {
            continue;
        }

        if (fill.Value<bool?>("visible") == false)
        {
            continue;
        }

        if (fill.Value<string>("type") == "IMAGE")
        {
            return true;
        }
    }

    return false;
}


    private void SelectPage(int pageIndex)
    {
        _intermediateDocument = null;
        _intermediateOutputPath = null;
        _selectedPageIndex = Mathf.Clamp(
            pageIndex,
            0,
            _pages.Count - 1);

        _selectedFrameIndex = 0;
        _frameAnalysis = null;
        _frames.Clear();

        PageInfo page = _pages[_selectedPageIndex];

        JArray children =
            page.Node["children"] as JArray;

        if (children == null)
        {
            return;
        }

        foreach (JToken childToken in children)
        {
            if (childToken is JObject childNode)
            {
                CollectImportableFrames(
                    childNode,
                    string.Empty);
            }
        }
    }


private void CollectImportableFrames(
    JObject node,
    string parentPath)
{
    string nodeType =
        node.Value<string>("type");

    string nodeName =
        node.Value<string>("name")
        ?? "(Unnamed)";

    string currentPath =
        string.IsNullOrEmpty(parentPath)
            ? nodeName
            : $"{parentPath}/{nodeName}";

    if (nodeType == "FRAME")
    {
        JObject bounds =
            node["absoluteBoundingBox"] as JObject;

        float width =
            bounds?.Value<float?>("width") ?? 0f;

        float height =
            bounds?.Value<float?>("height") ?? 0f;

        _frames.Add(
            new FrameInfo(
                node.Value<string>("id") ?? string.Empty,
                nodeName,
                currentPath,
                width,
                height,
                node));

        // Frame内部のAuto Layout用Frameなどは候補にしない。
        return;
    }

    // SectionまたはGroup内に配置された画面Frameを探す。
    if (nodeType != "SECTION" &&
        nodeType != "GROUP")
    {
        return;
    }

    JArray children =
        node["children"] as JArray;

    if (children == null)
    {
        return;
    }

    foreach (JToken childToken in children)
    {
        if (childToken is JObject childNode)
        {
            CollectImportableFrames(
                childNode,
                currentPath);
        }
    }
}

private void ValidateFileKey()
{
    if (string.IsNullOrWhiteSpace(_fileKey))
    {
        throw new InvalidOperationException(
            "File Key is empty.");
    }

    if (_fileKey.Contains("/") ||
        _fileKey.Contains("?") ||
        _fileKey.Contains("#"))
    {
        throw new InvalidOperationException(
            "Enter only the File Key.");
    }
}



    private async void DownloadImagesFromCache()
{
    try
    {
        ValidateInput();

        string fileKey = _fileKey.Trim();
        string outputDirectory = $"{OutputRoot}/{fileKey}";
        string imageDirectory = $"{outputDirectory}/Images";
        string imageFillPath =
            $"{outputDirectory}/image-fills.json";

        if (!File.Exists(imageFillPath))
        {
            Debug.LogError(
                $"image-fills.json does not exist: {imageFillPath}");

            return;
        }

        Directory.CreateDirectory(imageDirectory);

        string imageFillJson =
            File.ReadAllText(imageFillPath, Encoding.UTF8);

        JObject localImageMap = await DownloadImageFills(
            imageFillJson,
            imageDirectory);

        WriteText(
            $"{outputDirectory}/image-map.json",
            localImageMap.ToString());

        AssetDatabase.Refresh();

        Debug.Log(
            $"Downloaded {localImageMap.Count} image fills.");
    }
    catch (Exception exception)
    {
        Debug.LogException(exception);
    }
    finally
    {
        EditorUtility.ClearProgressBar();
    }
}

    

    private async void DownloadDocumentJson()
{
    if (_isDownloading)
    {
        return;
    }

    _isDownloading = true;
    Repaint();

    try
    {
        ValidateInput();

        string fileKey = _fileKey.Trim();
        string outputDirectory = $"{OutputRoot}/{fileKey}";

        Directory.CreateDirectory(outputDirectory);

        string requestUrl =
            $"{ApiBaseUrl}/files/{fileKey}?geometry=paths";

        string documentJson = await DownloadApiText(
            requestUrl,
            _token.Trim());

        WriteText(
            $"{outputDirectory}/document.json",
            documentJson);

        AssetDatabase.Refresh();

        Debug.Log(
            $"Figma document downloaded.\n" +
            $"Output: {outputDirectory}/document.json");
    }
    catch (Exception exception)
    {
        Debug.LogException(exception);
    }
    finally
    {
        _isDownloading = false;
        Repaint();
    }
}



    private async Task<JObject> DownloadImageFills(
    string imageFillJson,
    string imageDirectory)
{
    JObject response = JObject.Parse(imageFillJson);

    // Figma APIの両方のレスポンス形式に対応
    JObject images = response["images"] as JObject;

    if (images == null)
    {
        images = response["meta"]?["images"] as JObject;
    }

    var localImageMap = new JObject();

    if (images == null || !images.HasValues)
    {
        Debug.LogWarning("Image fill data was not found.");
        return localImageMap;
    }

    int index = 0;
    int count = images.Count;

    foreach (JProperty property in images.Properties())
    {
        index++;

        string imageRef = property.Name;
        string imageUrl = property.Value.Type == JTokenType.Null
            ? null
            : property.Value.ToString();

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            Debug.LogWarning(
                $"Image URL was not returned: {imageRef}");

            continue;
        }

        EditorUtility.DisplayProgressBar(
            "Downloading Figma Images",
            $"{index} / {count}",
            (float)index / count);

        DownloadedFile downloadedFile =
            await DownloadBinary(imageUrl);

        string safeImageRef = MakeSafeFileName(imageRef);
        string extension = DetermineExtension(
            downloadedFile.ContentType,
            imageUrl);

        string fileName = safeImageRef + extension;
        string fullPath = $"{imageDirectory}/{fileName}";

        File.WriteAllBytes(
            fullPath,
            downloadedFile.Data);

        localImageMap[imageRef] = $"Images/{fileName}";
    }

    return localImageMap;
}



    private static void ReadFills(
    JObject source,
    UiNodeData destination)
{
    JArray fills =
        source["fills"] as JArray;

    if (fills == null)
    {
        return;
    }

    foreach (JToken fillToken in fills)
    {
        if (fillToken is not JObject fill)
        {
            continue;
        }

        if (fill.Value<bool?>("visible") == false)
        {
            continue;
        }

        string fillType =
            fill.Value<string>("type");

        if (fillType == "IMAGE" &&
            string.IsNullOrWhiteSpace(destination.ImageRef))
        {
            destination.ImageRef =
                fill.Value<string>("imageRef");

            destination.ImageScaleMode =
                fill.Value<string>("scaleMode");

            destination.ImageOpacity =
                fill.Value<float?>("opacity")
                ?? 1f;

            continue;
        }

        if (fillType != "SOLID" ||
            destination.HasSolidFill)
        {
            continue;
        }

        JObject color =
            fill["color"] as JObject;

        if (color == null)
        {
            continue;
        }

        float fillOpacity =
            fill.Value<float?>("opacity")
            ?? 1f;

        destination.HasSolidFill = true;

        destination.SolidFill =
            new UiColorData
            {
                R = color.Value<float?>("r") ?? 0f,
                G = color.Value<float?>("g") ?? 0f,
                B = color.Value<float?>("b") ?? 0f,

                A =
                    (color.Value<float?>("a") ?? 1f)
                    * fillOpacity
            };
    }
}




    private static UiTextData ReadTextData(
    JObject source)
{
    JObject style =
        source["style"] as JObject;

    return new UiTextData
    {
        Characters =
            source.Value<string>("characters")
            ?? string.Empty,

        FontFamily =
            style?.Value<string>("fontFamily")
            ?? string.Empty,

        FontPostScriptName =
            style?.Value<string>("fontPostScriptName")
            ?? string.Empty,

        FontSize =
            style?.Value<float?>("fontSize")
            ?? 0f,

        FontWeight =
            style?.Value<float?>("fontWeight")
            ?? 400f,

        LetterSpacing =
            style?.Value<float?>("letterSpacing")
            ?? 0f,

        LineHeight =
            style?.Value<float?>("lineHeightPx")
            ?? 0f,

        HorizontalAlignment =
            style?.Value<string>("textAlignHorizontal")
            ?? "LEFT",

        VerticalAlignment =
            style?.Value<string>("textAlignVertical")
            ?? "TOP"
    };
}




    private static UiLayoutMode ReadLayoutMode(
    JObject source)
{
    switch (source.Value<string>("layoutMode"))
    {
        case "HORIZONTAL":
            return UiLayoutMode.Horizontal;

        case "VERTICAL":
            return UiLayoutMode.Vertical;

        case "GRID":
            return UiLayoutMode.Grid;

        default:
            return UiLayoutMode.None;
    }
}





    private static int CountNodes(
    UiNodeData node)
{
    if (node == null)
    {
        return 0;
    }

    int count = 1;

    foreach (UiNodeData child in node.Children)
    {
        count += CountNodes(child);
    }

    return count;
}



    private void DrawFrameAnalysis(
    FrameAnalysis analysis)
{
    EditorGUILayout.Space();

    EditorGUILayout.LabelField(
        "Frame Analysis",
        EditorStyles.boldLabel);

    using (new EditorGUI.DisabledScope(true))
    {
        EditorGUILayout.IntField(
            "Visible Nodes",
            analysis.VisibleNodes);

        EditorGUILayout.IntField(
            "Hidden Subtrees",
            analysis.HiddenSubtreeRoots);

        EditorGUILayout.IntField(
            "Structure Nodes",
            analysis.StructureNodes);

        EditorGUILayout.IntField(
            "Rectangles",
            analysis.RectangleNodes);

        EditorGUILayout.IntField(
            "Text Nodes",
            analysis.TextNodes);

        EditorGUILayout.IntField(
            "Vector Graphics",
            analysis.VectorGraphicNodes);

        EditorGUILayout.IntField(
            "Image Fill Nodes",
            analysis.ImageFillNodes);

        EditorGUILayout.IntField(
            "Auto Layout Nodes",
            analysis.AutoLayoutNodes);

        EditorGUILayout.IntField(
            "Mask Nodes",
            analysis.MaskNodes);

        EditorGUILayout.IntField(
            "Effect Nodes",
            analysis.EffectNodes);

        EditorGUILayout.IntField(
            "Ignored Nodes",
            analysis.IgnoredNodes);

        EditorGUILayout.IntField(
            "Unsupported Nodes",
            analysis.UnsupportedNodes);
    }

    if (analysis.UnsupportedTypes.Count > 0)
    {
        EditorGUILayout.HelpBox(
            "Unsupported node types: " +
            string.Join(", ", analysis.UnsupportedTypes),
            MessageType.Warning);
    }

    _showNodeTypeBreakdown =
        EditorGUILayout.Foldout(
            _showNodeTypeBreakdown,
            "Node Type Breakdown",
            true);

    if (!_showNodeTypeBreakdown)
    {
        return;
    }

    EditorGUI.indentLevel++;

    foreach (KeyValuePair<string, int> pair
             in analysis.NodeTypeCounts)
    {
        EditorGUILayout.LabelField(
            pair.Key,
            pair.Value.ToString());
    }

    EditorGUI.indentLevel--;
}

    private static async Task<string> DownloadApiText(
        string url,
        string token)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader(
                "X-Figma-Token",
                token);

            await Send(request);

            return request.downloadHandler.text;
        }
    }

    private static async Task<DownloadedFile> DownloadBinary(
        string url)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            await Send(request);

            return new DownloadedFile
            {
                Data = request.downloadHandler.data,
                ContentType = request.GetResponseHeader("Content-Type")
            };
        }
    }

    private static async Task Send(UnityWebRequest request)
    {
        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();

        while (!operation.isDone)
        {
            await Task.Yield();
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            string responseBody =
                request.downloadHandler != null
                    ? request.downloadHandler.text
                    : string.Empty;

            throw new InvalidOperationException(
                $"HTTP request failed.\n" +
                $"URL: {request.url}\n" +
                $"Status: {request.responseCode}\n" +
                $"Error: {request.error}\n" +
                $"Response: {responseBody}");
        }
    }

private void ValidateInput()
{
    if (string.IsNullOrWhiteSpace(_token))
    {
        throw new InvalidOperationException(
            "Personal Access Token is empty.");
    }

    ValidateFileKey();
}



    private static void WriteText(
        string path,
        string content)
    {
        File.WriteAllText(
            path,
            content,
            new UTF8Encoding(false));
    }

    private static string DetermineExtension(
        string contentType,
        string url)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            string normalizedContentType =
                contentType.Split(';')[0]
                    .Trim()
                    .ToLowerInvariant();

            switch (normalizedContentType)
            {
                case "image/png":
                    return ".png";

                case "image/jpeg":
                    return ".jpg";

                case "image/webp":
                    return ".webp";

                case "image/gif":
                    return ".gif";

                case "image/svg+xml":
                    return ".svg";
            }
        }

        string extension = Path.GetExtension(
            new Uri(url).AbsolutePath);

        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension;
        }

        return ".bin";
    }

    private static string MakeSafeFileName(string value)
    {
        foreach (char invalidCharacter
                 in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(
                invalidCharacter,
                '_');
        }

        return value;
    }

    private sealed class DownloadedFile
    {
        public byte[] Data;
        public string ContentType;
    }

    private sealed class PageInfo
{
    public string Name { get; }
    public JObject Node { get; }

    public PageInfo(
        string name,
        JObject node)
    {
        Name = name;
        Node = node;
    }
}

    private sealed class FrameInfo
{
    public string Id { get; }
    public string Name { get; }
    public string Path { get; }
    public float Width { get; }
    public float Height { get; }
    public JObject Node { get; }

    public FrameInfo(
        string id,
        string name,
        string path,
        float width,
        float height,
        JObject node)
    {
        Id = id;
        Name = name;
        Path = path;
        Width = width;
        Height = height;
        Node = node;
    }
}


    private sealed class FrameAnalysis
{
    public int VisitedNodes;
    public int VisibleNodes;
    public int HiddenSubtreeRoots;

    public int StructureNodes;
    public int RectangleNodes;
    public int TextNodes;
    public int VectorGraphicNodes;
    public int ImageFillNodes;

    public int AutoLayoutNodes;
    public int MaskNodes;
    public int EffectNodes;

    public int IgnoredNodes;
    public int UnsupportedNodes;

    public readonly SortedDictionary<string, int>
        NodeTypeCounts = new();

    public readonly SortedSet<string>
        UnsupportedTypes = new();
}

    private struct BoundsData
{
    public float X;
    public float Y;
    public float Width;
    public float Height;
}
}

#endif