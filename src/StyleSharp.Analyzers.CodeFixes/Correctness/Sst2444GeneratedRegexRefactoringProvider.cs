// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Formatting;

using RegexEngine = System.Text.RegularExpressions.Regex;
using RegexEngineOptions = System.Text.RegularExpressions.RegexOptions;

namespace StyleSharp.Analyzers;

/// <summary>
/// Offers to convert a valid <c>new Regex("literal")</c> into a source-generated regular expression (SST2444):
/// a partial method carrying the pattern in a <c>[GeneratedRegex]</c> attribute, with the construction replaced
/// by a call to it. The pattern is then validated by the compiler and the matcher is generated ahead of time
/// rather than parsed at run time.
/// </summary>
/// <remarks>
/// The refactoring is offered only where the generated-regex attribute resolves and the language version
/// supports it, and only for the single-literal construction inside a non-generic, non-nested class, struct,
/// or record — the shape the partial method can be added to without a wider rewrite. A pattern that does not
/// parse is never converted; that is the diagnostic's job.
/// </remarks>
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(Sst2444GeneratedRegexRefactoringProvider))]
[Shared]
public sealed class Sst2444GeneratedRegexRefactoringProvider : CodeRefactoringProvider
{
    /// <summary>The simple name of the engine type in a construction.</summary>
    private const string RegexTypeName = "Regex";

    /// <summary>The generated method's return type and the attribute's simple name.</summary>
    private const string GeneratedRegexAttributeName = "GeneratedRegex";

    /// <summary>The base name given to the generated partial method.</summary>
    private const string GeneratedMethodBaseName = "PatternRegex";

    /// <summary>The highest numeric suffix tried when the base method name is taken.</summary>
    private const int MaximumNameSuffixExclusive = 100;

    /// <summary>The metadata name of the engine type.</summary>
    private const string RegexMetadataName = "System.Text.RegularExpressions.Regex";

    /// <summary>The metadata name of the generated-regex attribute.</summary>
    private const string GeneratedRegexAttributeMetadataName = "System.Text.RegularExpressions.GeneratedRegexAttribute";

