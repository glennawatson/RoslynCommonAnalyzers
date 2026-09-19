// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags trivial expression-bodied forwarder methods and operators that lack
/// <c>MethodImplOptions.AggressiveInlining</c> (PSH1410). A one-expression forwarder — a
/// delegation, member read, or constant — can still be skipped by the JIT's IL-size inlining
/// heuristics; the attribute makes the intent explicit. Virtual, abstract, override, async,
/// extern, partial, and interface members are skipped, as is anything already carrying a
/// MethodImpl attribute. Blanket inlining attributes are an opinionated convention, so the
/// rule is opt-in. Gated on <c>MethodImplOptions.AggressiveInlining</c> existing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1410AggressiveInliningAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The attribute simple names that mark an explicit inlining decision.</summary>
    internal const string MethodImplAttributeShortName = "MethodImpl";

    /// <summary>The flag member the rule suggests.</summary>
    private const string AggressiveInliningMemberName = "AggressiveInlining";

    /// <summary>The metadata name of the options enum the attribute takes.</summary>
    private const string MethodImplOptionsMetadataName = "System.Runtime.CompilerServices.MethodImplOptions";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ApiSelectionRules.InlineTrivialForwarders);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, MethodImplOptionsMetadataName),
            AnalyzeMethod,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.OperatorDeclaration);
    }

    /// <summary>Returns whether a method declaration is a trivial forwarder eligible for the attribute.</summary>
    /// <param name="declaration">The method or operator declaration.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    internal static bool IsEligibleForwarder(BaseMethodDeclarationSyntax declaration)
    {
        if (declaration.ExpressionBody is null
            || declaration.Parent is InterfaceDeclarationSyntax
            || HasDisqualifyingModifier(declaration.Modifiers)
            || HasMethodImplAttribute(declaration.AttributeLists)
            || SitsInsideAConditionalRegion(declaration))
        {
            return false;
        }

        return IsForwardingExpression(declaration.ExpressionBody.Expression);
    }

    /// <summary>Returns whether a member sits inside a conditional compilation region.</summary>
    /// <param name="declaration">The member declaration.</param>
    /// <returns><see langword="true"/> when an <c>#if</c> region encloses the member.</returns>
    /// <remarks>
    /// A project that multi-targets compiles one file once per framework, and the same member is commonly
    /// written once per branch — carrying the attribute in the branch that needs it and not in the one that
    /// does not. Only some of those compilations then want the edit, and Roslyn cannot reconcile a linked
    /// document that gained the attribute in one framework and not another: it writes conflict markers into
    /// the source instead, and re-reports the branch that already had it until a duplicate attribute lands.
    /// No single edit is right for every compilation of the file, so none is offered.
    /// </remarks>
    internal static bool SitsInsideAConditionalRegion(SyntaxNode declaration)
    {
        var root = declaration.SyntaxTree.GetRoot();
        if (!root.ContainsDirectives)
        {
            return false;
        }

        var state = new ConditionalRegionScan(declaration.SpanStart);
        _ = DescendantTraversalHelper.VisitDescendantTokens(root, ref state, VisitConditionalToken);
        return state.Depth > 0;
    }

    /// <summary>Scans one token's trivia until the member's start is reached.</summary>
    /// <param name="token">The token visited in document order.</param>
    /// <param name="state">The member's start and the current conditional nesting depth.</param>
    /// <returns>True when the traversal should continue.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool VisitConditionalToken(in SyntaxToken token, ref ConditionalRegionScan state) =>
        ScanConditionalTrivia(token.LeadingTrivia, ref state)
            && token.SpanStart < state.Start
            && ScanConditionalTrivia(token.TrailingTrivia, ref state);

    /// <summary>Counts conditional directives before the member, including structured trivia.</summary>
    /// <param name="triviaList">The leading or trailing trivia to inspect.</param>
    /// <param name="state">The member's start and the current conditional nesting depth.</param>
    /// <returns>True when the traversal has not reached the member.</returns>
    private static bool ScanConditionalTrivia(in SyntaxTriviaList triviaList, ref ConditionalRegionScan state)
    {
        for (var i = 0; i < triviaList.Count; i++)
        {
            var trivia = triviaList[i];
            if (trivia.SpanStart >= state.Start)
            {
                return false;
            }

            if (trivia.IsKind(SyntaxKind.IfDirectiveTrivia))
            {
                state.Depth++;
            }
            else if (trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia))
            {
                state.Depth--;
            }

            if (trivia.GetStructure() is { } structure
                && !DescendantTraversalHelper.VisitDescendantTokens(structure, ref state, VisitConditionalToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an expression is a plain forward: a call, member read, index, or constant.</summary>
    /// <param name="expression">The body expression.</param>
    /// <returns><see langword="true"/> for forwarding shapes.</returns>
    private static bool IsForwardingExpression(ExpressionSyntax expression) =>
        expression is InvocationExpressionSyntax or MemberAccessExpressionSyntax or IdentifierNameSyntax
            or ElementAccessExpressionSyntax or ConditionalAccessExpressionSyntax or LiteralExpressionSyntax
            or ObjectCreationExpressionSyntax;

    /// <summary>Returns whether the modifier list rules the member out.</summary>
    /// <param name="modifiers">The member's modifiers.</param>
    /// <returns><see langword="true"/> for virtual-dispatch, async, extern, and partial members.</returns>
    private static bool HasDisqualifyingModifier(in SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            var kind = modifiers[i].Kind();
            if (kind is SyntaxKind.VirtualKeyword or SyntaxKind.AbstractKeyword or SyntaxKind.OverrideKeyword
                or SyntaxKind.AsyncKeyword or SyntaxKind.ExternKeyword or SyntaxKind.PartialKeyword)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether any attribute is a MethodImpl attribute, by simple name.</summary>
    /// <param name="attributeLists">The member's attribute lists.</param>
    /// <returns><see langword="true"/> when an explicit inlining decision exists.</returns>
    private static bool HasMethodImplAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var list in attributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                var name = attribute.Name;
                while (name is QualifiedNameSyntax qualified)
                {
                    name = qualified.Right;
                }

                if (name is SimpleNameSyntax simple
                    && simple.Identifier.ValueText is MethodImplAttributeShortName or $"{MethodImplAttributeShortName}Attribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Reports PSH1410 for an eligible forwarder.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="frameworkTypes">The compilation's deferred framework type cache.</param>
    private static void AnalyzeMethod(in SyntaxNodeAnalysisContext context, LazyMetadataType frameworkTypes)
    {
        var declaration = (BaseMethodDeclarationSyntax)context.Node;
        if (!IsEligibleForwarder(declaration))
        {
            return;
        }

        if (declaration.AttributeLists.Count != 0
            && (context.ContainingSymbol as IMethodSymbol
                ?? context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)) is { } member
            && HasTestFrameworkAttribute(member))
        {
            return;
        }

        if (frameworkTypes.Get() is not { } options
            || options.GetMembers(AggressiveInliningMemberName).IsEmpty)
        {
            return;
        }

        var identifier = declaration is MethodDeclarationSyntax method
            ? method.Identifier
            : ((OperatorDeclarationSyntax)declaration).OperatorToken;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.InlineTrivialForwarders,
            identifier.GetLocation(),
            identifier.ValueText));
    }

    /// <summary>Returns whether a member carries a known test framework entry point attribute.</summary>
    /// <param name="member">The resolved member symbol.</param>
    /// <returns><see langword="true"/> when a test or lifecycle attribute is present.</returns>
    private static bool HasTestFrameworkAttribute(ISymbol member)
    {
        var attributes = member.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            if (attributes[i].AttributeClass is { } attributeType
                && IsTestFrameworkAttribute(attributeType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is a known test attribute or derives from one.</summary>
    /// <param name="type">The resolved attribute type.</param>
    /// <returns><see langword="true"/> when the type belongs to a supported test framework.</returns>
    private static bool IsTestFrameworkAttribute(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (IsKnownTestFrameworkAttribute(current))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type has a supported framework namespace and attribute name.</summary>
    /// <param name="type">The candidate attribute type.</param>
    /// <returns><see langword="true"/> when the type is an exact supported framework attribute.</returns>
    private static bool IsKnownTestFrameworkAttribute(INamedTypeSymbol type)
    {
        if (type.ContainingType is not null)
        {
            return false;
        }

        var containingNamespace = type.ContainingNamespace;
        return IsNUnitTestAttribute(containingNamespace, type.Name)
            || IsXunitTestAttribute(containingNamespace, type.Name)
            || IsMSTestAttribute(containingNamespace, type.Name);
    }

    /// <summary>Returns whether a type is one of NUnit's test or lifecycle attributes.</summary>
    /// <param name="containingNamespace">The type's containing namespace.</param>
    /// <param name="name">The type name.</param>
    /// <returns><see langword="true"/> for an exact NUnit attribute.</returns>
    private static bool IsNUnitTestAttribute(INamespaceSymbol containingNamespace, string name) =>
        containingNamespace is
        {
            Name: "Framework",
            ContainingNamespace: { Name: "NUnit", ContainingNamespace.IsGlobalNamespace: true },
        }
        && name is "TestAttribute"
            or "TestCaseAttribute"
            or "TestCaseSourceAttribute"
            or "TheoryAttribute"
            or "SetUpAttribute"
            or "TearDownAttribute"
            or "OneTimeSetUpAttribute"
            or "OneTimeTearDownAttribute";

    /// <summary>Returns whether a type is one of xUnit's test attributes.</summary>
    /// <param name="containingNamespace">The type's containing namespace.</param>
    /// <param name="name">The type name.</param>
    /// <returns><see langword="true"/> for an exact xUnit attribute.</returns>
    private static bool IsXunitTestAttribute(INamespaceSymbol containingNamespace, string name) =>
        containingNamespace is { Name: "Xunit", ContainingNamespace.IsGlobalNamespace: true }
        && name is "FactAttribute" or "TheoryAttribute";

    /// <summary>Returns whether a type is one of MSTest's test or lifecycle attributes.</summary>
    /// <param name="containingNamespace">The type's containing namespace.</param>
    /// <param name="name">The type name.</param>
    /// <returns><see langword="true"/> for an exact MSTest attribute.</returns>
    private static bool IsMSTestAttribute(INamespaceSymbol containingNamespace, string name) =>
        containingNamespace is
        {
            Name: "UnitTesting",
            ContainingNamespace:
            {
                Name: "TestTools",
                ContainingNamespace:
                {
                    Name: "VisualStudio",
                    ContainingNamespace:
                    {
                        Name: "Microsoft",
                        ContainingNamespace.IsGlobalNamespace: true,
                    },
                },
            },
        }
        && name is "TestMethodAttribute"
            or "DataTestMethodAttribute"
            or "TestInitializeAttribute"
            or "TestCleanupAttribute"
            or "ClassInitializeAttribute"
            or "ClassCleanupAttribute"
            or "AssemblyInitializeAttribute"
            or "AssemblyCleanupAttribute";
}
