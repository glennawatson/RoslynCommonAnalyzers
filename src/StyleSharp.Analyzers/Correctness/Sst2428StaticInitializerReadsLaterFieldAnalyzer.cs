// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a static field initializer that reads a static field of the same type declared after it (SST2428).
/// </summary>
/// <remarks>
/// <para>
/// Static field initializers run in textual declaration order, so an initializer that reads a later static
/// field of the same type sees that field's default and keeps it. The read is bound only after the syntactic
/// gate has already rejected every field that is not <c>static</c>, is <c>const</c>, or has no initializer —
/// which is nearly all of them — so the clean path never touches the semantic model.
/// </para>
/// <para>
/// When the two fields are declared in different files of the same partial type, the order the initializers
/// run in is undefined rather than merely reversed, so the read is reported no matter which declaration comes
/// first, with a message that says so. Same-file fields are compared by declaration position.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2428StaticInitializerReadsLaterFieldAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The tail of the same-file message: the field runs after this initializer.</summary>
    private const string LaterInSameFileReason = "which is declared later and is still its default at this point";

    /// <summary>The tail of the partial message: the order between files is undefined.</summary>
    private const string UndefinedAcrossPartsReason =
        "which is declared in another file of this partial type, so which initializer runs first is undefined and it may still be its default";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.StaticInitializerReadsLaterField);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    /// <summary>Analyzes one field declaration for an initializer that reads a later static field.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        var modifiers = field.Modifiers;
        if (!ModifierListHelper.Contains(modifiers, SyntaxKind.StaticKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.ConstKeyword))
        {
            return;
        }

        var variables = field.Declaration.Variables;
        INamedTypeSymbol? containingType = null;
        var resolvedType = false;
        for (var i = 0; i < variables.Count; i++)
        {
            var variable = variables[i];
            if (variable.Initializer is not { } initializer)
            {
                continue;
            }

            if (!resolvedType)
            {
                resolvedType = true;
                containingType = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken)?.ContainingType;
            }

            if (containingType is null)
            {
                return;
            }

            var scan = new InitializerScan(context, containingType, variable.Identifier.ValueText, variable.SpanStart, field.SyntaxTree);
            DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, InitializerScan>(initializer.Value, ref scan, VisitIdentifier);
        }
    }

    /// <summary>Reports one identifier when it binds to a later static field of the same type.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="true"/>, so the whole initializer is examined.</returns>
    private static bool VisitIdentifier(IdentifierNameSyntax identifier, ref InitializerScan state)
    {
        if (state.Context.SemanticModel.GetSymbolInfo(identifier, state.Context.CancellationToken).Symbol is not
                IFieldSymbol { IsStatic: true, IsConst: false } referenced
            || !SymbolEqualityComparer.Default.Equals(referenced.ContainingType, state.ContainingType)
            || referenced.Locations.Length == 0)
        {
            return true;
        }

        var location = referenced.Locations[0];
        var acrossParts = !ReferenceEquals(location.SourceTree, state.CurrentTree);
        if (!acrossParts && location.SourceSpan.Start <= state.DeclaratorStart)
        {
            return true;
        }

        state.Context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.StaticInitializerReadsLaterField,
            identifier.GetLocation(),
            state.FieldName,
            referenced.Name,
            acrossParts ? UndefinedAcrossPartsReason : LaterInSameFileReason));
        return true;
    }

    /// <summary>The state threaded through one variable initializer's identifier scan.</summary>
    /// <param name="Context">The syntax node context.</param>
    /// <param name="ContainingType">The type that declares the field being initialized.</param>
    /// <param name="FieldName">The name of the field being initialized.</param>
    /// <param name="DeclaratorStart">The start of the variable declarator being initialized.</param>
    /// <param name="CurrentTree">The syntax tree the initializer lives in.</param>
    private readonly record struct InitializerScan(
        SyntaxNodeAnalysisContext Context,
        INamedTypeSymbol ContainingType,
        string FieldName,
        int DeclaratorStart,
        SyntaxTree CurrentTree);
}
