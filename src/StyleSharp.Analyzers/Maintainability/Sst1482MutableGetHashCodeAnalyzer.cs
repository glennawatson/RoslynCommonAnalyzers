// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a read of mutable state from inside a <c>GetHashCode()</c> override (SST1482).
/// </summary>
/// <remarks>
/// <para>
/// A hash-based collection puts an object in a bucket when it is added and looks in that bucket when it is
/// asked for it. If a field the hash reads is reassigned afterwards the object's hash moves but the object
/// does not, and it can no longer be found — not even by a reference to itself. So the hash may only read
/// state that cannot change for the object's lifetime: a <c>readonly</c> or <c>const</c> field, a get-only
/// auto-property, or an <c>init</c>-only property, which construction fixes and nothing else can touch.
/// </para>
/// <para>
/// Only state this object owns is considered. A read through another object (<c>_next.Value</c>) is that
/// object's business, and a parameter or a local is not state at all. A <c>this.</c> or <c>base.</c> receiver
/// still names this instance, so both are followed. <c>base.GetHashCode()</c> and
/// <c>HashCode.Combine(...)</c> need no special case: neither names a field or a property, so the walk looks
/// straight through them to their arguments, which is exactly where the mutable reads hide.
/// </para>
/// <para>
/// The whole rule is behind a syntactic prepass — the name, the arity and the <c>override</c> modifier — which
/// is false for every method in a file but the one hash override, so the walk and the binds it performs never
/// run on ordinary code.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1482MutableGetHashCodeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the hash member this rule inspects.</summary>
    private const string GetHashCodeName = "GetHashCode";

    /// <summary>The contextual keyword whose operand is a name, not a value.</summary>
    private const string NameOfKeyword = "nameof";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.MutableGetHashCode);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Walks a hash override for reads of state that can change.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!IsHashOverride(method))
        {
            return;
        }

        if (GetBody(method) is not { } body)
        {
            return;
        }

        var scan = new HashScanState(context);
        Walk(body, ref scan);
    }

    /// <summary>Returns whether a declaration is the parameterless <c>GetHashCode()</c> override.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns><see langword="true"/> when the declaration is the hash override.</returns>
    /// <remarks>
    /// Ordered cheapest first: the name rejects almost every method on a single string comparison, so the
    /// modifier scan is only reached by a member that is already named like the hash.
    /// </remarks>
    private static bool IsHashOverride(MethodDeclarationSyntax method)
        => method.Identifier.ValueText == GetHashCodeName
            && method.ParameterList.Parameters.Count == 0
            && ModifierListHelper.Contains(method.Modifiers, SyntaxKind.OverrideKeyword);

    /// <summary>Gets the code a hash override runs, whichever body form it uses.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns>The body, or <see langword="null"/> when the override declares none.</returns>
    private static SyntaxNode? GetBody(MethodDeclarationSyntax method)
        => (SyntaxNode?)method.Body ?? method.ExpressionBody?.Expression;

    /// <summary>Visits a hash override's body, binding only the names that could name this object's state.</summary>
    /// <param name="node">The node to visit.</param>
    /// <param name="scan">The running scan state.</param>
    /// <remarks>
    /// A hand-written preorder walk rather than <c>DescendantNodes()</c>, both to avoid the iterator and
    /// because the decision to descend is what keeps the binds down: the member half of an access on another
    /// object, the operand of a <c>nameof</c> and the member names of an object initializer all name something
    /// other than this instance's state, and none of them is bound.
    /// </remarks>
    private static void Walk(SyntaxNode node, ref HashScanState scan)
    {
        switch (node)
        {
            // 'nameof(_field)' yields a name at compile time; it never reads the field.
            case InvocationExpressionSyntax invocation when IsNameOf(invocation):
                return;

            // 'this.X' and 'base.X' name this object's state. Anything else — 'other.X', 'Type.X' — names
            // state that belongs elsewhere, so only the receiver is followed and the member half is dropped.
            case MemberAccessExpressionSyntax memberAccess:
            {
                WalkMemberAccess(memberAccess, ref scan);
                return;
            }

            // The member half of '?.', whose receiver the conditional access already offered up.
            case MemberBindingExpressionSyntax:
                return;

            // 'Member = value' in an initializer names a member of the object being built, not of this one,
            // so only the values are followed.
            case InitializerExpressionSyntax initializer when IsMemberInitializer(initializer):
            {
                WalkInitializerValues(initializer, ref scan);
                return;
            }

            // A property pattern's name belongs to the type being matched, and an argument or member name
            // belongs to the callee.
            case SubpatternSyntax subpattern:
            {
                Walk(subpattern.Pattern, ref scan);
                return;
            }

            case NameColonSyntax:
            case NameEqualsSyntax:
            case QualifiedNameSyntax:
            case AliasQualifiedNameSyntax:
                return;

            case SimpleNameSyntax name:
            {
                Inspect(name, ref scan);
                return;
            }
        }

        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].AsNode() is { } child)
            {
                Walk(child, ref scan);
            }
        }
    }

    /// <summary>Follows a member access, binding its member half only when it names this object's state.</summary>
    /// <param name="memberAccess">The member access.</param>
    /// <param name="scan">The running scan state.</param>
    private static void WalkMemberAccess(MemberAccessExpressionSyntax memberAccess, ref HashScanState scan)
    {
        if (memberAccess.Expression is ThisExpressionSyntax or BaseExpressionSyntax)
        {
            Inspect(memberAccess.Name, ref scan);
            return;
        }

        Walk(memberAccess.Expression, ref scan);
    }

    /// <summary>Follows only the assigned values of an object or <c>with</c> initializer.</summary>
    /// <param name="initializer">The initializer.</param>
    /// <param name="scan">The running scan state.</param>
    private static void WalkInitializerValues(InitializerExpressionSyntax initializer, ref HashScanState scan)
    {
        var expressions = initializer.Expressions;
        for (var i = 0; i < expressions.Count; i++)
        {
            var expression = expressions[i];
            Walk(expression is AssignmentExpressionSyntax assignment ? assignment.Right : expression, ref scan);
        }
    }

    /// <summary>Reports a name that reads state which can change after the object is hashed.</summary>
    /// <param name="name">The name to bind.</param>
    /// <param name="scan">The running scan state.</param>
    /// <remarks>
    /// One mutable member is one defect however many times the hash happens to read it: a member named
    /// twice in the same expression — once in a null test, once in the value — would otherwise put two
    /// squiggles on one line for a single thing to fix. The first read is reported and the rest are
    /// skipped.
    /// </remarks>
    private static void Inspect(SimpleNameSyntax name, ref HashScanState scan)
    {
        var context = scan.Context;
        var symbol = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol;
        var mutable = symbol switch
        {
            IFieldSymbol field when IsMutable(field) => symbol,
            IPropertySymbol property when IsMutable(property) => symbol,
            _ => null,
        };

        if (mutable is null || !scan.TryMarkReported(mutable))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.MutableGetHashCode,
            name.GetLocation(),
            mutable.Name));
    }

    /// <summary>Returns whether a field can hold a different value later than it did when it was hashed.</summary>
    /// <param name="field">The bound field.</param>
    /// <returns><see langword="true"/> when the field is neither <c>readonly</c> nor <c>const</c>.</returns>
    /// <remarks>
    /// A <c>static</c> field is judged the same way. It is not this object's state, but the hash it feeds is:
    /// reassign it and every object hashed against it is lost, which is the same bug from further away.
    /// </remarks>
    private static bool IsMutable(IFieldSymbol field) => !field.IsReadOnly && !field.IsConst;

    /// <summary>Returns whether a property can hand back a different value later than it did when it was hashed.</summary>
    /// <param name="property">The bound property.</param>
    /// <returns><see langword="true"/> when the property has a setter that is not <c>init</c>-only.</returns>
    /// <remarks>
    /// A get-only property and an <c>init</c>-only one are both closed after construction, so both are safe to
    /// hash. Only a settable property can move under a live object.
    /// </remarks>
    private static bool IsMutable(IPropertySymbol property) => property.SetMethod is { IsInitOnly: false };

    /// <summary>Returns whether an invocation is <c>nameof(...)</c>.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns><see langword="true"/> when the operand is a name rather than a value.</returns>
    private static bool IsNameOf(InvocationExpressionSyntax invocation)
        => invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: NameOfKeyword };

    /// <summary>Returns whether an initializer assigns members of the object being built.</summary>
    /// <param name="initializer">The initializer.</param>
    /// <returns><see langword="true"/> for an object initializer or a <c>with</c> initializer.</returns>
    private static bool IsMemberInitializer(InitializerExpressionSyntax initializer)
        => initializer.IsKind(SyntaxKind.ObjectInitializerExpression)
            || initializer.IsKind(SyntaxKind.WithInitializerExpression);

    /// <summary>Carries the context through the walk and remembers which members have been reported.</summary>
    /// <param name="Context">The syntax node context.</param>
    /// <remarks>
    /// The list is created on the first report, so a hash override with nothing wrong with it — which is
    /// almost all of them — walks its body without allocating.
    /// </remarks>
    private record struct HashScanState(SyntaxNodeAnalysisContext Context)
    {
        /// <summary>Gets or sets the members already reported, created on first use.</summary>
        private List<ISymbol>? Reported { get; set; }

        /// <summary>Records a member as reported, and says whether it is the first time.</summary>
        /// <param name="symbol">The mutable member.</param>
        /// <returns><see langword="true"/> when the member has not been reported yet.</returns>
        public bool TryMarkReported(ISymbol symbol)
        {
            if (Reported is { } reported)
            {
                for (var i = 0; i < reported.Count; i++)
                {
                    if (SymbolEqualityComparer.Default.Equals(reported[i], symbol))
                    {
                        return false;
                    }
                }
            }
            else
            {
                reported = new List<ISymbol>(2);
                Reported = reported;
            }

            reported.Add(symbol);
            return true;
        }
    }
}
