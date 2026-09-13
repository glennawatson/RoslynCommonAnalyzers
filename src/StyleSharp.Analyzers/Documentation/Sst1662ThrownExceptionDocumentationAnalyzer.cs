// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a documented member that throws an exception type its documentation does not describe with a
/// matching <c>&lt;exception cref="..."&gt;</c> element (SST1662). Only exceptions constructed and thrown
/// directly in the member body are considered — nothing is followed into called members or into deferred
/// lambda/local-function bodies. The undocumented types, in cref form, are stashed in
/// <see cref="ThrownTypesKey"/> so the code fix can add the missing elements. Off by default.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1662ThrownExceptionDocumentationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic property key holding the newline-separated cref forms of the undocumented thrown types.</summary>
    internal const string ThrownTypesKey = "thrownTypes";

    /// <summary>
    /// Diagnostic property key holding the description for each entry of <see cref="ThrownTypesKey"/>, in the
    /// same order, empty where the throw is unconditional and there is nothing to state.
    /// </summary>
    internal const string ThrownDescriptionsKey = "thrownDescriptions";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(DocumentationRules.ThrownExceptionDocumentation);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.ConversionOperatorDeclaration);
    }

    /// <summary>Reports a documented member missing an <c>&lt;exception&gt;</c> element for a directly-thrown type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var member = (BaseMethodDeclarationSyntax)context.Node;
        var body = (SyntaxNode?)member.Body ?? member.ExpressionBody;
        if (body is null)
        {
            return;
        }

        var documentation = XmlDocumentationHelper.GetDocumentationComment(member);
        if (documentation is null || XmlDocumentationHelper.IsInheritDoc(documentation))
        {
            // An undocumented member is the coverage rules' concern; this rule only augments existing documentation.
            return;
        }

        var thrown = new List<ThrownException>(member.Body?.Statements.Count ?? 1);
        CollectDirectThrows(body, thrown);
        if (thrown.Count == 0)
        {
            return;
        }

        var documented = CollectDocumentedExceptionNames(documentation);
        if (!TrySelectMissing(thrown, documented, out var missing, out var descriptions)
            || MemberName(member) is not { } named)
        {
            return;
        }

        var (nameToken, name) = named;
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(ThrownTypesKey, missing)
            .Add(ThrownDescriptionsKey, descriptions);
        context.ReportDiagnostic(DiagnosticHelper.Create(
            DocumentationRules.ThrownExceptionDocumentation,
            member.SyntaxTree,
            nameToken.Span,
            properties,
            name));
    }

    /// <summary>Collects the object-creation types thrown directly in a member body, skipping deferred scopes.</summary>
    /// <param name="node">The node to scan.</param>
    /// <param name="into">The list receiving each <c>throw new T</c> and what reaches it.</param>
    private static void CollectDirectThrows(SyntaxNode node, List<ThrownException> into)
    {
        foreach (var child in node.ChildNodes())
        {
            if (IsDeferredScope(child))
            {
                // A throw inside a closure or nested function runs in a different context; not "this member throws".
                continue;
            }

            if (ThrownObjectCreationType(child) is { } type)
            {
                into.Add(new(type, DescribeTrigger(child)));
                continue;
            }

            CollectDirectThrows(child, into);
        }
    }

    /// <summary>Describes what has to hold for a throw to be reached.</summary>
    /// <param name="throwNode">The <c>throw</c> statement or expression.</param>
    /// <returns>The documentation text, or an empty string when nothing guards the throw.</returns>
    /// <remarks>
    /// The guard is restated as written rather than turned into prose. It is the condition the caller has to
    /// avoid, so quoting it says exactly what triggers the exception without the fix inventing a sentence;
    /// where nothing guards the throw there is no fact to state and the member is left to its author.
    /// </remarks>
    private static string DescribeTrigger(SyntaxNode throwNode)
    {
        if (throwNode is ThrowExpressionSyntax expression
            && expression.Parent is BinaryExpressionSyntax coalesce
            && coalesce.IsKind(SyntaxKind.CoalesceExpression)
            && coalesce.Right == expression)
        {
            return $"Thrown when <c>{Escape(coalesce.Left)}</c> is <see langword=\"null\"/>.";
        }

        for (var current = throwNode; current is not null; current = current.Parent)
        {
            // The else branch is reached by the condition being false, which this does not describe.
            if (current.Parent is IfStatementSyntax ifStatement && ifStatement.Statement == current)
            {
                return $"Thrown when <c>{Escape(ifStatement.Condition)}</c>.";
            }

            if (current is MemberDeclarationSyntax)
            {
                break;
            }
        }

        return string.Empty;
    }

    /// <summary>Renders an expression as single-line XML character data.</summary>
    /// <param name="expression">The expression as written.</param>
    /// <returns>The expression text, collapsed onto one line and escaped.</returns>
    /// <remarks>
    /// The markup characters a condition can contain would close the element around it, and a line break
    /// would break the record the fix reads back out of the diagnostic.
    /// </remarks>
    private static string Escape(SyntaxNode expression)
    {
        var text = expression.ToString();
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character is ' ' or '\t' or '\r' or '\n')
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                _ = builder.Append(' ');
                pendingSpace = false;
            }

            _ = character switch
            {
                '&' => builder.Append("&amp;"),
                '<' => builder.Append("&lt;"),
                '>' => builder.Append("&gt;"),
                _ => builder.Append(character),
            };
        }

        return builder.ToString();
    }

    /// <summary>Returns whether a node introduces a deferred or nested execution scope whose throws are not the member's.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for a lambda, anonymous method, or local function.</returns>
    private static bool IsDeferredScope(SyntaxNode node) =>
        node is SimpleLambdaExpressionSyntax
            or ParenthesizedLambdaExpressionSyntax
            or AnonymousMethodExpressionSyntax
            or LocalFunctionStatementSyntax;

    /// <summary>Returns the created type of a <c>throw new T(...)</c>, or <see langword="null"/> for anything else.</summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns>The thrown object-creation type, or <see langword="null"/>.</returns>
    private static TypeSyntax? ThrownObjectCreationType(SyntaxNode node) => node switch
    {
        ThrowStatementSyntax { Expression: ObjectCreationExpressionSyntax creation } => creation.Type,
        ThrowExpressionSyntax { Expression: ObjectCreationExpressionSyntax creation } => creation.Type,
        _ => null,
    };

    /// <summary>Collects the simple names documented by the member's top-level <c>&lt;exception&gt;</c> elements.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <returns>The documented exception simple-name slices.</returns>
    private static List<ReadOnlyMemory<char>> CollectDocumentedExceptionNames(DocumentationCommentTriviaSyntax documentation)
    {
        var names = new List<ReadOnlyMemory<char>>();
        foreach (var node in documentation.Content)
        {
            if (XmlDocumentationHelper.GetElementName(node) != "exception")
            {
                continue;
            }

            if (CrefSimpleName(node) is { } simpleName)
            {
                names.Add(simpleName);
            }
        }

        return names;
    }

    /// <summary>Builds the cref forms and descriptions of the thrown types that are not documented.</summary>
    /// <param name="thrown">The thrown exceptions in source order.</param>
    /// <param name="documented">The documented exception simple names.</param>
    /// <param name="missing">The newline-separated cref forms.</param>
    /// <param name="descriptions">The newline-separated descriptions, aligned with <paramref name="missing"/>.</param>
    /// <returns><see langword="true"/> when at least one thrown type is undocumented.</returns>
    private static bool TrySelectMissing(
        List<ThrownException> thrown,
        List<ReadOnlyMemory<char>> documented,
        out string missing,
        out string descriptions)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var typeCapacity = thrown.Count;
        var reasonCapacity = thrown.Count;
        foreach (var exception in thrown)
        {
            typeCapacity += exception.Type.Span.Length;
            reasonCapacity += exception.Description.Length;
        }

        var types = new StringBuilder(typeCapacity);
        var reasons = new StringBuilder(reasonCapacity);
        var found = false;
        foreach (var exception in thrown)
        {
            var simpleName = SimpleName(exception.Type);
            if (simpleName.Length == 0 || IsDocumented(documented, simpleName) || !seen.Add(simpleName))
            {
                continue;
            }

            if (found)
            {
                _ = types.Append('\n');
                _ = reasons.Append('\n');
            }

            _ = types.Append(CrefForm(exception.Type));
            _ = reasons.Append(exception.Description);
            found = true;
        }

        missing = types.ToString();
        descriptions = reasons.ToString();
        return found;
    }

    /// <summary>Compares a thrown type's simple name to the documented name slices.</summary>
    /// <param name="documented">The documented exception names.</param>
    /// <param name="simpleName">The thrown type's simple name.</param>
    /// <returns>Whether a documented name matches ordinally.</returns>
    private static bool IsDocumented(List<ReadOnlyMemory<char>> documented, string simpleName)
    {
        foreach (var name in documented)
        {
            if (name.Span.SequenceEqual(simpleName.AsSpan()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the simple (rightmost, non-generic) name of a type as written.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns>The simple name, or an empty string when the type is not a plain name.</returns>
    private static string SimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        AliasQualifiedNameSyntax alias => SimpleName(alias.Name),
        _ => string.Empty,
    };

    /// <summary>Returns a cref-attribute form of a thrown type, converting generic angle brackets to braces.</summary>
    /// <param name="type">The type syntax as written.</param>
    /// <returns>The cref-safe type text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string CrefForm(TypeSyntax type) =>
        type.ToString().Replace('<', '{').Replace('>', '}');

    /// <summary>Returns the simple name an <c>&lt;exception&gt;</c> element's cref refers to, or <see langword="null"/>.</summary>
    /// <param name="node">The <c>&lt;exception&gt;</c> element.</param>
    /// <returns>The documented type's simple name, or <see langword="null"/>.</returns>
    private static ReadOnlyMemory<char>? CrefSimpleName(XmlNodeSyntax node)
    {
        var attributes = node switch
        {
            XmlElementSyntax element => element.StartTag.Attributes,
            XmlEmptyElementSyntax element => element.Attributes,
            _ => default,
        };

        foreach (var attribute in attributes)
        {
            if (attribute is XmlCrefAttributeSyntax cref)
            {
                return LastNameSegment(cref.Cref.ToString());
            }
        }

        return null;
    }

    /// <summary>Extracts the rightmost identifier from a cref's textual form, dropping any generic or parameter suffix.</summary>
    /// <param name="cref">The cref text.</param>
    /// <returns>The rightmost identifier segment.</returns>
    private static ReadOnlyMemory<char> LastNameSegment(string cref)
    {
        var end = cref.Length;
        for (var i = 0; i < cref.Length; i++)
        {
            if (cref[i] is not ('{' or '(' or '<'))
            {
                continue;
            }

            end = i;
            break;
        }

        var start = 0;
        for (var i = end - 1; i >= 0; i--)
        {
            if (cref[i] is not ('.' or ':'))
            {
                continue;
            }

            start = i + 1;
            break;
        }

        return cref.AsMemory(start, end - start);
    }

    /// <summary>Returns the reported name token and text for a member.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The token to report at and the member name, or <see langword="null"/>.</returns>
    private static (SyntaxToken Token, string Name)? MemberName(BaseMethodDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => (method.Identifier, method.Identifier.ValueText),
        ConstructorDeclarationSyntax constructor => (constructor.Identifier, constructor.Identifier.ValueText),
        OperatorDeclarationSyntax @operator => (@operator.OperatorToken, $"operator {@operator.OperatorToken.ValueText}"),
        ConversionOperatorDeclarationSyntax conversion => (conversion.OperatorKeyword, $"operator {conversion.Type}"),
        _ => null,
    };

    /// <summary>An exception a member throws directly, and what reaches it.</summary>
    /// <param name="Type">The constructed exception type as written.</param>
    /// <param name="Description">The documentation text for the trigger, empty when the throw is unconditional.</param>
    private readonly record struct ThrownException(TypeSyntax Type, string Description);
}
