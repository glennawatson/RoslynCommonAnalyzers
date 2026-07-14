// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a local that is handed a newly created <see cref="System.IDisposable"/> (or
/// <c>IAsyncDisposable</c>) and never disposes it (SST2410). The method built the thing, kept the
/// only reference to it, and returned without releasing it.
/// </summary>
/// <remarks>
/// <para>
/// This is an ownership check, not a dataflow one, and it is deliberately quiet. A local is reported
/// only when it is created with <c>new</c>, used where it stands, and dropped: every reference to it
/// has to be a plain member access on it (<c>stream.Write(…)</c>, <c>stream.Length</c>). The instant
/// the value goes anywhere else — returned, assigned to a field, a property or another local, passed
/// to any method or constructor, added to a collection, <c>yield return</c>ed, or captured by a
/// lambda or a local function — the rule stops, because the receiver may be the owner now
/// (<c>new StreamReader(stream)</c> owns the stream it wraps).
/// </para>
/// <para>
/// Structs are never reported (disposing a copy is not the caller's business), nor is
/// <see cref="System.Threading.Tasks.Task"/>, which implements the interface but is not meant to be
/// disposed. A <c>using</c> declaration and a <c>using</c> statement are already disposal, and are
/// not touched — choosing between the disposal syntaxes belongs to SST2236 and PSH1310, not here.
/// The code fix (<c>Sst2410DisposableNeverDisposedCodeFixProvider</c>) adds the <c>using</c>.
/// </para>
/// <para>
/// The clean path is syntactic: a local without a <c>using</c> keyword, initialized with an object
/// creation. Nothing binds until both hold.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2410DisposableNeverDisposedAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The synchronous disposal method.</summary>
    private const string DisposeName = "Dispose";

    /// <summary>The asynchronous disposal method.</summary>
    private const string DisposeAsyncName = "DisposeAsync";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.DisposableNeverDisposed);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName("System.IDisposable") is not { } disposable)
            {
                return;
            }

            var types = new DisposableTypes(
                disposable,
                start.Compilation.GetTypeByMetadataName("System.IAsyncDisposable"),
                start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"));

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, types), SyntaxKind.LocalDeclarationStatement);
        });
    }

    /// <summary>Analyzes one local declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The disposal types resolved for this compilation.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, in DisposableTypes types)
    {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;

        // A `using`/`await using` declaration already disposes; a const cannot hold a new object.
        if (!declaration.UsingKeyword.IsKind(SyntaxKind.None)
            || declaration.IsConst
            || GetScope(declaration) is not { } scope)
        {
            return;
        }

        var variables = declaration.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            var variable = variables[i];
            if (variable.Initializer?.Value is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
            {
                AnalyzeVariable(context, types, variable, scope);
            }
        }
    }

    /// <summary>Analyzes one local that is initialized with a newly created object.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The disposal types resolved for this compilation.</param>
    /// <param name="variable">The declarator.</param>
    /// <param name="scope">The block the local lives in.</param>
    private static void AnalyzeVariable(SyntaxNodeAnalysisContext context, in DisposableTypes types, VariableDeclaratorSyntax variable, SyntaxNode scope)
    {
        var creation = variable.Initializer!.Value;
        if (context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is not { } created
            || !types.IsOwnedDisposable(created)
            || context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not ILocalSymbol local)
        {
            return;
        }

        var usage = new UsageScan(context, local, scope);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, UsageScan>(scope, ref usage, VisitReference);
        if (usage.Disposed || usage.Escaped)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.DisposableNeverDisposed,
            variable.Identifier.GetLocation(),
            local.Name));
    }

    /// <summary>Classifies one reference to the local, stopping the walk as soon as the rule must stay quiet.</summary>
    /// <param name="reference">The identifier being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once the local is disposed or has escaped.</returns>
    private static bool VisitReference(IdentifierNameSyntax reference, ref UsageScan state)
    {
        if (reference.Identifier.ValueText != state.Local.Name || !state.IsTheLocal(reference))
        {
            return true;
        }

        if (IsDisposal(reference))
        {
            state.Disposed = true;
            return false;
        }

        // A plain member access keeps the value where it is. A reference inside a lambda or a local
        // function outlives this scan, and anything else hands the value somewhere that may own it.
        if (IsMemberAccessOnLocal(reference) && !IsCaptured(reference, state.Scope))
        {
            return true;
        }

        state.Escaped = true;
        return false;
    }

    /// <summary>Returns whether a reference disposes the local.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns><see langword="true"/> for <c>local.Dispose</c> or <c>local.DisposeAsync</c>.</returns>
    private static bool IsDisposal(IdentifierNameSyntax reference)
        => IsMemberAccessOnLocal(reference)
            && ((MemberAccessExpressionSyntax)reference.Parent!).Name.Identifier.ValueText is DisposeName or DisposeAsyncName;

    /// <summary>Returns whether the local is the receiver of a member access, which does not hand it anywhere.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns><see langword="true"/> when the reference reads a member of the local.</returns>
    private static bool IsMemberAccessOnLocal(IdentifierNameSyntax reference)
        => reference.Parent is MemberAccessExpressionSyntax access
            && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && access.Expression == reference;

    /// <summary>Returns whether a reference sits inside a lambda, an anonymous method, or a local function.</summary>
    /// <param name="reference">The reference.</param>
    /// <param name="scope">The block the local lives in, which bounds the walk.</param>
    /// <returns><see langword="true"/> when the value is captured and can outlive the scan.</returns>
    private static bool IsCaptured(SyntaxNode reference, SyntaxNode scope)
    {
        for (var node = reference.Parent; node is not null && node != scope; node = node.Parent)
        {
            if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the block a local lives in, which is as far as any reference to it can reach.</summary>
    /// <param name="declaration">The local declaration.</param>
    /// <returns>The enclosing block, or <see langword="null"/> when the local is declared somewhere unusual.</returns>
    private static SyntaxNode? GetScope(LocalDeclarationStatementSyntax declaration) => declaration.Parent switch
    {
        BlockSyntax block => block,
        SwitchSectionSyntax section => section,
        _ => null,
    };

    /// <summary>The disposal types of one compilation, resolved once.</summary>
    /// <param name="Disposable">The <see cref="System.IDisposable"/> interface.</param>
    /// <param name="AsyncDisposable">The <c>IAsyncDisposable</c> interface, when the target framework has one.</param>
    /// <param name="Task">The <see cref="System.Threading.Tasks.Task"/> type, which is disposable but must not be disposed.</param>
    private readonly record struct DisposableTypes(
        INamedTypeSymbol Disposable,
        INamedTypeSymbol? AsyncDisposable,
        INamedTypeSymbol? Task)
    {
        /// <summary>Returns whether a created type is one this rule expects the local to dispose.</summary>
        /// <param name="created">The type of the object that was created.</param>
        /// <returns><see langword="true"/> for a disposable reference type that is not a task.</returns>
        public bool IsOwnedDisposable(ITypeSymbol created)
            => !created.IsValueType && Implements(created) && !IsTask(created);

        /// <summary>Returns whether a type implements either disposal interface.</summary>
        /// <param name="created">The created type.</param>
        /// <returns><see langword="true"/> when the type is disposable.</returns>
        private bool Implements(ITypeSymbol created)
        {
            var interfaces = created.AllInterfaces;
            for (var i = 0; i < interfaces.Length; i++)
            {
                var candidate = interfaces[i];
                if (SymbolEqualityComparer.Default.Equals(candidate, Disposable)
                    || (AsyncDisposable is not null && SymbolEqualityComparer.Default.Equals(candidate, AsyncDisposable)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns whether a type is a task, which implements the interface but is not meant to be disposed.</summary>
        /// <param name="created">The created type.</param>
        /// <returns><see langword="true"/> for <c>Task</c> and anything deriving from it, including <c>Task&lt;T&gt;</c>.</returns>
        private bool IsTask(ITypeSymbol created)
        {
            if (Task is null)
            {
                return false;
            }

            for (var type = created; type is not null; type = type.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, Task))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>The state threaded through a local's reference scan.</summary>
    /// <param name="Context">The syntax node context.</param>
    /// <param name="Local">The declared local.</param>
    /// <param name="Scope">The block the local lives in.</param>
    private record struct UsageScan(SyntaxNodeAnalysisContext Context, ILocalSymbol Local, SyntaxNode Scope)
    {
        /// <summary>Gets or sets a value indicating whether the local is disposed.</summary>
        public bool Disposed { get; set; }

        /// <summary>Gets or sets a value indicating whether the value was handed somewhere that may own it.</summary>
        public bool Escaped { get; set; }

        /// <summary>Returns whether a reference really resolves to this local.</summary>
        /// <param name="reference">The reference with a matching name.</param>
        /// <returns><see langword="true"/> when the name is not another symbol's.</returns>
        public readonly bool IsTheLocal(IdentifierNameSyntax reference)
            => SymbolEqualityComparer.Default.Equals(
                Context.SemanticModel.GetSymbolInfo(reference, Context.CancellationToken).Symbol,
                Local);
    }
}
