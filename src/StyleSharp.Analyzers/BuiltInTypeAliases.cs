// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Maps the framework primitive types to their C# keyword aliases (for SST1121). Only the classic
/// fifteen aliases are handled; <c>IntPtr</c>/<c>UIntPtr</c> are intentionally excluded because the
/// <c>nint</c>/<c>nuint</c> keywords are not an unconditional substitute.
/// </summary>
internal static class BuiltInTypeAliases
{
    /// <summary>The keyword alias for each aliased special type.</summary>
    private static readonly Dictionary<SpecialType, string> KeywordBySpecialType = new()
    {
        [SpecialType.System_Boolean] = "bool",
        [SpecialType.System_Byte] = "byte",
        [SpecialType.System_SByte] = "sbyte",
        [SpecialType.System_Char] = "char",
        [SpecialType.System_Decimal] = "decimal",
        [SpecialType.System_Double] = "double",
        [SpecialType.System_Single] = "float",
        [SpecialType.System_Int16] = "short",
        [SpecialType.System_UInt16] = "ushort",
        [SpecialType.System_Int32] = "int",
        [SpecialType.System_UInt32] = "uint",
        [SpecialType.System_Int64] = "long",
        [SpecialType.System_UInt64] = "ulong",
        [SpecialType.System_Object] = "object",
        [SpecialType.System_String] = "string"
    };

    /// <summary>The predefined-type token kind for each keyword alias.</summary>
    private static readonly Dictionary<string, SyntaxKind> TokenKindByKeyword = new()
    {
        ["bool"] = SyntaxKind.BoolKeyword,
        ["byte"] = SyntaxKind.ByteKeyword,
        ["sbyte"] = SyntaxKind.SByteKeyword,
        ["char"] = SyntaxKind.CharKeyword,
        ["decimal"] = SyntaxKind.DecimalKeyword,
        ["double"] = SyntaxKind.DoubleKeyword,
        ["float"] = SyntaxKind.FloatKeyword,
        ["short"] = SyntaxKind.ShortKeyword,
        ["ushort"] = SyntaxKind.UShortKeyword,
        ["int"] = SyntaxKind.IntKeyword,
        ["uint"] = SyntaxKind.UIntKeyword,
        ["long"] = SyntaxKind.LongKeyword,
        ["ulong"] = SyntaxKind.ULongKeyword,
        ["object"] = SyntaxKind.ObjectKeyword,
        ["string"] = SyntaxKind.StringKeyword
    };

    /// <summary>The simple framework type names that have a keyword alias, used as a cheap pre-filter.</summary>
    private static readonly HashSet<string> Names =
    [
        "Boolean",
        "Byte",
        "SByte",
        "Char",
        "Decimal",
        "Double",
        "Single",
        "Int16",
        "UInt16",
        "Int32",
        "UInt32",
        "Int64",
        "UInt64",
        "Object",
        "String"
    ];

    /// <summary>Returns whether the simple type name has a keyword alias (a cheap pre-filter before semantic checks).</summary>
    /// <param name="name">The simple type name.</param>
    /// <returns><see langword="true"/> when the name is an aliased framework primitive.</returns>
    public static bool IsAliasedName(string name) => Names.Contains(name);

    /// <summary>Returns the keyword alias for a special type, or <see langword="null"/> when none applies.</summary>
    /// <param name="specialType">The special type.</param>
    /// <returns>The keyword alias (for example <c>int</c>), or <see langword="null"/>.</returns>
    public static string? Keyword(SpecialType specialType)
        => KeywordBySpecialType.TryGetValue(specialType, out var keyword) ? keyword : null;

    /// <summary>Returns the predefined-type token kind for a keyword alias.</summary>
    /// <param name="keyword">The keyword alias.</param>
    /// <returns>The matching predefined-type <see cref="SyntaxKind"/>.</returns>
    public static SyntaxKind TokenKind(string keyword) => TokenKindByKeyword[keyword];
}
