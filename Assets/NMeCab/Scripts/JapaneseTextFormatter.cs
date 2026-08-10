using System;
using System.Collections.Generic;
using System.Text;
using NMeCab;

public sealed class JapaneseTextFormatter : IDisposable
{
    private readonly MeCabTagger _tagger;

    public JapaneseTextFormatter(string dictionaryPath)
    {
        

        _tagger = MeCabTagger.Create(dictionaryPath);
    }


    public string Format(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var tokens = Parse(source);

        if (tokens.Count == 0)
        {
            return source;
        }

        var result = new StringBuilder();
        var segment = new StringBuilder();

        segment.Append(tokens[0].Surface);

        for (int i = 1; i < tokens.Count; i++)
        {
            Token previous = tokens[i - 1];
            Token current = tokens[i];

            if (ShouldPreventBreak(previous, current))
            {
                segment.Append(current.Surface);
                continue;
            }

            AppendSegment(result, segment);

            result.Append("<zwsp>");

            segment.Clear();
            segment.Append(current.Surface);
        }

        AppendSegment(result, segment);

        return result.ToString();
    }

    private List<Token> Parse(string source)
    {
        var tokens = new List<Token>();

        MeCabNode[] node = _tagger.Parse(source);

        foreach (var item in node)
        {
            if (item.PosId == 0)
            {
                continue;
            }

            tokens.Add(CreateToken(item));
        }

        return tokens;
    }

    private static Token CreateToken(MeCabNode node)
    {
        string[] features =
            (node.Feature ?? string.Empty).Split(',');

        return new Token(
            node.Surface,
            GetFeature(features, 0),
            GetFeature(features, 1));
    }

    private static string GetFeature(
        string[] features,
        int index)
    {
        return index < features.Length
            ? features[index]
            : string.Empty;
    }

    private static bool ShouldPreventBreak(
        Token previous,
        Token current)
    {
        // 行頭に置きたくない
        if (current.Pos == "助詞")
            return true;

        if (current.Pos == "助動詞")
            return true;

        if (current.Pos1 == "接尾")
            return true;

        if (current.Pos1 == "非自立")
            return true;

        if (IsClosingSymbol(current.Surface))
            return true;

        // 行末に残したくない
        if (IsOpeningSymbol(previous.Surface))
            return true;

        if (previous.Pos == "接頭詞")
            return true;

        if (previous.Pos == "連体詞")
            return true;

        return false;
    }

    private static void AppendSegment(
        StringBuilder result,
        StringBuilder segment)
    {
        if (segment.Length == 0)
            return;

        result.Append("<nobr>");
        result.Append(segment);
        result.Append("</nobr>");
    }

    private static bool IsOpeningSymbol(string value)
    {
        return value is
            "「" or "『" or "（" or "(" or
            "［" or "[" or "【" or "〈" or "《";
    }

    private static bool IsClosingSymbol(string value)
    {
        return value is
            "。" or "、" or "！" or "？" or
            "!" or "?" or "」" or "』" or
            "）" or ")" or "］" or "]" or
            "】" or "〉" or "》";
    }

    public void Dispose()
    {
        _tagger.Dispose();
    }

    private sealed class Token
    {
        public string Surface { get; }
        public string Pos { get; }
        public string Pos1 { get; }

        public Token(
            string surface,
            string pos,
            string pos1)
        {
            Surface = surface;
            Pos = pos;
            Pos1 = pos1;
        }
    }
}