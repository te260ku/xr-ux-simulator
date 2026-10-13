using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class CSharpIdValidator
{
    private static readonly Regex Identifier = new Regex(
        @"\A[_\p{L}\p{Nl}][_\p{L}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]*\z",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> Keywords = new HashSet<string>(
        ("abstract as base bool break byte case catch char checked class const continue " +
         "decimal default delegate do double else enum event explicit extern false finally fixed " +
         "float for foreach goto if implicit in int interface internal is lock long namespace new " +
         "null object operator out override params private protected public readonly ref return " +
         "sbyte sealed short sizeof stackalloc static string struct switch this throw true try " +
         "typeof uint ulong unchecked unsafe ushort using virtual void volatile while " +
         "__arglist __makeref __reftype __refvalue").Split(' '),
        StringComparer.Ordinal);

    public static void Validate(IReadOnlyList<string> ids, string targetName)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (!Identifier.IsMatch(id) || Keywords.Contains(id))
                throw new FormatException($"Invalid C# field identifier: '{id}'. The ID is not renamed.");
            if (id == targetName)
                throw new FormatException($"ID '{id}' cannot equal the containing class name.");
            if (!seen.Add(id)) throw new FormatException($"Duplicate ID: '{id}'.");
        }
    }
}
