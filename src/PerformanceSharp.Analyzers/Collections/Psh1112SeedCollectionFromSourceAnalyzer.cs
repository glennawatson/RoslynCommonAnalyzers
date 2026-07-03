// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a bulk add into a collection created empty by the immediately preceding statement
/// (PSH1112): <c>var x = new List&lt;T&gt;(); x.AddRange(src);</c> and the
/// <c>HashSet&lt;T&gt;</c>/<c>UnionWith</c> twin. Seeding through the constructor sizes the
/// backing store once from the source's count. Creations that already pass arguments (capacity,
/// comparer, source) or carry an initializer are left alone. When the language is C# 12+ and the
/// <c>performancesharp.prefer_collection_expressions</c> option (default <c>true</c>) is not
/// disabled, the diagnostic carries a property telling the code fix to emit a spread collection
/// expression instead — but only for explicitly typed declarations, since <c>var</c> gives a
/// collection expression no target type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1112SeedCollectionFromSourceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The property key telling the code fix to emit a spread collection expression.</summary>
    internal const string UseCollectionExpressionKey = "UseCollectionExpression";

    /// <summary>The list bulk-add method name.</summary>
    internal const string AddRangeMethodName = "AddRange";

    /// <summary>The set bulk-add method name.</summary>
    internal const string UnionWithMethodName = "UnionWith";

    /// <summary>The editorconfig key for the collection-expression preference.</summary>
    private const string PreferCollectionExpressionsKey = "performancesharp.prefer_collection_expressions";

    /// <summary>The metadata name of the list type.</summary>
    private const string ListMetadataName = "System.Collections.Generic.List`1";

    /// <summary>The metadata name of the hash set type.</summary>
    private const string HashSetMetadataName = "System.Collections.Generic.HashSet`1";

    /// <summary>Cached properties for diagnostics whose fix should emit a collection expression.</summary>
    private static readonly ImmutableDictionary<string, string?> CollectionExpressionProperties =
        ImmutableDictionary<string, string?>.Empty.Add(UseCollectionExpressionKey, "true");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.SeedCollectionFromSource);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var listType = start.Compilation.GetTypeByMetadataName(ListMetadataName);
            var hashSetType = start.Compilation.GetTypeByMetadataName(HashSetMetadataName);
            if (listType is null || hashSetType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, listType, hashSetType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>
    /// Returns the empty-creation shape for a bulk-add invocation: the receiver is a local
    /// declared by the immediately preceding statement with an argument-free, initializer-free
    /// creation, and the source expression does not mention the local.
    /// </summary>
    /// <param name="invocation">The bulk-add invocation.</param>
    /// <param name="declaration">The preceding declaration statement.</param>
    /// <param name="creation">The empty creation initializing the local.</param>
    /// <returns><see langword="true"/> when the syntax-only shape matches.</returns>
    internal static bool TryGetSeedShape(
        InvocationExpressionSyntax invocation,
        out LocalDeclarationStatementSyntax declaration,
        out BaseObjectCreationExpressionSyntax creation)
    {
        declaration = null!;
        creation = null!;

        if (TryGetBulkAddReceiver(invocation) is not { } receiver
            || TryGetPrecedingDeclaration(invocation) is not { } candidate)
        {
            return false;
        }

        var variable = candidate.Declaration.Variables[0];
        if (variable.Identifier.ValueText != receiver.Identifier.ValueText
            || variable.Initializer?.Value is not BaseObjectCreationExpressionSyntax candidateCreation
            || candidateCreation.ArgumentList is { Arguments.Count: > 0 }
            || candidateCreation.Initializer is not null
            || SourceMentionsReceiver(invocation.ArgumentList.Arguments[0].Expression, receiver.Identifier.ValueText))
        {
            return false;
        }

        declaration = candidate;
        creation = candidateCreation;
        return true;
    }

    /// <summary>Returns the receiver local of a single-argument AddRange/UnionWith call.</summary>
    /// <param name="invocation">The candidate bulk-add invocation.</param>
    /// <returns>The receiver identifier, or <see langword="null"/> when the shape does not match.</returns>
    private static IdentifierNameSyntax? TryGetBulkAddReceiver(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 1
            || invocation.Expression is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver } access
            || access.Name.Identifier.ValueText is not (AddRangeMethodName or UnionWithMethodName))
        {
            return null;
        }

        return receiver;
    }

    /// <summary>Returns the single-variable declaration statement immediately preceding the invocation's statement.</summary>
    /// <param name="invocation">The bulk-add invocation.</param>
    /// <returns>The preceding declaration, or <see langword="null"/> when there is none.</returns>
    private static LocalDeclarationStatementSyntax? TryGetPrecedingDeclaration(InvocationExpressionSyntax invocation)
    {
        if (invocation.Parent is not ExpressionStatementSyntax statement
            || statement.Parent is not BlockSyntax block)
        {
            return null;
        }

        var index = block.Statements.IndexOf(statement);
        return index > 0
            && block.Statements[index - 1] is LocalDeclarationStatementSyntax candidate
            && candidate.Declaration.Variables.Count == 1
            ? candidate
            : null;
    }

    /// <summary>Returns whether the source expression mentions the receiver local.</summary>
    /// <param name="source">The bulk-add source expression.</param>
    /// <param name="receiverName">The receiver local's name.</param>
    /// <returns><see langword="true"/> when the local appears inside the source.</returns>
    private static bool SourceMentionsReceiver(ExpressionSyntax source, string receiverName)
    {
        foreach (var token in source.DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText == receiverName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports PSH1112 for a bulk add into a just-created empty list or set.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="listType">The list type definition.</param>
    /// <param name="hashSetType">The hash set type definition.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol listType, INamedTypeSymbol hashSetType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!TryGetSeedShape(invocation, out var declaration, out var creation))
        {
            return;
        }

        var methodName = ((MemberAccessExpressionSyntax)invocation.Expression).Name.Identifier.ValueText;
        var expectedType = methodName == AddRangeMethodName ? listType : hashSetType;
        if (context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is not INamedTypeSymbol createdType
            || !SymbolEqualityComparer.Default.Equals(createdType.OriginalDefinition, expectedType))
        {
            return;
        }

        var useCollectionExpression = declaration.Declaration.Type is not IdentifierNameSyntax { IsVar: true }
            && invocation.SyntaxTree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp12 }
            && !IsCollectionExpressionPreferenceDisabled(context);

        if (useCollectionExpression)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                CollectionRules.SeedCollectionFromSource,
                invocation.SyntaxTree,
                invocation.Span,
                CollectionExpressionProperties,
                createdType.Name,
                methodName));
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.SeedCollectionFromSource,
            Location.Create(invocation.SyntaxTree, invocation.Span),
            createdType.Name,
            methodName));
    }

    /// <summary>Returns whether the collection-expression preference is explicitly disabled.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <returns><see langword="true"/> when the option is set to <c>false</c>.</returns>
    private static bool IsCollectionExpressionPreferenceDisabled(SyntaxNodeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
        return options.TryGetValue(PreferCollectionExpressionsKey, out var value)
            && string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }
}
