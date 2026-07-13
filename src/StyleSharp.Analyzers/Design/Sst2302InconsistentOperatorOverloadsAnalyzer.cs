// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an operator overload whose set is incomplete in a way the language itself does not catch
/// (SST2302).
/// </summary>
/// <remarks>
/// <para>
/// The language already refuses to compile a type that declares <c>==</c> without <c>!=</c>, <c>&lt;</c>
/// without <c>&gt;</c>, or <c>&lt;=</c> without <c>&gt;=</c>. Repeating those here would be a rule that
/// can only fire on code that does not build, so it reports the gaps the compiler leaves open instead:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <c>==</c> without an <c>Equals(object)</c> override, or without a <c>GetHashCode()</c> override — two
/// notions of equality that are free to disagree, and a hash that does not follow the equality the type
/// advertises.
/// </description>
/// </item>
/// <item>
/// <description>
/// the <c>&lt;</c>/<c>&gt;</c> pair without the <c>&lt;=</c>/<c>&gt;=</c> pair, or the reverse. The
/// compiler pairs each operator with its mirror but never requires the other pair, so a caller can end up
/// able to write <c>a &lt; b</c> and not <c>a &lt;= b</c>.
/// </description>
/// </item>
/// <item>
/// <description>
/// relational operators on a type that does not implement <c>IComparable&lt;T&gt;</c>, which is what
/// sorting, <c>Max</c>, and every ordered collection actually ask for.
/// </description>
/// </item>
/// </list>
/// <para>
/// Each gap is reported once and in one place: the equality gap on the <c>==</c> declaration, and the
/// ordering gaps on the relational operator that is present. The mirror operators (<c>!=</c>, <c>&gt;</c>,
/// <c>&gt;=</c>) are never the reporting site, so a complete pair does not produce the same diagnostic
/// twice.
/// </para>
/// <para>
/// Analysis is driven from the operator declaration, not the type, so a compilation without operator
/// overloads — which is nearly all of them — never runs a single check.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2302InconsistentOperatorOverloadsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the less-than operator.</summary>
    private const string LessThanName = "op_LessThan";

    /// <summary>The metadata name of the less-than-or-equal operator.</summary>
    private const string LessThanOrEqualName = "op_LessThanOrEqual";

    /// <summary>The unqualified name of the ordering contract.</summary>
    private const string ComparableName = "IComparable";

    /// <summary>The metadata name of the generic ordering contract the rule suggests.</summary>
    private const string ComparableMetadataName = "System.IComparable`1";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.InconsistentOperatorOverloads);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeOperator, SyntaxKind.OperatorDeclaration);
    }

    /// <summary>Checks the set one operator declaration belongs to.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeOperator(SyntaxNodeAnalysisContext context)
    {
        var declaration = (OperatorDeclarationSyntax)context.Node;
        var kind = declaration.OperatorToken.Kind();
        if (kind is not (SyntaxKind.EqualsEqualsToken or SyntaxKind.LessThanToken or SyntaxKind.LessThanEqualsToken))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { ContainingType: { } type })
        {
            return;
        }

        var location = declaration.OperatorToken.GetLocation();
        if (kind == SyntaxKind.EqualsEqualsToken)
        {
            AnalyzeEqualityOperator(context, type, location);
            return;
        }

        AnalyzeRelationalOperator(context, type, location, kind);
    }

    /// <summary>Reports an <c>==</c> whose type does not also decide equality the way the framework asks.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="type">The type that declares the operator.</param>
    /// <param name="location">The operator token's location.</param>
    private static void AnalyzeEqualityOperator(SyntaxNodeAnalysisContext context, INamedTypeSymbol type, Location location)
    {
        var missingEquals = !OverridesObjectMethod(type, nameof(Equals), parameterCount: 1);
        var missingHashCode = !OverridesObjectMethod(type, nameof(GetHashCode), parameterCount: 0);
        if (!missingEquals && !missingHashCode)
        {
            return;
        }

        var missing = (missingEquals, missingHashCode) switch
        {
            (true, true) => "Equals(object) and GetHashCode()",
            (true, false) => "Equals(object)",
            _ => "GetHashCode()",
        };

        Report(context, location, type, "==", missing);
    }

    /// <summary>Reports the halves of the ordering set that the language does not force.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="type">The type that declares the operator.</param>
    /// <param name="location">The operator token's location.</param>
    /// <param name="kind">The declared operator, which is <c>&lt;</c> or <c>&lt;=</c>.</param>
    /// <remarks>
    /// One diagnostic, from one site. <c>&lt;</c> owns the report when the type declares it and <c>&lt;=</c>
    /// only speaks when <c>&lt;</c> is absent, so the mirror operators never repeat what their partner has
    /// already said; and a type missing both the other pair and the ordering contract is told so once
    /// rather than squiggled twice in the same place.
    /// </remarks>
    private static void AnalyzeRelationalOperator(SyntaxNodeAnalysisContext context, INamedTypeSymbol type, Location location, SyntaxKind kind)
    {
        var declaresLessThan = kind == SyntaxKind.LessThanToken;
        var hasLessThan = type.GetMembers(LessThanName).Length > 0;
        if (!declaresLessThan && hasLessThan)
        {
            return;
        }

        var hasLessThanOrEqual = type.GetMembers(LessThanOrEqualName).Length > 0;
        var missingPair = hasLessThan != hasLessThanOrEqual;

        // The ordering contract is only asked for once it is known to exist in the analyzed compilation.
        // The lookup runs on the rare operator path and only when the type does not already implement it.
        var missingComparable = !ImplementsComparable(type) && CanImplementComparable(context.Compilation);
        if (!missingPair && !missingComparable)
        {
            return;
        }

        var missing = GetMissingOrdering(declaresLessThan, missingPair, missingComparable, type.Name);
        Report(context, location, type, declaresLessThan ? "<" : "<=", missing);
    }

    /// <summary>Composes what an incomplete ordering set does not declare.</summary>
    /// <param name="declaresLessThan">Whether the reported operator is <c>&lt;</c> rather than <c>&lt;=</c>.</param>
    /// <param name="missingPair">Whether the mirror pair of the declared one is absent.</param>
    /// <param name="missingComparable">Whether the ordering contract is absent.</param>
    /// <param name="typeName">The name of the type that declares the operator.</param>
    /// <returns>The text naming everything the set is missing.</returns>
    private static string GetMissingOrdering(bool declaresLessThan, bool missingPair, bool missingComparable, string typeName)
    {
        var pair = declaresLessThan ? "<= and >=" : "< and >";
        var comparable = $"IComparable<{typeName}>";
        return (missingPair, missingComparable) switch
        {
            (true, true) => $"{pair} and {comparable}",
            (true, false) => pair,
            _ => comparable,
        };
    }

    /// <summary>Returns whether a type overrides one of <c>object</c>'s equality members.</summary>
    /// <param name="type">The type that declares the operator.</param>
    /// <param name="name">The member name.</param>
    /// <param name="parameterCount">The member's parameter count.</param>
    /// <returns><see langword="true"/> when the type declares the override itself.</returns>
    /// <remarks>
    /// An override declared on a base class does not count. The base decides equality over the base's
    /// state; a derived type that adds state and an <c>==</c> of its own has to say how that state
    /// participates, and inheriting the answer is exactly the bug.
    /// </remarks>
    private static bool OverridesObjectMethod(INamedTypeSymbol type, string name, int parameterCount)
    {
        var members = type.GetMembers(name);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsOverride: true } method && method.Parameters.Length == parameterCount)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type implements either form of the ordering contract.</summary>
    /// <param name="type">The type that declares the operator.</param>
    /// <returns><see langword="true"/> when the type can be ordered by something other than its operators.</returns>
    private static bool ImplementsComparable(INamedTypeSymbol type)
    {
        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidate = interfaces[i];
            if (string.Equals(candidate.Name, ComparableName, StringComparison.Ordinal)
                && candidate.ContainingNamespace is { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the analyzed compilation actually has the contract the rule is about to ask for.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    /// <returns><see langword="true"/> when <c>IComparable&lt;T&gt;</c> can be implemented.</returns>
    private static bool CanImplementComparable(Compilation compilation)
        => compilation.GetTypeByMetadataName(ComparableMetadataName) is not null;

    /// <summary>Reports one gap in an operator set.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="location">Where the gap is reported.</param>
    /// <param name="type">The type that declares the operator.</param>
    /// <param name="declared">The operator the type declares.</param>
    /// <param name="missing">What the type does not declare.</param>
    private static void Report(SyntaxNodeAnalysisContext context, Location location, INamedTypeSymbol type, string declared, string missing)
        => context.ReportDiagnostic(Diagnostic.Create(DesignRules.InconsistentOperatorOverloads, location, type.Name, declared, missing));
}
