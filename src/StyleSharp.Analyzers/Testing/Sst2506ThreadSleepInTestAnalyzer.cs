// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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

    /// <summary>The method declaration registration shared by every compilation.</summary>
    private static readonly SyntaxKind[] MethodKinds = [SyntaxKind.MethodDeclaration];

    /// <summary>The metadata names of the attributes that mark a method as a test.</summary>
    private static readonly string[] TestMarkerMetadataNames =
    [
        "Xunit.FactAttribute",
        "Xunit.TheoryAttribute",
        "NUnit.Framework.TestAttribute",
        "NUnit.Framework.TestCaseAttribute",
        "NUnit.Framework.TestCaseSourceAttribute",
        "NUnit.Framework.TheoryAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute",
        "TUnit.Core.TestAttribute",
    ];

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(TestingRules.ThreadSleepInTest);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var symbols = new TestSymbols(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMethod(nodeContext, symbols), MethodKinds);
        });
    }

    /// <summary>Reports each <c>Thread.Sleep</c> in one test method's body.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="symbols">The thread and marker types cached on first demand per compilation.</param>
    private static void AnalyzeMethod(in SyntaxNodeAnalysisContext context, TestSymbols symbols)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!HasTestAttributeName(method.AttributeLists))
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
            scan.ThreadType = scan.Symbols.GetThreadType();
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

    /// <summary>Returns whether any of a method's attributes is spelled like a supported test marker.</summary>
    /// <param name="attributeLists">The method's attribute lists.</param>
    /// <returns><see langword="true"/> when a test-marker attribute name is present.</returns>
    private static bool HasTestAttributeName(SyntaxList<AttributeListSyntax> attributeLists)
    {
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (IsKnownTestAttributeName(GetAttributeSimpleName(attributes[j].Name)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether one of a method's marker-named attributes binds to a resolved test marker.</summary>
    /// <param name="attributeLists">The method's attribute lists.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="symbols">The test-marker attribute types resolved only when a bound type matches their name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the method carries a real test attribute.</returns>
    private static bool IsTestMethod(SyntaxList<AttributeListSyntax> attributeLists, SemanticModel model, TestSymbols symbols, CancellationToken cancellationToken)
    {
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var attribute = attributes[j];
                if (!IsKnownTestAttributeName(GetAttributeSimpleName(attribute.Name)))
                {
                    continue;
                }

                if (model.GetSymbolInfo(attribute, cancellationToken).Symbol is { ContainingType: { } attributeType }
                    && MatchesMarker(attributeType, symbols))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether an attribute type equals or derives from one of the resolved markers.</summary>
    /// <param name="attributeType">The bound attribute type.</param>
    /// <param name="symbols">The test-marker attribute types cached per compilation.</param>
    /// <returns><see langword="true"/> when the type is a marker or a subclass of one.</returns>
    private static bool MatchesMarker(INamedTypeSymbol attributeType, TestSymbols symbols)
    {
        for (var current = attributeType; current is not null; current = current.BaseType)
        {
            for (var i = 0; i < TestMarkerMetadataNames.Length; i++)
            {
                var metadataName = TestMarkerMetadataNames[i];
                var simpleName = metadataName.AsSpan(metadataName.LastIndexOf('.') + 1);
                if (current.MetadataName.AsSpan().SequenceEqual(simpleName)
                    && SymbolEqualityComparer.Default.Equals(current, symbols.GetMarker(i)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether an attribute's simple name matches a supported test marker, with or without the suffix.</summary>
    /// <param name="name">The attribute's rightmost identifier.</param>
    /// <returns><see langword="true"/> for a known test-attribute spelling.</returns>
    private static bool IsKnownTestAttributeName(string name) =>
        IsTestAttributeShortName(name) || IsTestAttributeSuffixedName(name);

    /// <summary>Returns whether a name is a supported test attribute written without the <c>Attribute</c> suffix.</summary>
    /// <param name="name">The attribute's rightmost identifier.</param>
    /// <returns><see langword="true"/> for the short spelling.</returns>
    private static bool IsTestAttributeShortName(string name) =>
        name is "Fact" or "Theory" or "Test" or "TestCase" or "TestCaseSource" or "TestMethod" or "DataTestMethod";

    /// <summary>Returns whether a name is a supported test attribute written with the <c>Attribute</c> suffix.</summary>
    /// <param name="name">The attribute's rightmost identifier.</param>
    /// <returns><see langword="true"/> for the suffixed spelling.</returns>
    private static bool IsTestAttributeSuffixedName(string name) =>
        name is "FactAttribute" or "TheoryAttribute" or "TestAttribute" or "TestCaseAttribute" or "TestCaseSourceAttribute" or "TestMethodAttribute" or "DataTestMethodAttribute";

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased attribute name.</summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The simple name, or an empty string.</returns>
    private static string GetAttributeSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>The state threaded through one method body's sleep walk.</summary>
    private record struct SleepScan
    {
        /// <summary>Initializes a new instance of the <see cref="SleepScan"/> struct.</summary>
        /// <param name="context">The syntax node context.</param>
        /// <param name="method">The method being analyzed.</param>
        /// <param name="symbols">The thread and marker types cached per compilation.</param>
        public SleepScan(in SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method, TestSymbols symbols)
        {
            Context = context;
            Method = method;
            Symbols = symbols;
        }

        /// <summary>Gets the syntax node context.</summary>
        public SyntaxNodeAnalysisContext Context { get; }

        /// <summary>Gets the thread and test-marker types resolved on the first candidate call.</summary>
        public TestSymbols Symbols { get; }

        /// <summary>Gets or sets the Thread type, resolved on the first Sleep-named call.</summary>
        public INamedTypeSymbol? ThreadType { get; set; }

        /// <summary>Gets the method being analyzed.</summary>
        public MethodDeclarationSyntax Method { get; }

        /// <summary>Gets or sets a value indicating whether the test-attribute binding has run.</summary>
        public bool TestChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether the method is a bound test.</summary>
        public bool IsTest { get; set; }
    }

    /// <summary>Resolves the thread type and individual test markers once each, only on demand.</summary>
    /// <param name="compilation">The compilation whose framework types are resolved.</param>
    private sealed class TestSymbols(Compilation compilation)
    {
        /// <summary>The metadata name of the type the reported call must bind to.</summary>
        private const string ThreadTypeMetadataName = "System.Threading.Thread";

        /// <summary>Serializes first lookups across method callbacks.</summary>
        private readonly object _gate = new();

        /// <summary>The resolved thread type, or null when it is absent.</summary>
        private INamedTypeSymbol? _threadType;

        /// <summary>The marker slots, allocated only when a bound attribute needs a marker lookup.</summary>
        private INamedTypeSymbol?[]? _markers;

        /// <summary>One bit per completed marker lookup, including absent results.</summary>
        private int _resolvedMarkers;

        /// <summary>Whether the thread lookup has completed, including an absent result.</summary>
        private bool _threadResolved;

        /// <summary>Reads the cached thread type without locking after resolution.</summary>
        /// <returns>The thread type, or null when it is unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetThreadType() => Volatile.Read(ref _threadResolved) ? _threadType : ResolveThreadType();

        /// <summary>Reads one marker without resolving any unrelated markers.</summary>
        /// <param name="index">The marker's index in the metadata-name table.</param>
        /// <returns>The marker type, or null when it is unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetMarker(int index) =>
            (Volatile.Read(ref _resolvedMarkers) & (1 << index)) != 0 ? _markers![index] : ResolveMarker(index);

        /// <summary>Resolves and publishes the thread type on first demand.</summary>
        /// <returns>The thread type, or null when it is unavailable.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private INamedTypeSymbol? ResolveThreadType()
        {
            lock (_gate)
            {
                if (!_threadResolved)
                {
                    _threadType = compilation.GetTypeByMetadataName(ThreadTypeMetadataName);
                    Volatile.Write(ref _threadResolved, true);
                }

                return _threadType;
            }
        }

        /// <summary>Resolves and publishes one marker on first demand.</summary>
        /// <param name="index">The marker's index in the metadata-name table.</param>
        /// <returns>The marker type, or null when it is unavailable.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private INamedTypeSymbol? ResolveMarker(int index)
        {
            lock (_gate)
            {
                var bit = 1 << index;
                _markers ??= new INamedTypeSymbol?[TestMarkerMetadataNames.Length];
                if ((_resolvedMarkers & bit) == 0)
                {
                    _markers[index] = compilation.GetTypeByMetadataName(TestMarkerMetadataNames[index]);
                    Volatile.Write(ref _resolvedMarkers, _resolvedMarkers | bit);
                }

                return _markers[index];
            }
        }
    }
}
