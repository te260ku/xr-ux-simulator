#if UNITY_EDITOR

using System;
using System.Collections.Generic;

[Serializable]
public sealed class UiImportDocument
{
    public string FileKey;
    public string RootNodeId;
    public string RootNodeName;
    public UiNodeData Root;
}

[Serializable]
public sealed class UiNodeData
{
    public string Id;
    public string Name;
    public string SourceType;

    public UiNodeKind Kind;

    // 親ノード基準の位置
    public float X;
    public float Y;

    public float Width;
    public float Height;
    public float RotationDegrees;

    public float Opacity = 1f;
    public bool ClipsContent;

    public float CornerRadius;

    public UiLayoutMode LayoutMode;
    public float ItemSpacing;
    public float PaddingLeft;
    public float PaddingRight;
    public float PaddingTop;
    public float PaddingBottom;

    public bool HasSolidFill;
    public UiColorData SolidFill;

    public string ImageRef;
    public string ImageScaleMode;

    public UiTextData Text;

    public List<UiNodeData> Children =
        new List<UiNodeData>();

    public float ImageOpacity = 1f;

    public bool HasStroke;
    public UiColorData StrokeColor;

    public float StrokeTop;
    public float StrokeRight;
    public float StrokeBottom;
    public float StrokeLeft;

    public UiStrokeAlign StrokeAlign;
}

public enum UiStrokeAlign
{
    Inside,
    Center,
    Outside
}

public enum UiNodeKind
{
    Container,
    Rectangle,
    Text,
    Image,
    Vector,
    Unsupported
}

public enum UiLayoutMode
{
    None,
    Horizontal,
    Vertical,
    Grid
}

[Serializable]
public struct UiColorData
{
    public float R;
    public float G;
    public float B;
    public float A;
}

[Serializable]
public sealed class UiTextData
{
    public string Characters;

    public string FontFamily;
    public string FontPostScriptName;

    public float FontSize;
    public float FontWeight;

    public float LetterSpacing;
    public float LineHeight;

    public string HorizontalAlignment;
    public string VerticalAlignment;
}

#endif