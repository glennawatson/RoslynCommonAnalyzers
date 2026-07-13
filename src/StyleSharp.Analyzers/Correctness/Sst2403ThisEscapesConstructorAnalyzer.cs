// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a constructor that hands <c>this</c> to something else before it has finished building the object
/// (SST2403): passing it as an argument, storing it where another object or the type itself can reach it, or
/// subscribing a closure over it to somebody else's event.
/// </summary>
/// <remarks>
/// <para>
/// Three uses of <c>this</c> inside a constructor are not escapes and are never reported:
/// </para>
/// <list type="bullet">
/// <item><description><b>Member access on itself</b> — <c>this.Field = value</c>, <c>this.Method()</c>,
/// <c>this[i]</c>. The object is talking to itself; nothing leaves.</description></item>
/// <item><description><b>A base or chained constructor initializer</b> — <c>: base(this)</c>. The receiver
/// is the same object's own base constructor, which is part of building it.</description></item>
/// <item><description><b>Storing it in the object's own instance state</b> — <c>_self = this</c>, or a
/// local. Neither reference outlives the object it points at.</description></item>
/// </list>
/// <para>
/// A lambda counts. <c>service.Updated += (s, e) =&gt; Refresh();</c> publishes the closure — and the
/// half-built object inside it — to whatever holds that event, which is exactly the failure the rule is
/// about. A <c>this</c> reached only as the receiver of a member access is not an escape on its own, but the
/// same access inside an escaping lambda is, because the lambda carries the object with it. Only a written
/// <c>this</c> is seen: a lambda that captures the object through an unqualified member name is not
/// reported, because there is no <c>this</c> in the source to point at.
/// </para>
/// <para>
/// The clean path binds nothing. Each <c>this</c> is classified by walking up from it on syntax alone, and
/// an assignment target is bound only once <c>this</c> is known to be what is being stored.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2403ThisEscapesConstructorAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.ThisEscapesConstructor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConstructorDeclaration);
    }

    /// <summary>Analyzes one constructor for a <c>this</c> that leaves it.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;
        if (ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword)
            || GetBody(constructor) is not { } body)
        {
            return;
        }

        var scan = new EscapeScan(context, body, constructor.Identifier.ValueText);
        DescendantTraversalHelper.VisitDescendants<ThisExpressionSyntax, EscapeScan>(body, ref scan, VisitThis);
    }

    /// <summary>Classifies one <c>this</c> and reports it when it escapes.</summary>
    /// <param name="thisExpression">The <c>this</c> being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="true"/>, so the whole constructor is examined.</returns>
    private static bool VisitThis(ThisExpressionSyntax thisExpression, ref EscapeScan state)
    {
        var escaping = GetEscapingExpression(thisExpression, state.Body);
        if (escaping is null || !IsHandedOver(escaping, state.Context))
        {
            return true;
        }

        state.Context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.ThisEscapesConstructor,
            escaping.GetLocation(),
            state.TypeName));
        return true;
    }

    /// <summary>Gets the expression that would carry the object out, if this one were handed over.</summary>
    /// <param name="thisExpression">The <c>this</c> expression.</param>
    /// <param name="body">The constructor body.</param>
    /// <returns>
    /// The enclosing closure when the object is captured, <c>this</c> itself when it is used directly, or
    /// <see langword="null"/> when the object is only reading one of its own members.
    /// </returns>
    /// <remarks>
    /// Reporting the closure rather than each <c>this</c> inside it keeps one escape to one diagnostic: a
    /// lambda that touches the half-built object three times still only publishes it once.
    /// </remarks>
    private static ExpressionSyntax? GetEscapingExpression(ThisExpressionSyntax thisExpression, SyntaxNode body)
    {
        if (GetOutermostClosure(thisExpression, body) is { } closure)
        {
            return IsFirstThisIn(closure, thisExpression) ? closure : null;
        }

        return IsOwnMemberAccess(thisExpression) ? null : thisExpression;
    }

    /// <summary>Returns whether an expression is handed to something that outlives the constructor.</summary>
    /// <param name="escaping">The expression that would carry the object out.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> when the expression is passed as an argument or stored externally.</returns>
    private static bool IsHandedOver(ExpressionSyntax escaping, SyntaxNodeAnalysisContext context) => escaping.Parent switch
    {
        ArgumentSyntax argument => argument.Expression == escaping,
        AssignmentExpressionSyntax assignment => assignment.Right == escaping
            && IsStoringAssignment(assignment)
            && IsExternalTarget(assignment.Left, context),
        _ => false,
    };

    /// <summary>Returns whether an assignment stores its right-hand side rather than computing with it.</summary>
    /// <param name="assignment">The assignment.</param>
    /// <returns><see langword="true"/> for a plain store and for an event subscription.</returns>
    private static bool IsStoringAssignment(AssignmentExpressionSyntax assignment)
        => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) || assignment.IsKind(SyntaxKind.AddAssignmentExpression);

    /// <summary>Returns whether an assignment target outlives the object being built.</summary>
    /// <param name="target">The assignment's left-hand side.</param>
    /// <param name="context">The syntax node context.</param>
    /// <returns><see langword="true"/> for a static member, or a member of another object.</returns>
    /// <remarks>
    /// A local, a parameter and the object's own instance members all die with — or belong to — the object,
    /// so storing <c>this</c> in one of them publishes nothing. A static field, or a field or event on
    /// another object, is reachable after the constructor returns, and by any thread that can see it.
    /// </remarks>
    private static bool IsExternalTarget(ExpressionSyntax target, SyntaxNodeAnalysisContext context)
    {
        var qualifiedByAnotherObject = target switch
        {
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } => false,
            MemberAccessExpressionSyntax => true,
            _ => false,
        };

        if (target is not (IdentifierNameSyntax or MemberAccessExpressionSyntax))
        {
            return false;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(target, context.CancellationToken).Symbol;
        if (symbol is not (IFieldSymbol or IPropertySymbol or IEventSymbol))
        {
            return false;
        }

        return qualifiedByAnotherObject || symbol.IsStatic;
    }

    /// <summary>Returns whether a <c>this</c> is only being used to reach one of the object's own members.</summary>
    /// <param name="thisExpression">The <c>this</c> expression.</param>
    /// <returns><see langword="true"/> for <c>this.X</c>, <c>this[i]</c> and <c>this?.X</c>.</returns>
    private static bool IsOwnMemberAccess(ThisExpressionSyntax thisExpression) => thisExpression.Parent switch
    {
        MemberAccessExpressionSyntax access => access.Expression == thisExpression,
        ElementAccessExpressionSyntax access => access.Expression == thisExpression,
        ConditionalAccessExpressionSyntax access => access.Expression == thisExpression,
        _ => false,
    };

    /// <summary>Gets the outermost closure between a <c>this</c> and the constructor body.</summary>
    /// <param name="thisExpression">The <c>this</c> expression.</param>
    /// <param name="body">The constructor body.</param>
    /// <returns>The closure that would carry the object, or <see langword="null"/> when there is none.</returns>
    /// <remarks>
    /// A local function is not a closure until it is converted to a delegate, and the conversion — not the
    /// declaration — is where the object would escape. That is a chain this rule does not follow, so a
    /// <c>this</c> inside a local function is left alone.
    /// </remarks>
    private static AnonymousFunctionExpressionSyntax? GetOutermostClosure(ThisExpressionSyntax thisExpression, SyntaxNode body)
    {
        AnonymousFunctionExpressionSyntax? closure = null;
        for (SyntaxNode? node = thisExpression.Parent; node is not null && node != body; node = node.Parent)
        {
            if (node is LocalFunctionStatementSyntax)
            {
                return null;
            }

            if (node is AnonymousFunctionExpressionSyntax function)
            {
                closure = function;
            }
        }

        return closure;
    }

    /// <summary>Returns whether a <c>this</c> is the first one inside a closure.</summary>
    /// <param name="closure">The closure.</param>
    /// <param name="thisExpression">The <c>this</c> expression.</param>
    /// <returns><see langword="true"/> when this occurrence is the one the closure is reported on.</returns>
    private static bool IsFirstThisIn(AnonymousFunctionExpressionSyntax closure, ThisExpressionSyntax thisExpression)
    {
        var scan = default(FirstThisScan);
        DescendantTraversalHelper.VisitDescendants<ThisExpressionSyntax, FirstThisScan>(closure, ref scan, VisitFirstThis);
        return scan.First == thisExpression;
    }

    /// <summary>Records the first <c>this</c> in a closure.</summary>
    /// <param name="thisExpression">The <c>this</c> being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/>, which stops the walk at the first occurrence.</returns>
    private static bool VisitFirstThis(ThisExpressionSyntax thisExpression, ref FirstThisScan state)
    {
        state.First = thisExpression;
        return false;
    }

    /// <summary>Gets a constructor's body, in whichever form it is written.</summary>
    /// <param name="constructor">The constructor.</param>
    /// <returns>The body, or <see langword="null"/> when the constructor has none.</returns>
    /// <remarks>
    /// Only the body is walked, so a <c>: base(this)</c> initializer is out of scope by construction: it
    /// hands the object to its own base constructor, which is part of building it.
    /// </remarks>
    private static SyntaxNode? GetBody(ConstructorDeclarationSyntax constructor)
        => (SyntaxNode?)constructor.Body ?? constructor.ExpressionBody;

    /// <summary>The state threaded through a constructor's escape scan.</summary>
    /// <param name="Context">The syntax node context.</param>
    /// <param name="Body">The constructor body.</param>
    /// <param name="TypeName">The name of the type being constructed.</param>
    private readonly record struct EscapeScan(SyntaxNodeAnalysisContext Context, SyntaxNode Body, string TypeName);

    /// <summary>The state threaded through the search for a closure's first <c>this</c>.</summary>
    private record struct FirstThisScan
    {
        /// <summary>Gets or sets the first <c>this</c> found.</summary>
        public ThisExpressionSyntax? First { get; set; }
    }
}
