// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].AsNode() is not { } child || IsDeferredScope(child))
            {
                // A throw inside a closure or nested function runs in a different context; not "this member throws".
                continue;
            }

            if (ThrownObjectCreationType(child) is { } type)
            {
                into.Add(new(type, child));
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
        var names = new List<ReadOnlyMemory<char>>(documentation.Content.Count);
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
        HashSet<string>? seen = null;
        var typeCapacity = thrown.Count;
        foreach (var exception in thrown)
        {
            typeCapacity += exception.Type.Span.Length;
        }

        StringBuilder? types = null;
        StringBuilder? reasons = null;
        foreach (var exception in thrown)
        {
            var simpleName = SimpleName(exception.Type);
            if (simpleName.Length == 0 || IsDocumented(documented, simpleName))
            {
                continue;
            }

            seen ??= new HashSet<string>(StringComparer.Ordinal);
            if (!seen.Add(simpleName))
            {
                continue;
            }

            var description = DescribeTrigger(exception.ThrowNode);
            if (types is null)
            {
                types = new(typeCapacity);
                reasons = new(description.Length);
            }
            else
            {
                _ = types.Append('\n');
                _ = reasons!.Append('\n');
            }

            AppendCrefForm(types, exception.Type);
            _ = reasons!.Append(description);
        }

        if (types is null)
        {
            missing = string.Empty;
            descriptions = string.Empty;
            return false;
        }

        missing = types.ToString();
        descriptions = reasons!.ToString();
        return true;
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

    /// <summary>Appends a thrown type as cref text, converting generic angle brackets to braces.</summary>
    /// <param name="builder">The diagnostic property builder receiving the type text.</param>
    /// <param name="type">The type syntax as written.</param>
    private static void AppendCrefForm(StringBuilder builder, TypeSyntax type)
    {
        var text = type.ToString();
        for (var i = 0; i < text.Length; i++)
        {
            _ = builder.Append(text[i] switch
            {
                '<' => '{',
                '>' => '}',
                var character => character,
            });
        }
    }

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
                return LastNameSegment(cref.Cref);
            }
        }

        return null;
    }

    /// <summary>Reads the last name token before a cref's first generic or parameter suffix without rendering syntax.</summary>
    /// <param name="cref">The cref syntax.</param>
    /// <returns>The written identifier segment, or empty memory when the segment includes other text.</returns>
    private static ReadOnlyMemory<char> LastNameSegment(CrefSyntax cref)
    {
        var state = (Name: default(SyntaxToken), Start: cref.SpanStart, cref.Span.End);
        _ = DescendantTraversalHelper.VisitDescendantTokens(
            cref,
            ref state,
            static (in SyntaxToken token, ref (SyntaxToken Name, int Start, int End) current) =>
            {
                switch (token.Text)
                {
                    case "{" or "(" or "<":
                    {
                        current.End = token.SpanStart;
                        return false;
                    }

                    case "." or "::":
                    {
                        current.Start = token.Span.End;
                        break;
                    }

                    default:
                    {
                        if (token.IsKind(SyntaxKind.IdentifierToken))
                        {
                            current.Name = token;
                        }

                        break;
                    }
                }

                return true;
            });

        return state.Name.SpanStart == state.Start && state.Name.Span.End == state.End
            ? state.Name.Text.AsMemory()
            : ReadOnlyMemory<char>.Empty;
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

    /// <summary>An exception a member throws directly, retaining its syntax until a description is needed.</summary>
    /// <param name="Type">The constructed exception type as written.</param>
    /// <param name="ThrowNode">The throw whose guarding condition supplies a missing exception's description.</param>
    private readonly record struct ThrownException(TypeSyntax Type, SyntaxNode ThrowNode);
}
