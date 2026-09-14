// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>Stopwatch.StartNew()</c> locals whose only use is reading elapsed time (PSH1408),
/// where <c>Stopwatch.GetTimestamp()</c> plus <c>Stopwatch.GetElapsedTime(...)</c> measures the
/// same interval without the allocation. The whole rule is gated on <c>GetElapsedTime</c>
/// existing in the compilation (.NET 7+). The local's usages are scanned with a whitelist —
/// <c>Elapsed</c>, <c>ElapsedMilliseconds</c>, <c>ElapsedTicks</c> reads and <c>Stop()</c>
/// calls — and any other mention (Restart, Reset, IsRunning, escaping as an argument or return
/// value) keeps the declaration clean.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1408UseStopwatchTimestampsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The factory method name the syntax gate requires.</summary>
    private const string StartNewMethodName = "StartNew";

    /// <summary>The receiver type name the syntax gate requires.</summary>
    private const string StopwatchTypeName = "Stopwatch";

    /// <summary>The member whose presence gates the rule to .NET 7+.</summary>
    private const string GetElapsedTimeMethodName = "GetElapsedTime";

    /// <summary>The metadata name of the stopwatch type.</summary>
    private const string StopwatchMetadataName = "System.Diagnostics.Stopwatch";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ApiSelectionRules.UseStopwatchTimestamps);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, StopwatchMetadataName),
            AnalyzeDeclaration,
            SyntaxKind.LocalDeclarationStatement);
    }

    /// <summary>Reports PSH1408 for a StartNew local used only to read elapsed time.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation's deferred stopwatch type.</param>
    private static void AnalyzeDeclaration(in SyntaxNodeAnalysisContext context, LazyMetadataType types)
    {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;
        if (declaration.Declaration.Variables.Count != 1
            || declaration.Declaration.Variables[0].Initializer?.Value is not InvocationExpressionSyntax initializer
            || !IsStopwatchStartNewShape(initializer)
            || EnclosingFunction.GetBody(declaration) is not { } body)
        {
            return;
        }

        var variable = declaration.Declaration.Variables[0];
        var scan = new UsageScan(variable.Identifier.ValueText, variable.Identifier.SpanStart);
        _ = DescendantTraversalHelper.VisitDescendantTokens(body, ref scan, static (in SyntaxToken token, ref UsageScan state) => state.Visit(in token));
        if (!scan.OnlyElapsedReads || scan.FirstElapsedMember is not { } elapsedMember)
        {
            return;
        }

        if (!IsSupportedStopwatchCall(context, initializer, types))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.UseStopwatchTimestamps,
            initializer.SyntaxTree,
            initializer.Span,
            elapsedMember));
    }

    /// <summary>Checks that the candidate binds to Stopwatch on a framework with GetElapsedTime.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="initializer">The candidate StartNew call.</param>
    /// <param name="types">The compilation's deferred stopwatch type.</param>
    /// <returns>Whether the framework supports replacing this call with timestamps.</returns>
    private static bool IsSupportedStopwatchCall(in SyntaxNodeAnalysisContext context, InvocationExpressionSyntax initializer, LazyMetadataType types) =>
        types.Get() is { } stopwatchType
        && !stopwatchType.GetMembers(GetElapsedTimeMethodName).IsEmpty
        && context.SemanticModel.GetSymbolInfo(initializer, context.CancellationToken).Symbol is IMethodSymbol method
        && SymbolEqualityComparer.Default.Equals(method.ContainingType, stopwatchType);

    /// <summary>Returns whether an invocation has the <c>Stopwatch.StartNew()</c> syntax shape.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches, before any binding.</returns>
    private static bool IsStopwatchStartNewShape(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Count == 0
            && TypeNameReceiver.IsCallOnTypeName(invocation, StartNewMethodName, StopwatchTypeName);

    /// <summary>Token-visitor state that whitelists elapsed reads and Stop calls on one local.</summary>
    private struct UsageScan
    {
        /// <summary>The local's name.</summary>
        private readonly string _name;

        /// <summary>The declarator identifier's position, excluded from the scan.</summary>
        private readonly int _declaratorStart;

        /// <summary>Initializes a new instance of the <see cref="UsageScan"/> struct.</summary>
        /// <param name="name">The local's name.</param>
        /// <param name="declaratorStart">The declarator identifier's position.</param>
        public UsageScan(string name, int declaratorStart)
        {
            _name = name;
            _declaratorStart = declaratorStart;
            OnlyElapsedReads = true;
        }

        /// <summary>Gets a value indicating whether every usage seen so far is whitelisted.</summary>
        public bool OnlyElapsedReads { get; private set; }

        /// <summary>Gets the first elapsed member read, for the diagnostic message.</summary>
        public string? FirstElapsedMember { get; private set; }

        /// <summary>Classifies one token; stops the walk on the first non-whitelisted usage.</summary>
        /// <param name="token">The token to inspect.</param>
        /// <returns><see langword="true"/> to keep walking.</returns>
        public bool Visit(in SyntaxToken token)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken)
                || token.SpanStart == _declaratorStart
                || token.ValueText != _name
                || token.Parent is not IdentifierNameSyntax identifier)
            {
                return true;
            }

            if (IsWhitelistedUse(identifier, out var elapsedMember))
            {
                FirstElapsedMember ??= elapsedMember;
                return true;
            }

            OnlyElapsedReads = false;
            return false;
        }

        /// <summary>Returns whether an identifier occurrence is a whitelisted elapsed read or Stop call.</summary>
        /// <param name="identifier">The identifier occurrence.</param>
        /// <param name="elapsedMember">The elapsed member read, when the usage is one.</param>
        /// <returns><see langword="true"/> for whitelisted usages.</returns>
        private static bool IsWhitelistedUse(IdentifierNameSyntax identifier, out string? elapsedMember)
        {
            elapsedMember = null;
            if (identifier.Parent is not MemberAccessExpressionSyntax access || access.Expression != identifier)
            {
                return false;
            }

            var memberName = access.Name.Identifier.ValueText;
            if (memberName is "Elapsed" or "ElapsedMilliseconds" or "ElapsedTicks")
            {
                elapsedMember = memberName;
                return true;
            }

            return memberName == "Stop" && access.Parent is InvocationExpressionSyntax;
        }
    }
}
