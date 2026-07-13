// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a local variable that is declared and never read (SST1497). A local that is written — even
/// written repeatedly — but never read is still unused: the value it holds goes nowhere.
/// </summary>
/// <remarks>
/// <para>
/// Reads are found by name, not by symbol: a plain identifier inside the local's scope can only mean that
/// local, because C# does not let a nested declaration shadow it, and every read of a local must spell its
/// name. That keeps the whole rule off the semantic model. The scan stops at the first read, so a used
/// local — the overwhelming majority — costs one partial walk of its enclosing block and nothing else.
/// </para>
/// <para>
/// The one shape that is a write and not a read is <c>x = value;</c> written as a statement of its own.
/// Anything else — a compound assignment, an increment, an <c>out</c> or <c>ref</c> argument, an assignment
/// used for its value — counts as a read, because the fix could not safely unpick it. Reading the local
/// inside a nested lambda or local function counts too: the scan covers the whole enclosing block, so a
/// captured local is seen exactly where it is used.
/// </para>
/// <para>
/// Declarations whose lifetime is the point are left alone: a <c>using</c> declaration, a <c>ref</c> local,
/// a <c>foreach</c> variable, a pattern variable and a <c>fixed</c> buffer are all either excluded by shape
/// or never registered. An <c>out var</c> is reported, because the callee assigning it is not the caller
/// reading it — the fix turns it into a discard.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1497UnusedLocalAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the discard, which declares nothing worth reporting.</summary>
    private const string DiscardName = "_";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.UnusedLocal);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
        context.RegisterSyntaxNodeAction(AnalyzeOutVariable, SyntaxKind.DeclarationExpression);
    }

    /// <summary>Gets the syntax that bounds where a local can be read.</summary>
    /// <param name="node">The declaration.</param>
    /// <returns>The enclosing block, expression body, member, or compilation unit.</returns>
    /// <remarks>
    /// The scope is deliberately widened rather than narrowed. A local declared straight into a switch
    /// section is in scope across the whole switch, and an <c>out var</c> declared in a governing expression
    /// outlives the statement that introduced it, so the walk climbs past both to the enclosing block. A
    /// scope that is too wide can only find more reads, which can only silence a report; a scope that is too
    /// narrow would invent one.
    /// </remarks>
    internal static SyntaxNode? GetScope(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is GlobalStatementSyntax)
            {
                continue;
            }

            if (current is BlockSyntax or ArrowExpressionClauseSyntax or MemberDeclarationSyntax or CompilationUnitSyntax)
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>Returns whether an identifier reads the local it names.</summary>
    /// <param name="identifier">The identifier to classify.</param>
    /// <returns><see langword="true"/> for every use except a standalone <c>x = value;</c> statement.</returns>
    /// <remarks>
    /// The name of a member access (<c>other.x</c>), of a named argument (<c>x: 1</c>) and of a qualified
    /// name is not a reference to a local at all, so those are not reads either.
    /// </remarks>
    internal static bool IsReadReference(IdentifierNameSyntax identifier)
        => !IsNameOfSomethingElse(identifier) && !IsDeadWriteTarget(identifier, out _);

    /// <summary>Returns whether an identifier is the target of an assignment written as its own statement.</summary>
    /// <param name="identifier">The identifier to classify.</param>
    /// <param name="statement">The assignment statement when the identifier is its target.</param>
    /// <returns><see langword="true"/> for <c>x = value;</c>, which stores a value nothing goes on to read.</returns>
    /// <remarks>
    /// Only a simple assignment counts. A compound assignment reads before it writes, and an assignment used
    /// for its own value (<c>y = x = 1</c>) hands the value on, so both are reads and stop the report.
    /// </remarks>
    internal static bool IsDeadWriteTarget(IdentifierNameSyntax identifier, out ExpressionStatementSyntax? statement)
    {
        if (identifier.Parent is AssignmentExpressionSyntax assignment
            && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && assignment.Left == identifier
            && assignment.Parent is ExpressionStatementSyntax expressionStatement)
        {
            statement = expressionStatement;
            return true;
        }

        statement = null;
        return false;
    }

    /// <summary>Reports every variable of a local declaration that nothing reads.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context)
    {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;
        if (!declaration.UsingKeyword.IsKind(SyntaxKind.None) || declaration.Declaration.Type is RefTypeSyntax)
        {
            return;
        }

        if (GetScope(declaration) is not { } scope)
        {
            return;
        }

        var variables = declaration.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            var identifier = variables[i].Identifier;
            if (identifier.ValueText == DiscardName || IsRead(scope, identifier.ValueText))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(MaintainabilityRules.UnusedLocal, identifier.GetLocation(), identifier.ValueText));
        }
    }

    /// <summary>Reports an <c>out var</c> declaration whose variable nothing reads.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <remarks>
    /// Only the <c>out</c> argument shape is considered. The same syntax spells a deconstruction
    /// (<c>var (a, b) = pair</c>), where the variables are the point of the statement.
    /// </remarks>
    private static void AnalyzeOutVariable(SyntaxNodeAnalysisContext context)
    {
        var declaration = (DeclarationExpressionSyntax)context.Node;
        if (declaration.Designation is not SingleVariableDesignationSyntax designation
            || declaration.Parent is not ArgumentSyntax argument
            || !argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))
        {
            return;
        }

        var identifier = designation.Identifier;
        if (identifier.ValueText == DiscardName || GetScope(declaration) is not { } scope || IsRead(scope, identifier.ValueText))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(MaintainabilityRules.UnusedLocal, identifier.GetLocation(), identifier.ValueText));
    }

    /// <summary>Returns whether anything in the scope reads the named local.</summary>
    /// <param name="scope">The syntax that bounds the local.</param>
    /// <param name="name">The local's name.</param>
    /// <returns><see langword="true"/> as soon as one read is found.</returns>
    private static bool IsRead(SyntaxNode scope, string name)
    {
        var state = new ReadScanState(name);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, ReadScanState>(
            scope,
            ref state,
            static (IdentifierNameSyntax identifier, ref ReadScanState scan) => scan.Observe(identifier));
        return state.Read;
    }

    /// <summary>Returns whether an identifier names a member, an argument, or a namespace part rather than a local.</summary>
    /// <param name="identifier">The identifier to classify.</param>
    /// <returns><see langword="true"/> when the identifier could never bind to a local.</returns>
    private static bool IsNameOfSomethingElse(IdentifierNameSyntax identifier) => identifier.Parent switch
    {
        MemberAccessExpressionSyntax member => member.Name == identifier,
        MemberBindingExpressionSyntax binding => binding.Name == identifier,
        QualifiedNameSyntax qualified => qualified.Right == identifier,
        AliasQualifiedNameSyntax aliased => aliased.Name == identifier,
        NameColonSyntax or NameEqualsSyntax => true,
        _ => false,
    };

    /// <summary>Tracks whether one named local has been read, and stops the walk as soon as it has.</summary>
    private struct ReadScanState : IEquatable<ReadScanState>
    {
        /// <summary>The local's name.</summary>
        private readonly string _name;

        /// <summary>Initializes a new instance of the <see cref="ReadScanState"/> struct.</summary>
        /// <param name="name">The local's name.</param>
        public ReadScanState(string name)
        {
            _name = name;
            Read = false;
        }

        /// <summary>Gets a value indicating whether a read has been seen.</summary>
        public bool Read { get; private set; }

        /// <summary>Returns whether two scan states are equivalent.</summary>
        /// <param name="other">The other state.</param>
        /// <returns><see langword="true"/> when the tracked state is equal.</returns>
        public readonly bool Equals(ReadScanState other) => Read == other.Read && _name == other._name;

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj) => obj is ReadScanState other && Equals(other);

        /// <inheritdoc/>
        public override readonly int GetHashCode() => unchecked((_name.GetHashCode() * 397) ^ (Read ? 1 : 0));

        /// <summary>Observes one identifier and returns whether scanning should continue.</summary>
        /// <param name="identifier">The identifier.</param>
        /// <returns><see langword="false"/> once a read has been seen.</returns>
        public bool Observe(IdentifierNameSyntax identifier)
        {
            if (identifier.Identifier.ValueText == _name && IsReadReference(identifier))
            {
                Read = true;
            }

            return !Read;
        }
    }
}
