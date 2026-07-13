// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>[DebuggerDisplay]</c> whose <c>{Member}</c> expression names something the type does not have
/// (SST2405). The debugger, not the compiler, resolves that string, so a typo or a renamed member survives
/// the build and only shows up as an error in the watch window.
/// </summary>
/// <remarks>
/// <para>
/// Only an expression the rule can be sure about is checked: a bare member name, optionally with a format
/// specifier (<c>{Name,nq}</c>) and optionally a call (<c>{Describe(),nq}</c>). Anything else — a chain, an
/// indexer, arithmetic — is left alone, because a name inside it may be resolved against something other than
/// this type, and a diagnostic that is only probably right is worse than none.
/// </para>
/// <para>
/// Members are looked up on the type and every base type, so a display string naming an inherited property is
/// correct and stays silent. The attribute is only checked on a type declaration; on a field or a property it
/// resolves against that member's type, which is a different question.
/// </para>
/// <para>
/// The clean path is one string comparison against the attribute's name.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2405DebuggerDisplayNamesMissingMemberAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The attribute's simple name, as it appears in the message.</summary>
    private const string DebuggerDisplayName = "DebuggerDisplay";

    /// <summary>The attribute's full type name.</summary>
    private const string DebuggerDisplayAttributeName = "DebuggerDisplayAttribute";

    /// <summary>The length of the empty call parentheses a display expression may end with.</summary>
    private const int CallParenthesesLength = 2;

    /// <summary>The length of a backslash escape in a display string.</summary>
    private const int EscapeSequenceLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.DebuggerDisplayNamesMissingMember);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    /// <summary>Analyzes one attribute.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (!IsDebuggerDisplayName(attribute.Name)
            || GetDisplayLiteral(attribute) is not { } literal
            || GetTargetType(context, attribute) is not { } type)
        {
            return;
        }

        ReportMissingMembers(context, literal, type);
    }

    /// <summary>Reports every member the display string names that the type does not have.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="literal">The display string.</param>
    /// <param name="type">The type the attribute is on.</param>
    private static void ReportMissingMembers(SyntaxNodeAnalysisContext context, LiteralExpressionSyntax literal, INamedTypeSymbol type)
    {
        var text = literal.Token.Text;
        var start = literal.Token.SpanStart;
        var index = 0;
        while (TryReadNextMemberName(text, ref index, out var name, out var offset))
        {
            if (DeclaresMember(type, name!))
            {
                continue;
            }

            var span = new Microsoft.CodeAnalysis.Text.TextSpan(start + offset, name!.Length);
            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.DebuggerDisplayNamesMissingMember,
                Location.Create(literal.SyntaxTree, span),
                DebuggerDisplayName,
                name!,
                type.Name));
        }
    }

    /// <summary>Reads the next <c>{…}</c> expression that is a plain member name.</summary>
    /// <param name="text">The display string's source text, quotes and all.</param>
    /// <param name="index">The scan position, advanced past the expression that was read.</param>
    /// <param name="name">The member name when one is read.</param>
    /// <param name="offset">The name's offset into the literal's text.</param>
    /// <returns><see langword="true"/> when a name was read; <see langword="false"/> at the end of the string.</returns>
    /// <remarks>
    /// The token's own text is scanned rather than its value, so the offset of a name inside it is also its
    /// offset in the file — which is how the diagnostic lands on the member name itself and not on the whole
    /// string. A backslash escapes the character after it, which is how a display string writes a literal
    /// brace.
    /// </remarks>
    private static bool TryReadNextMemberName(string text, ref int index, out string? name, out int offset)
    {
        while (index < text.Length)
        {
            if (text[index] == '\\')
            {
                index += EscapeSequenceLength;
                continue;
            }

            if (text[index] != '{')
            {
                index++;
                continue;
            }

            var open = index + 1;
            var close = text.IndexOf('}', open);
            if (close < 0)
            {
                break;
            }

            index = close + 1;
            if (TryReadMemberName(text, open, close, out name, out offset))
            {
                return true;
            }
        }

        name = null;
        offset = 0;
        return false;
    }

    /// <summary>Reads a member name out of one brace-delimited expression.</summary>
    /// <param name="text">The display string's source text.</param>
    /// <param name="open">The position after the opening brace.</param>
    /// <param name="close">The position of the closing brace.</param>
    /// <param name="name">The member name when the expression is one.</param>
    /// <param name="offset">The name's offset into the literal's text.</param>
    /// <returns><see langword="true"/> when the expression is a plain member name.</returns>
    private static bool TryReadMemberName(string text, int open, int close, out string? name, out int offset)
    {
        name = null;
        offset = 0;
        var start = open;
        var end = GetExpressionEnd(text, open, close);
        Trim(text, ref start, ref end);
        end = StripCall(text, start, end);
        if (end <= start || !IsIdentifier(text, start, end))
        {
            return false;
        }

        name = text.Substring(start, end - start);
        offset = start;
        return true;
    }

    /// <summary>Gets the end of the expression, which a format specifier may cut short.</summary>
    /// <param name="text">The display string's source text.</param>
    /// <param name="open">The position after the opening brace.</param>
    /// <param name="close">The position of the closing brace.</param>
    /// <returns>The position after the last character of the expression.</returns>
    private static int GetExpressionEnd(string text, int open, int close)
    {
        var comma = text.IndexOf(',', open);
        return comma < 0 || comma > close ? close : comma;
    }

    /// <summary>Trims the spaces around an expression.</summary>
    /// <param name="text">The display string's source text.</param>
    /// <param name="start">The first position, advanced past leading spaces.</param>
    /// <param name="end">The position after the last, pulled back past trailing spaces.</param>
    private static void Trim(string text, ref int start, ref int end)
    {
        while (start < end && text[start] == ' ')
        {
            start++;
        }

        while (end > start && text[end - 1] == ' ')
        {
            end--;
        }
    }

    /// <summary>Drops the empty parentheses of a called member.</summary>
    /// <param name="text">The display string's source text.</param>
    /// <param name="start">The first position of the expression.</param>
    /// <param name="end">The position after the last.</param>
    /// <returns>The position after the member's name.</returns>
    private static int StripCall(string text, int start, int end)
        => end - start > CallParenthesesLength && text[end - 1] == ')' && text[end - CallParenthesesLength] == '('
            ? end - CallParenthesesLength
            : end;

    /// <summary>Returns whether a stretch of the display string is a bare identifier.</summary>
    /// <param name="text">The display string's source text.</param>
    /// <param name="start">The first position of the candidate.</param>
    /// <param name="end">The position after the last.</param>
    /// <returns><see langword="true"/> when every character can appear in an identifier.</returns>
    private static bool IsIdentifier(string text, int start, int end)
    {
        if (char.IsDigit(text[start]))
        {
            return false;
        }

        for (var i = start; i < end; i++)
        {
            if (!char.IsLetterOrDigit(text[i]) && text[i] != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a type, or anything it inherits from, declares a member with this name.</summary>
    /// <param name="type">The type the attribute is on.</param>
    /// <param name="name">The member name.</param>
    /// <returns><see langword="true"/> when the debugger will find the member.</returns>
    private static bool DeclaresMember(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(name).Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the type an attribute is applied to.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="attribute">The attribute.</param>
    /// <returns>The type, or <see langword="null"/> when the attribute is not on a type declaration.</returns>
    private static INamedTypeSymbol? GetTargetType(SyntaxNodeAnalysisContext context, AttributeSyntax attribute)
    {
        if (attribute.Parent?.Parent is not TypeDeclarationSyntax declaration)
        {
            return null;
        }

        return context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
    }

    /// <summary>Gets the display string an attribute is constructed with.</summary>
    /// <param name="attribute">The attribute.</param>
    /// <returns>The literal, or <see langword="null"/> when the format is not a literal string.</returns>
    private static LiteralExpressionSyntax? GetDisplayLiteral(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is not { Arguments.Count: > 0 } list || list.Arguments[0].NameEquals is not null)
        {
            return null;
        }

        return list.Arguments[0].Expression as LiteralExpressionSyntax;
    }

    /// <summary>Returns whether an attribute is written as the debugger-display one.</summary>
    /// <param name="name">The attribute's name.</param>
    /// <returns><see langword="true"/> when the rightmost name matches, with or without the suffix.</returns>
    private static bool IsDebuggerDisplayName(NameSyntax name)
        => GetSimpleName(name) is DebuggerDisplayName or DebuggerDisplayAttributeName;

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased name.</summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The simple name, or an empty string.</returns>
    private static string GetSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };
}
