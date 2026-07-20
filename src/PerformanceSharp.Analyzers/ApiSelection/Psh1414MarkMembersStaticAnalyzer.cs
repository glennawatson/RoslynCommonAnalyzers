// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a private or internal instance method or property that never reads <c>this</c> (PSH1414). The
/// receiver is still passed at every call as a hidden argument, and the JIT must prove it
/// non-null before it can dispatch; <c>static</c> says what the member actually is and lets the
/// call go direct.
/// <para>
/// The rule touches only <c>private</c> and (assembly-visible) <c>internal</c> members: making a
/// <c>public</c> or <c>protected</c> member static is a breaking change to a surface other code binds
/// to, a decision the analyzer has no business making. Anything <c>virtual</c>, <c>abstract</c>, or
/// <c>override</c> is bound to instance dispatch by definition, and an auto-property <em>is</em>
/// instance state.
/// </para>
/// <para>
/// The rule is deliberately aware of the members that a framework <em>requires</em> to stay instance
/// methods, so it never suggests <c>static</c> where the suggestion would break the tool that reaches
/// the member. A member carrying a test attribute (xUnit, NUnit, MSTest, TUnit), a BenchmarkDotNet
/// benchmark or lifecycle attribute, or a serialization callback attribute is left alone, as is every
/// member of a type marked as a test fixture or a BenchmarkDotNet diagnostics host. Unlike a blanket
/// "any attribute is off-limits" rule, an unrelated attribute — <c>[Obsolete]</c>, an inlining hint —
/// does not exempt a member that could plainly be static.
/// </para>
/// <para>
/// A member "uses instance state" when it mentions <c>this</c> or <c>base</c>, or names any
/// non-static member of its own type or a base type. A call to itself does not count: an
/// unqualified recursive call still binds once the member is static.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1414MarkMembersStaticAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata names of member attributes that require the member to stay an instance method.</summary>
    private static readonly string[] InstanceRequiringMemberAttributeNames =
    [
        "Xunit.FactAttribute",
        "Xunit.TheoryAttribute",
        "NUnit.Framework.TestAttribute",
        "NUnit.Framework.TestCaseAttribute",
        "NUnit.Framework.TestCaseSourceAttribute",
        "NUnit.Framework.TheoryAttribute",
        "NUnit.Framework.SetUpAttribute",
        "NUnit.Framework.TearDownAttribute",
        "NUnit.Framework.OneTimeSetUpAttribute",
        "NUnit.Framework.OneTimeTearDownAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute",
        "TUnit.Core.TestAttribute",
        "TUnit.Core.BeforeAttribute",
        "TUnit.Core.AfterAttribute",
        "BenchmarkDotNet.Attributes.BenchmarkAttribute",
        "BenchmarkDotNet.Attributes.GlobalSetupAttribute",
        "BenchmarkDotNet.Attributes.GlobalCleanupAttribute",
        "BenchmarkDotNet.Attributes.IterationSetupAttribute",
        "BenchmarkDotNet.Attributes.IterationCleanupAttribute",
        "System.Runtime.Serialization.OnSerializingAttribute",
        "System.Runtime.Serialization.OnSerializedAttribute",
        "System.Runtime.Serialization.OnDeserializingAttribute",
        "System.Runtime.Serialization.OnDeserializedAttribute",
    ];

    /// <summary>The metadata names of type attributes whose members a framework reaches by reflection on an instance.</summary>
    private static readonly string[] FixtureTypeAttributeNames =
    [
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute",
        "NUnit.Framework.TestFixtureAttribute",
        "BenchmarkDotNet.Attributes.MemoryDiagnoserAttribute",
        "BenchmarkDotNet.Attributes.SimpleJobAttribute",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.MarkMembersStatic);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var memberMarkers = ResolveMarkers(start.Compilation, InstanceRequiringMemberAttributeNames);
            var fixtureMarkers = ResolveMarkers(start.Compilation, FixtureTypeAttributeNames);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeMember(nodeContext, memberMarkers, fixtureMarkers),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.PropertyDeclaration);
        });
    }

    /// <summary>Returns whether a member's modifiers even allow it to become static.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member is a plain private or internal instance member.</returns>
    /// <remarks>
    /// This is the syntactic gate shared with the code fix. It decides accessibility and dispatch shape
    /// only; whether a framework requires the member to stay instance is a semantic question answered in
    /// <see cref="AnalyzeMember"/>, which is why an attribute no longer disqualifies a member here.
    /// </remarks>
    internal static bool IsEligibleDeclaration(MemberDeclarationSyntax member)
    {
        if (member.Parent is not TypeDeclarationSyntax)
        {
            return false;
        }

        var modifiers = member.Modifiers;
        var isPrivateOrInternal = modifiers.Any(SyntaxKind.PrivateKeyword) || modifiers.Any(SyntaxKind.InternalKeyword);
        return isPrivateOrInternal && !HasDisqualifyingModifier(modifiers);
    }

    /// <summary>Returns whether a member carries a modifier that either widens its surface or fixes its dispatch.</summary>
    /// <param name="modifiers">The declaration's modifiers.</param>
    /// <returns><see langword="true"/> when the member cannot be made static without changing a contract.</returns>
    private static bool HasDisqualifyingModifier(SyntaxTokenList modifiers)
        => modifiers.Any(SyntaxKind.ProtectedKeyword)
            || modifiers.Any(SyntaxKind.PublicKeyword)
            || modifiers.Any(SyntaxKind.StaticKeyword)
            || modifiers.Any(SyntaxKind.VirtualKeyword)
            || modifiers.Any(SyntaxKind.AbstractKeyword)
            || modifiers.Any(SyntaxKind.OverrideKeyword)
            || modifiers.Any(SyntaxKind.ExternKeyword)
            || modifiers.Any(SyntaxKind.PartialKeyword);

    /// <summary>Reports PSH1414 for a private or internal instance member that never reads <c>this</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="memberMarkers">The resolved attributes that pin a member to instance dispatch.</param>
    /// <param name="fixtureMarkers">The resolved attributes that mark a type's members as reflection targets.</param>
    private static void AnalyzeMember(SyntaxNodeAnalysisContext context, INamedTypeSymbol[] memberMarkers, INamedTypeSymbol[] fixtureMarkers)
    {
        var member = (MemberDeclarationSyntax)context.Node;
        if (!IsEligibleDeclaration(member)
            || TryGetExecutableBody(member) is not { } body
            || context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken) is not { } symbol
            || HasAttributeFrom(symbol.GetAttributes(), memberMarkers)
            || HasAttributeFrom(symbol.ContainingType.GetAttributes(), fixtureMarkers))
        {
            return;
        }

        if (UsesInstanceState(context, body, symbol))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.MarkMembersStatic,
            GetIdentifier(member).GetLocation(),
            symbol.Name));
    }

    /// <summary>Resolves the non-null named-type symbols for a set of metadata names, right-sized.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <param name="metadataNames">The metadata names to resolve.</param>
    /// <returns>The resolved markers, an array no longer than <paramref name="metadataNames"/> (empty when none resolve).</returns>
    private static INamedTypeSymbol[] ResolveMarkers(Compilation compilation, string[] metadataNames)
    {
        var buffer = new INamedTypeSymbol[metadataNames.Length];
        var count = 0;
        for (var i = 0; i < metadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(metadataNames[i]) is { } marker)
            {
                buffer[count++] = marker;
            }
        }

        if (count == buffer.Length)
        {
            return buffer;
        }

        var result = new INamedTypeSymbol[count];
        Array.Copy(buffer, result, count);
        return result;
    }

    /// <summary>Returns whether any of a member's or type's attributes binds to a resolved marker.</summary>
    /// <param name="attributes">The attributes to inspect.</param>
    /// <param name="markers">The resolved markers to match against.</param>
    /// <returns><see langword="true"/> when an attribute equals or derives from a marker.</returns>
    private static bool HasAttributeFrom(ImmutableArray<AttributeData> attributes, INamedTypeSymbol[] markers)
    {
        if (markers.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < attributes.Length; i++)
        {
            for (var current = attributes[i].AttributeClass; current is not null; current = current.BaseType)
            {
                for (var j = 0; j < markers.Length; j++)
                {
                    if (SymbolEqualityComparer.Default.Equals(current, markers[j]))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Returns the member's executable body, or nothing when it has none to inspect.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The body to scan, or <see langword="null"/> for an abstract or auto-implemented member.</returns>
    private static SyntaxNode? TryGetExecutableBody(MemberDeclarationSyntax member)
        => member switch
        {
            MethodDeclarationSyntax method => (SyntaxNode?)method.Body ?? method.ExpressionBody,
            PropertyDeclarationSyntax { ExpressionBody: { } expressionBody } => expressionBody,
            PropertyDeclarationSyntax { AccessorList: { } accessors } => HasAccessorBody(accessors) ? accessors : null,
            _ => null,
        };

    /// <summary>Returns whether a property's accessors have real bodies, so it is not an auto-property.</summary>
    /// <param name="accessors">The property's accessor list.</param>
    /// <returns><see langword="true"/> when at least one accessor has a body, and none is auto-implemented.</returns>
    private static bool HasAccessorBody(AccessorListSyntax accessors)
    {
        var list = accessors.Accessors;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Body is null && list[i].ExpressionBody is null)
            {
                return false;
            }
        }

        return list.Count > 0;
    }

    /// <summary>Returns the identifier token the diagnostic is reported on.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The member's name token.</returns>
    private static SyntaxToken GetIdentifier(MemberDeclarationSyntax member)
        => member switch
        {
            MethodDeclarationSyntax method => method.Identifier,
            PropertyDeclarationSyntax property => property.Identifier,
            _ => default,
        };

    /// <summary>Returns whether a member's body reads <c>this</c>, directly or through an unqualified member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="body">The member's executable body.</param>
    /// <param name="symbol">The member being analyzed, whose own self-references do not count.</param>
    /// <returns><see langword="true"/> when the member depends on its receiver.</returns>
    private static bool UsesInstanceState(SyntaxNodeAnalysisContext context, SyntaxNode body, ISymbol symbol)
    {
        var state = new InstanceUseScanState(symbol, symbol.ContainingType, context.SemanticModel, context.CancellationToken);
        DescendantTraversalHelper.VisitDescendants<ExpressionSyntax, InstanceUseScanState>(body, ref state, VisitBodyExpression);
        return state.UsesInstance;
    }

    /// <summary>Classifies one expression in the body as instance-dependent or not.</summary>
    /// <param name="node">The visited expression.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once instance use is proved.</returns>
    private static bool VisitBodyExpression(ExpressionSyntax node, ref InstanceUseScanState state)
    {
        if (node is ThisExpressionSyntax or BaseExpressionSyntax)
        {
            state.UsesInstance = true;
            return false;
        }

        if (node is not IdentifierNameSyntax identifier)
        {
            return true;
        }

        if (state.Model.GetSymbolInfo(identifier, state.CancellationToken).Symbol is not { IsStatic: false } referenced
            || SymbolEqualityComparer.Default.Equals(referenced, state.Symbol))
        {
            return true;
        }

        var usesInstance = referenced is IParameterSymbol parameter
            ? IsCapturedFromEnclosingDeclaration(parameter, state.Symbol)
            : IsInstanceMemberOfHierarchy(referenced, state.ContainingType);

        if (!usesInstance)
        {
            return true;
        }

        state.UsesInstance = true;
        return false;
    }

    /// <summary>Returns whether a parameter belongs to a declaration that encloses the analyzed member.</summary>
    /// <param name="parameter">The referenced parameter.</param>
    /// <param name="member">The member being analyzed.</param>
    /// <returns><see langword="true"/> when naming the parameter binds the member to its receiver.</returns>
    /// <remarks>
    /// A parameter the member owns — its own, or one belonging to a lambda or local function written
    /// inside it — is just a value in scope. A parameter owned by something <em>outside</em> the member
    /// is not: it is the enclosing declaration's, captured into the object so the member can read it.
    /// Both shapes that produce one make <c>static</c> a compiler error rather than a cleanup — a primary
    /// constructor parameter gives CS9105, and an extension block's receiver gives CS9347 — so a member
    /// that names either is left alone.
    /// </remarks>
    private static bool IsCapturedFromEnclosingDeclaration(IParameterSymbol parameter, ISymbol member)
    {
        for (var owner = parameter.ContainingSymbol; owner is not null; owner = owner.ContainingSymbol)
        {
            if (SymbolEqualityComparer.Default.Equals(owner, member))
            {
                return false;
            }

            if (owner is INamedTypeSymbol)
            {
                break;
            }
        }

        return true;
    }

    /// <summary>Returns whether a symbol is an instance member of the analyzed type or one of its bases.</summary>
    /// <param name="symbol">The referenced symbol.</param>
    /// <param name="containingType">The analyzed member's containing type.</param>
    /// <returns><see langword="true"/> when reading the symbol requires a receiver.</returns>
    private static bool IsInstanceMemberOfHierarchy(ISymbol symbol, INamedTypeSymbol containingType)
    {
        if (symbol.Kind is not (SymbolKind.Field or SymbolKind.Property or SymbolKind.Method or SymbolKind.Event))
        {
            return false;
        }

        for (var current = containingType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol.ContainingType, current))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tracks whether a member's body was shown to depend on its receiver.</summary>
    /// <param name="Symbol">The member being analyzed.</param>
    /// <param name="ContainingType">The member's containing type.</param>
    /// <param name="Model">The semantic model.</param>
    /// <param name="CancellationToken">A token that cancels the operation.</param>
    private record struct InstanceUseScanState(
        ISymbol Symbol,
        INamedTypeSymbol ContainingType,
        SemanticModel Model,
        CancellationToken CancellationToken)
    {
        /// <summary>Gets or sets a value indicating whether the body reads instance state.</summary>
        public bool UsesInstance { get; set; }
    }
}
