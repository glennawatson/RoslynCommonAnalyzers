// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a call to <c>System.Threading.Thread.Sleep</c> inside the body of a test method (SST2506). A fixed
/// real-time delay makes the suite slower on every run and is a classic flaky-test source, because it races the
/// wall clock instead of waiting for the condition the test actually depends on.
/// </summary>
/// <remarks>
/// <para>
/// The rule is gated on two facts: <c>System.Threading.Thread</c> resolving (no type,
/// no <c>Thread.Sleep</c> to find) and at least one supported test-framework marker attribute
/// (xUnit, NUnit, MSTest, or TUnit) resolving. These types are resolved only after a test-shaped method contains
/// a <c>Sleep</c>-named call.
/// </para>
/// <para>
/// The clean path is syntax only. A method is skipped unless one of its attributes is spelled like a test marker,
/// and its body is then walked once for a <c>Sleep</c>-named call. Nothing binds until such a call is found; only
/// then is the call bound, excluding same-named user methods before framework types resolve. A marker attribute
/// is then bound to confirm the method really is a test, so a same-named user attribute is left alone. Only
/// markers matching a bound attribute or base type's name are resolved. The walk reaches a sleep nested in an
/// <c>if</c>, a loop, a lambda, or a local function.
/// </para>
/// <para>
/// Only <c>Thread.Sleep</c> is reported. An awaited <c>Task.Delay</c> with a constant delay is the same defect in
/// asynchronous form, but it is left to a separate rule so this one stays a single, unambiguous shape.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2506ThreadSleepInTestAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The method name every reported call is spelled with.</summary>
    private const string SleepMethodName = "Sleep";

    /// <summary>The metadata name of the type the reported call must bind to.</summary>
    private const string ThreadTypeMetadataName = "System.Threading.Thread";

    /// <summary>The slot of the thread type in <see cref="SleepMetadataNames"/>.</summary>
    private const int ThreadSlot = 0;

    /// <summary>The first test-marker slot in <see cref="SleepMetadataNames"/>.</summary>
    private const int FirstMarkerSlot = 1;

    /// <summary>The method declaration registration shared by every compilation.</summary>
    private static readonly SyntaxKind[] MethodKinds = [SyntaxKind.MethodDeclaration];

    /// <summary>The thread type followed by the test-marker attributes, one slot each.</summary>
    private static readonly string[] SleepMetadataNames = TestAttributeNames.CreateMarkerMetadataNames(ThreadTypeMetadataName);

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(TestingRules.ThreadSleepInTest);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataTypeSlots(compilation, SleepMetadataNames),
            AnalyzeMethod,
            MethodKinds);
    }

    /// <summary>Reports each <c>Thread.Sleep</c> in one test method's body.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="symbols">The thread and marker types cached on first demand per compilation.</param>
    private static void AnalyzeMethod(in SyntaxNodeAnalysisContext context, LazyMetadataTypeSlots symbols)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!SyntaxNames.AnyAttributeNamed(method.AttributeLists, TestAttributeNames.IsTestAttributeName))
        {
            return;
        }

        var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null)
        {
            return;
        }

        var scan = new SleepScan(context, method, symbols);
        _ = DescendantTraversalHelper.VisitDescendants<InvocationExpressionSyntax, SleepScan>(body, ref scan, VisitInvocation);
    }

    /// <summary>Reports one <c>Thread.Sleep</c> call, confirming the enclosing method is a test on first hit.</summary>
    /// <param name="invocation">The invocation being visited.</param>
    /// <param name="scan">The scan state threaded through the walk.</param>
    /// <returns><see langword="false"/> to stop the walk when the method is not a test; otherwise <see langword="true"/>.</returns>
    private static bool VisitInvocation(InvocationExpressionSyntax invocation, ref SleepScan scan)
    {
        if (!IsSleepNamed(invocation.Expression))
        {
            return true;
        }

        if (scan.Context.SemanticModel.GetSymbolInfo(invocation, scan.Context.CancellationToken).Symbol is not IMethodSymbol { Name: SleepMethodName } method
            || method.ContainingType is not
            {
                Name: "Thread",
                ContainingNamespace: { Name: "Threading", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } },
            })
        {
            return true;
        }

        if (!scan.TestChecked)
        {
            scan.ThreadType = scan.Symbols.Get(ThreadSlot);
            if (scan.ThreadType is null)
            {
                return false;
            }

            scan.IsTest = IsTestMethod(scan.Method.AttributeLists, scan.Context.SemanticModel, scan.Symbols, scan.Context.CancellationToken);
            scan.TestChecked = true;
        }

        if (!scan.IsTest)
        {
            return false;
        }

        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, scan.ThreadType))
        {
            return true;
        }

        scan.Context.ReportDiagnostic(DiagnosticHelper.Create(TestingRules.ThreadSleepInTest, invocation.GetLocation()));
        return true;
    }

    /// <summary>Returns whether an invoked expression names <c>Sleep</c>.</summary>
    /// <param name="expression">The invocation's expression.</param>
    /// <returns><see langword="true"/> for <c>Thread.Sleep</c> or a <c>Sleep</c> reached through <c>using static</c>.</returns>
    private static bool IsSleepNamed(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText == SleepMethodName,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == SleepMethodName,
        _ => false,
    };

    /// <summary>Returns whether one of a method's marker-named attributes binds to a resolved test marker.</summary>
    /// <param name="attributeLists">The method's attribute lists.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="symbols">The test-marker attribute types resolved only when a bound type matches their name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the method carries a real test attribute.</returns>
    private static bool IsTestMethod(SyntaxList<AttributeListSyntax> attributeLists, SemanticModel model, LazyMetadataTypeSlots symbols, CancellationToken cancellationToken)
    {
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var attribute = attributes[j];
                if (!TestAttributeNames.IsTestAttributeName(SyntaxNames.GetSimpleName(attribute.Name)))
                {
                    continue;
                }

                if (model.GetSymbolInfo(attribute, cancellationToken).Symbol is { ContainingType: { } attributeType }
                    && symbols.IsOrDerivesFromAny(attributeType, FirstMarkerSlot, SleepMetadataNames.Length))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The state threaded through one method body's sleep walk.</summary>
    private record struct SleepScan
    {
        /// <summary>Initializes a new instance of the <see cref="SleepScan"/> struct.</summary>
        /// <param name="context">The syntax node context.</param>
        /// <param name="method">The method being analyzed.</param>
        /// <param name="symbols">The thread and marker types cached per compilation.</param>
        public SleepScan(in SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method, LazyMetadataTypeSlots symbols)
        {
            Context = context;
            Method = method;
            Symbols = symbols;
        }

        /// <summary>Gets the syntax node context.</summary>
        public SyntaxNodeAnalysisContext Context { get; }

        /// <summary>Gets the thread and test-marker types resolved on the first candidate call.</summary>
        public LazyMetadataTypeSlots Symbols { get; }

        /// <summary>Gets or sets the Thread type, resolved on the first Sleep-named call.</summary>
        public INamedTypeSymbol? ThreadType { get; set; }

        /// <summary>Gets the method being analyzed.</summary>
        public MethodDeclarationSyntax Method { get; }

        /// <summary>Gets or sets a value indicating whether the test-attribute binding has run.</summary>
        public bool TestChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether the method is a bound test.</summary>
        public bool IsTest { get; set; }
    }
}
