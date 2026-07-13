// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a local <see cref="System.Text.StringBuilder"/> that is appended to and never read (SST2408). Every
/// <c>Append</c> did real work and then threw it away: the <c>ToString</c> that was supposed to collect it is
/// missing, and nothing at runtime says so.
/// </summary>
/// <remarks>
/// <para>
/// Anything that could look at the contents counts as a read, and the rule leans on that: reading a property,
/// calling anything that is not one of the mutating methods, passing the builder to something, returning it,
/// interpolating it, reassigning it. Only a discarded call to <c>Append</c>, <c>AppendLine</c>,
/// <c>AppendFormat</c>, <c>AppendJoin</c>, <c>Insert</c>, <c>Remove</c>, <c>Replace</c> or <c>Clear</c> — a
/// whole statement whose value nobody takes, chained or not — leaves the builder unread. At least one of them
/// has to be an append, or there is nothing in the builder to have lost.
/// </para>
/// <para>
/// Only locals are examined. A field or a property may be read by any other member of the type, and a
/// parameter's builder belongs to the caller.
/// </para>
/// <para>
/// The clean path is a look at the declared type's name. Nothing binds unless the declaration says
/// <c>StringBuilder</c>, and the search for references only binds the names that already spell the local's.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2408StringBuilderNeverReadAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The type's simple name.</summary>
    private const string StringBuilderName = "StringBuilder";

    /// <summary>The namespace the type lives in.</summary>
    private const string TextNamespace = "Text";

    /// <summary>The namespace containing <see cref="TextNamespace"/>.</summary>
    private const string SystemNamespace = "System";

    /// <summary>The prefix shared by the methods that put something into the builder.</summary>
    private const string AppendPrefix = "Append";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.StringBuilderNeverRead);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LocalDeclarationStatement);
    }

    /// <summary>Analyzes one local declaration.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;
        if (!DeclaresStringBuilder(declaration.Declaration) || GetScope(declaration) is not { } scope)
        {
            return;
        }

        var variables = declaration.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            AnalyzeVariable(context, variables[i], scope);
        }
    }

    /// <summary>Analyzes one declared builder.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="variable">The declarator.</param>
    /// <param name="scope">The block the local lives in.</param>
    private static void AnalyzeVariable(SyntaxNodeAnalysisContext context, VariableDeclaratorSyntax variable, SyntaxNode scope)
    {
        if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not ILocalSymbol local
            || !IsStringBuilder(local.Type))
        {
            return;
        }

        var usage = new UsageScan(context, local);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, UsageScan>(scope, ref usage, VisitReference);
        if (!usage.Appended || usage.Read)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.StringBuilderNeverRead,
            variable.Identifier.GetLocation(),
            local.Name));
    }

    /// <summary>Classifies one reference to the builder.</summary>
    /// <param name="reference">The identifier being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once a read is found, which stops the walk.</returns>
    private static bool VisitReference(IdentifierNameSyntax reference, ref UsageScan state)
    {
        if (reference.Identifier.ValueText != state.Local.Name || !state.IsTheLocal(reference))
        {
            return true;
        }

        if (IsRead(reference))
        {
            state.Read = true;
            return false;
        }

        state.Appended |= IsAppend(reference);
        return true;
    }

    /// <summary>Returns whether a reference could look at what the builder holds.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns><see langword="true"/> for anything other than a discarded mutation.</returns>
    private static bool IsRead(IdentifierNameSyntax reference)
    {
        ExpressionSyntax current = reference;
        while (current.Parent is MemberAccessExpressionSyntax access && access.Expression == current)
        {
            if (access.Parent is not InvocationExpressionSyntax invocation || !IsMutator(access.Name.Identifier.ValueText))
            {
                return true;
            }

            current = invocation;
        }

        if (current.Parent is AssignmentExpressionSyntax assignment
            && assignment.Left == current
            && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            return false;
        }

        return current.Parent is not ExpressionStatementSyntax;
    }

    /// <summary>Returns whether a reference starts a discarded append.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns><see langword="true"/> when the statement puts something into the builder.</returns>
    private static bool IsAppend(IdentifierNameSyntax reference)
        => reference.Parent is MemberAccessExpressionSyntax access
            && access.Expression == reference
            && access.Name.Identifier.ValueText.StartsWith(AppendPrefix, StringComparison.Ordinal);

    /// <summary>Returns whether a method changes the builder without reporting anything about it.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> for a method whose only useful result is the builder itself.</returns>
    private static bool IsMutator(string name) => name is "Append"
        or "AppendLine"
        or "AppendFormat"
        or "AppendJoin"
        or "Insert"
        or "Remove"
        or "Replace"
        or "Clear";

    /// <summary>Returns whether a declaration is written as a builder.</summary>
    /// <param name="declaration">The variable declaration.</param>
    /// <returns><see langword="true"/> when the declared type, or the created one, is named <c>StringBuilder</c>.</returns>
    private static bool DeclaresStringBuilder(VariableDeclarationSyntax declaration)
        => NamesStringBuilder(declaration.Type) || (declaration.Type.IsVar && CreatesStringBuilder(declaration));

    /// <summary>Returns whether an implicitly typed declaration is initialized with a new builder.</summary>
    /// <param name="declaration">The variable declaration.</param>
    /// <returns><see langword="true"/> when a declarator is initialized with <c>new StringBuilder(…)</c>.</returns>
    private static bool CreatesStringBuilder(VariableDeclarationSyntax declaration)
    {
        var variables = declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            if (variables[i].Initializer?.Value is ObjectCreationExpressionSyntax creation && NamesStringBuilder(creation.Type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is written with the builder's simple name.</summary>
    /// <param name="type">The type as written.</param>
    /// <returns><see langword="true"/> when the rightmost name matches.</returns>
    private static bool NamesStringBuilder(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == StringBuilderName,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == StringBuilderName,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText == StringBuilderName,
        _ => false,
    };

    /// <summary>Returns whether a bound type is <see cref="System.Text.StringBuilder"/>.</summary>
    /// <param name="type">The local's type.</param>
    /// <returns><see langword="true"/> for the framework's builder, and nothing else that shares its name.</returns>
    private static bool IsStringBuilder(ITypeSymbol type)
        => type is INamedTypeSymbol { Name: StringBuilderName, ContainingNamespace: { Name: TextNamespace } text }
            && text.ContainingNamespace is { Name: SystemNamespace } system
            && system.ContainingNamespace is { IsGlobalNamespace: true };

    /// <summary>Gets the block a local lives in, which is as far as any reference to it can reach.</summary>
    /// <param name="declaration">The local declaration.</param>
    /// <returns>The enclosing block, or <see langword="null"/> when the local is declared somewhere unusual.</returns>
    private static SyntaxNode? GetScope(LocalDeclarationStatementSyntax declaration) => declaration.Parent switch
    {
        BlockSyntax block => block,
        SwitchSectionSyntax section => section,
        _ => null,
    };

    /// <summary>The state threaded through a builder's reference scan.</summary>
    /// <param name="Context">The syntax node context.</param>
    /// <param name="Local">The declared builder.</param>
    private record struct UsageScan(SyntaxNodeAnalysisContext Context, ILocalSymbol Local)
    {
        /// <summary>Gets or sets a value indicating whether anything was appended to the builder.</summary>
        public bool Appended { get; set; }

        /// <summary>Gets or sets a value indicating whether anything could read the builder.</summary>
        public bool Read { get; set; }

        /// <summary>Returns whether a reference really resolves to this builder.</summary>
        /// <param name="reference">The reference with a matching name.</param>
        /// <returns><see langword="true"/> when the name is not another symbol's.</returns>
        public readonly bool IsTheLocal(IdentifierNameSyntax reference)
            => SymbolEqualityComparer.Default.Equals(
                Context.SemanticModel.GetSymbolInfo(reference, Context.CancellationToken).Symbol,
                Local);
    }
}
