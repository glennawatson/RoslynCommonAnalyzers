// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags lookups on string-keyed collections whose key argument is materialized from a char
/// span — <c>span.ToString()</c> or <c>new string(span)</c> — just to probe (PSH1116).
/// <c>GetAlternateLookup&lt;ReadOnlySpan&lt;char&gt;&gt;</c> (.NET 9+) probes with the span
/// directly and allocates nothing. Reported only when the receiver's type actually exposes
/// GetAlternateLookup, which also gates the whole rule on the runtime having the API. The
/// collection's comparer must support alternate lookups — the ordinal defaults do — which is
/// why this stays a report-only advisory.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1116AlternateLookupAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The lookup member names the syntax gate accepts.</summary>
    internal const string ContainsKeyMethodName = "ContainsKey";

    /// <summary>The TryGetValue member name.</summary>
    internal const string TryGetValueMethodName = "TryGetValue";

    /// <summary>The Contains member name.</summary>
    internal const string ContainsMethodName = "Contains";

    /// <summary>The Remove member name.</summary>
    internal const string RemoveMethodName = "Remove";

    /// <summary>The alternate lookup member the receiver must expose.</summary>
    private const string GetAlternateLookupMethodName = "GetAlternateLookup";

    /// <summary>The metadata name of the dictionary type used as the compilation gate.</summary>
    private const string DictionaryMetadataName = "System.Collections.Generic.Dictionary`2";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.UseAlternateLookup);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(DictionaryMetadataName) is not { } dictionaryType
                || dictionaryType.GetMembers(GetAlternateLookupMethodName).IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1116 for a probe whose key is materialized from a char span.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count == 0
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText
                is not (ContainsKeyMethodName or TryGetValueMethodName or ContainsMethodName or RemoveMethodName))
        {
            return;
        }

        var key = invocation.ArgumentList.Arguments[0].Expression;
        if (TryGetMaterialization(key) is not { } materialization
            || !IsCharSpan(context, materialization.Source)
            || !ReceiverSupportsAlternateLookup(context, access.Expression))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseAlternateLookup,
            key.SyntaxTree,
            key.Span,
            materialization.Description));
    }

    /// <summary>Returns the span source and description of a key-materializing expression, before any binding.</summary>
    /// <param name="key">The key argument expression.</param>
    /// <returns>The materialization parts, or <see langword="null"/> when the shape does not match.</returns>
    private static (ExpressionSyntax Source, string Description)? TryGetMaterialization(ExpressionSyntax key)
    {
        if (key is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } toString
            && toString.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: nameof(ToString) } access)
        {
            return (access.Expression, nameof(ToString));
        }

        return key is ObjectCreationExpressionSyntax { ArgumentList.Arguments: [var single] } creation
            && creation.Type is PredefinedTypeSyntax predefined
            && predefined.Keyword.IsKind(SyntaxKind.StringKeyword)
            ? (single.Expression, "new string")
            : null;
    }

    /// <summary>Returns whether an expression's type is a char span.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The span source expression.</param>
    /// <returns><see langword="true"/> for <c>Span&lt;char&gt;</c> and <c>ReadOnlySpan&lt;char&gt;</c>.</returns>
    private static bool IsCharSpan(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        => context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type is INamedTypeSymbol
        {
            Name: "Span" or "ReadOnlySpan",
            IsGenericType: true,
            TypeArguments: [{ SpecialType: SpecialType.System_Char }],
            ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
        };

    /// <summary>Returns whether the probe receiver is a string-keyed collection exposing GetAlternateLookup.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="receiver">The receiver expression.</param>
    /// <returns><see langword="true"/> when the alternate lookup applies.</returns>
    private static bool ReceiverSupportsAlternateLookup(SyntaxNodeAnalysisContext context, ExpressionSyntax receiver)
    {
        if (context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken).Type
            is not INamedTypeSymbol { IsGenericType: true } receiverType
            || receiverType.TypeArguments[0].SpecialType != SpecialType.System_String)
        {
            return false;
        }

        return !receiverType.OriginalDefinition.GetMembers(GetAlternateLookupMethodName).IsEmpty;
    }
}
