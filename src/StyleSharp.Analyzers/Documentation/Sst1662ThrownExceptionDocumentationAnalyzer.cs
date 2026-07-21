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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DocumentationRules.ThrownExceptionDocumentation);

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

        var thrown = new List<TypeSyntax>();
        CollectDirectThrows(body, thrown);
        if (thrown.Count == 0)
        {
            return;
        }

        var documented = CollectDocumentedExceptionNames(documentation);
        var missing = SelectMissing(thrown, documented);
        if (missing is null || MemberName(member) is not { } named)
        {
            return;
        }

        var (nameToken, name) = named;
        var properties = ImmutableDictionary<string, string?>.Empty.Add(ThrownTypesKey, missing);
        context.ReportDiagnostic(DiagnosticHelper.Create(
            DocumentationRules.ThrownExceptionDocumentation,
            member.SyntaxTree,
            nameToken.Span,
            properties,
            name));
    }

    /// <summary>Collects the object-creation types thrown directly in a member body, skipping deferred scopes.</summary>
    /// <param name="node">The node to scan.</param>
    /// <param name="into">The list receiving each <c>throw new T</c> type.</param>
    private static void CollectDirectThrows(SyntaxNode node, List<TypeSyntax> into)
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
                into.Add(type);
                continue;
            }

            CollectDirectThrows(child, into);
        }
    }

    /// <summary>Returns whether a node introduces a deferred or nested execution scope whose throws are not the member's.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for a lambda, anonymous method, or local function.</returns>
    private static bool IsDeferredScope(SyntaxNode node)
        => node is SimpleLambdaExpressionSyntax
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
    /// <returns>The set of documented exception simple names.</returns>
    private static HashSet<string> CollectDocumentedExceptionNames(DocumentationCommentTriviaSyntax documentation)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
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

    /// <summary>Builds the newline-separated cref forms of the thrown types that are not documented, or <see langword="null"/> when all are.</summary>
    /// <param name="thrown">The thrown types in source order.</param>
    /// <param name="documented">The documented exception simple names.</param>
    /// <returns>The joined cref forms, or <see langword="null"/> when nothing is missing.</returns>
    private static string? SelectMissing(List<TypeSyntax> thrown, HashSet<string> documented)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        StringBuilder? builder = null;
        foreach (var type in thrown)
        {
            var simpleName = SimpleName(type);
            if (simpleName.Length == 0 || documented.Contains(simpleName) || !seen.Add(simpleName))
            {
                continue;
            }

            builder ??= new StringBuilder();
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(CrefForm(type));
        }

        return builder?.ToString();
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
    private static string CrefForm(TypeSyntax type)
        => type.ToString().Replace('<', '{').Replace('>', '}');

    /// <summary>Returns the simple name an <c>&lt;exception&gt;</c> element's cref refers to, or <see langword="null"/>.</summary>
    /// <param name="node">The <c>&lt;exception&gt;</c> element.</param>
    /// <returns>The documented type's simple name, or <see langword="null"/>.</returns>
    private static string? CrefSimpleName(XmlNodeSyntax node)
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
    private static string LastNameSegment(string cref)
    {
        var end = cref.Length;
        for (var i = 0; i < cref.Length; i++)
        {
            if (cref[i] is '{' or '(' or '<')
            {
                end = i;
                break;
            }
        }

        var start = 0;
        for (var i = end - 1; i >= 0; i--)
        {
            if (cref[i] is '.' or ':')
            {
                start = i + 1;
                break;
            }
        }

        return cref.Substring(start, end - start);
    }

    /// <summary>Returns the reported name token and text for a member.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The token to report at and the member name, or <see langword="null"/>.</returns>
    private static (SyntaxToken Token, string Name)? MemberName(BaseMethodDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => (method.Identifier, method.Identifier.ValueText),
        ConstructorDeclarationSyntax constructor => (constructor.Identifier, constructor.Identifier.ValueText),
        OperatorDeclarationSyntax @operator => (@operator.OperatorToken, "operator " + @operator.OperatorToken.ValueText),
        ConversionOperatorDeclarationSyntax conversion => (conversion.OperatorKeyword, "operator " + conversion.Type),
        _ => null,
    };
}