    /// <summary>A finite construction timeout; matching is never run, so the value is never consulted.</summary>
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root?.FindNode(context.Span) is not { } node
            || node.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>() is not { } creation
            || !IsSingleLiteralConstruction(creation, out var patternLiteral)
            || !SupportsGeneratedRegex(root))
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model is null || !IsEligible(model, creation, patternLiteral!, context.CancellationToken, out var typeDeclaration))
        {
            return;
        }

        var document = context.Document;
        context.RegisterRefactoring(CodeAction.Create(
            "Convert to a source-generated regular expression",
            _ => Task.FromResult(Apply(document, root, creation, typeDeclaration!, patternLiteral!)),
            nameof(Sst2444GeneratedRegexRefactoringProvider)));
    }

    /// <summary>Returns whether a construction is <c>new Regex("literal")</c> with a single string-literal argument.</summary>
    /// <param name="creation">The construction to inspect.</param>
    /// <param name="patternLiteral">The pattern literal when the shape matches.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    private static bool IsSingleLiteralConstruction(ObjectCreationExpressionSyntax creation, out LiteralExpressionSyntax? patternLiteral)
    {
        patternLiteral = null;
        if (GetSimpleName(creation.Type) != RegexTypeName
            || creation.Initializer is not null
            || creation.ArgumentList is not { Arguments.Count: 1 } arguments
            || arguments.Arguments[0].Expression is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal)
        {
            return false;
        }

        patternLiteral = literal;
        return true;
    }

    /// <summary>Returns whether the compilation unit's language version supports a generated regular expression.</summary>
    /// <param name="root">The syntax root.</param>
    /// <returns><see langword="true"/> when the language version is C# 11 or later.</returns>
    private static bool SupportsGeneratedRegex(SyntaxNode root)
        => root.SyntaxTree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp11 };

    /// <summary>Returns whether the construction can be converted, and the type declaration to add the method to.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="creation">The construction being converted.</param>
    /// <param name="patternLiteral">The pattern literal.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="typeDeclaration">The containing type declaration when eligible.</param>
    /// <returns><see langword="true"/> when the construction is eligible.</returns>
    private static bool IsEligible(
        SemanticModel model,
        ObjectCreationExpressionSyntax creation,
        LiteralExpressionSyntax patternLiteral,
        CancellationToken cancellationToken,
        out TypeDeclarationSyntax? typeDeclaration)
    {
        typeDeclaration = null;
        if (model.Compilation.GetTypeByMetadataName(GeneratedRegexAttributeMetadataName) is null
            || model.Compilation.GetTypeByMetadataName(RegexMetadataName) is not { } regexType
            || model.GetSymbolInfo(creation, cancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, regexType))
        {
            return false;
        }

        if (model.GetConstantValue(patternLiteral, cancellationToken) is not { HasValue: true, Value: string pattern } || !IsValidPattern(pattern))
        {
            return false;
        }

        typeDeclaration = FindHostType(creation);
        return typeDeclaration is not null;
    }

    /// <summary>Finds the non-generic, non-nested class, struct, or record the method can be added to.</summary>
    /// <param name="creation">The construction being converted.</param>
    /// <returns>The host type declaration, or <see langword="null"/> when none is eligible.</returns>
    private static TypeDeclarationSyntax? FindHostType(ObjectCreationExpressionSyntax creation)
    {
        if (creation.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } type
            || type is InterfaceDeclarationSyntax
            || type.TypeParameterList is not null
            || type.Parent is TypeDeclarationSyntax)
        {
            return null;
        }

        return type;
    }

    /// <summary>Returns whether a pattern parses under the default engine.</summary>
    /// <param name="pattern">The constant pattern.</param>
    /// <returns><see langword="true"/> when the pattern is valid.</returns>
    private static bool IsValidPattern(string pattern)
    {
        try
        {
            _ = new RegexEngine(pattern, RegexEngineOptions.None, ValidationTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Rewrites the construction as a call and adds the generated-regex partial method.</summary>
    /// <param name="document">The document being refactored.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="creation">The construction being converted.</param>
    /// <param name="typeDeclaration">The host type declaration.</param>
    /// <param name="patternLiteral">The pattern literal.</param>
    /// <returns>The refactored document.</returns>
    private static Document Apply(
        Document document,
        SyntaxNode root,
        ObjectCreationExpressionSyntax creation,
        TypeDeclarationSyntax typeDeclaration,
        LiteralExpressionSyntax patternLiteral)
    {
        var name = CreateMethodName(typeDeclaration);
        var callAnnotation = new SyntaxAnnotation();
        var call = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(name))
            .WithTriviaFrom(creation)
            .WithAdditionalAnnotations(callAnnotation);

        var rootWithCall = root.ReplaceNode(creation, call);
        var host = FindAnnotatedHost(rootWithCall, callAnnotation);
        var method = BuildMethod(name, patternLiteral);
        var updatedHost = EnsurePartial(host.WithMembers(host.Members.Add(method)));
        return document.WithSyntaxRoot(rootWithCall.ReplaceNode(host, updatedHost));
    }

    /// <summary>Finds the type declaration containing the annotated call in the rewritten tree.</summary>
    /// <param name="root">The rewritten syntax root.</param>
    /// <param name="annotation">The annotation marking the call.</param>
    /// <returns>The host type declaration.</returns>
    private static TypeDeclarationSyntax FindAnnotatedHost(SyntaxNode root, SyntaxAnnotation annotation)
    {
        foreach (var node in root.GetAnnotatedNodes(annotation))
        {
            if (node.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } host)
            {
                return host;
            }
        }

        throw new InvalidOperationException("The annotated call is no longer inside a type declaration.");
    }

    /// <summary>Builds the generated-regex partial method carrying the pattern.</summary>
    /// <param name="name">The method name.</param>
    /// <param name="patternLiteral">The pattern literal.</param>
    /// <returns>The method declaration.</returns>
    private static MethodDeclarationSyntax BuildMethod(string name, LiteralExpressionSyntax patternLiteral)
    {
        var attributeArgument = SyntaxFactory.AttributeArgument(patternLiteral.WithoutTrivia());
        var attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(GeneratedRegexAttributeName))
            .WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(attributeArgument)));
        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));

        return SyntaxFactory.MethodDeclaration(SyntaxFactory.IdentifierName(RegexTypeName), SyntaxFactory.Identifier(name))
            .WithAttributeLists(SyntaxFactory.SingletonList(attributeList))
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>Adds the <c>partial</c> modifier to a type declaration that lacks it.</summary>
    /// <param name="type">The host type declaration.</param>
    /// <returns>The type declaration with a <c>partial</c> modifier.</returns>
    private static TypeDeclarationSyntax EnsurePartial(TypeDeclarationSyntax type)
    {
        if (type.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return type;
        }

        if (type.Modifiers.Count == 0)
        {
            var keyword = type.Keyword;
            var partial = SyntaxFactory.Token(SyntaxKind.PartialKeyword)
                .WithLeadingTrivia(keyword.LeadingTrivia)
                .WithTrailingTrivia(SyntaxFactory.Space);
            return type.WithKeyword(keyword.WithLeadingTrivia()).WithModifiers(SyntaxFactory.TokenList(partial));
        }

        var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        return type.WithModifiers(type.Modifiers.Add(partialToken));
    }

    /// <summary>Picks a method name the host type does not already use.</summary>
    /// <param name="type">The host type declaration.</param>
    /// <returns>The method name.</returns>
    private static string CreateMethodName(TypeDeclarationSyntax type)
    {
        var used = CollectMemberNames(type);
        if (!used.Contains(GeneratedMethodBaseName))
        {
            return GeneratedMethodBaseName;
        }

        for (var suffix = 2; suffix < MaximumNameSuffixExclusive; suffix++)
        {
            var candidate = GeneratedMethodBaseName + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        return GeneratedMethodBaseName + type.Identifier.ValueText;
    }

    /// <summary>Collects the member names declared directly in a type.</summary>
    /// <param name="type">The host type declaration.</param>
    /// <returns>The set of declared member names.</returns>
    private static HashSet<string> CollectMemberNames(TypeDeclarationSyntax type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in type.Members)
        {
            if (member is MethodDeclarationSyntax method)
            {
                names.Add(method.Identifier.ValueText);
            }
            else if (member is PropertyDeclarationSyntax property)
            {
                names.Add(property.Identifier.ValueText);
            }
            else if (member is FieldDeclarationSyntax field)
            {
                AddVariableNames(names, field.Declaration.Variables);
            }
        }

        return names;
    }

    /// <summary>Adds each declared variable's name to a set.</summary>
    /// <param name="names">The set to add to.</param>
    /// <param name="variables">The declared variables.</param>
    private static void AddVariableNames(HashSet<string> names, SeparatedSyntaxList<VariableDeclaratorSyntax> variables)
    {
        foreach (var variable in variables)
        {
            names.Add(variable.Identifier.ValueText);
        }
    }

    /// <summary>Returns the rightmost identifier of a written type name.</summary>
    /// <param name="type">The written type syntax.</param>
    /// <returns>The simple name, or <see langword="null"/> when the syntax names no simple type.</returns>
    private static string? GetSimpleName(TypeSyntax type) => type switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetSimpleName(qualified.Right),
        AliasQualifiedNameSyntax alias => GetSimpleName(alias.Name),
        _ => null,
    };
}
