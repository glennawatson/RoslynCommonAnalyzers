// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an instance constructor that assigns one of its own type's static fields (SST2402).
/// </summary>
/// <remarks>
/// <para>
/// Four shapes are deliberately not reported, because each of them means something other than "the newest
/// object redefines the field":
/// </para>
/// <list type="bullet">
/// <item><description>A <b>static constructor</b>, which is the correct place to set static state — it runs
/// once, before anything can observe the field.</description></item>
/// <item><description>A <b>compound assignment or increment</b> (<c>Count++</c>, <c>_total += x</c>). It
/// accumulates rather than overwrites: an instance counter is a deliberate, if racy, design, and the value
/// the last constructor wrote is not the whole story.</description></item>
/// <item><description>A <b>lazily-initialised guard</b> — <c>Instance ??= this;</c>, or an assignment whose
/// enclosing <c>if</c> tests the same field. The check is the author saying "only the first one wins", which
/// is the opposite of the bug this rule describes.</description></item>
/// <item><description>A <b>[ThreadStatic]</b> field, which is per-thread state deliberately kept off the
/// instance.</description></item>
/// </list>
/// <para>
/// A write inside a lambda or a local function is not reported either: that code runs when the delegate is
/// invoked, not while the object is being built.
/// </para>
/// <para>
/// The clean path binds nothing. The constructor's assignments are found on syntax, the containing type is
/// resolved only once one is present, and an assignment target is bound only after its name has matched a
/// static field the type actually declares — which most assignments in a constructor never do.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2402StaticFieldWrittenInConstructorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The attribute marking a field as per-thread state.</summary>
    private const string ThreadStaticAttributeName = "ThreadStaticAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.StaticFieldWrittenInConstructor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConstructorDeclaration);
    }

    /// <summary>Analyzes one constructor for writes to its type's static fields.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;
        if (ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword)
            || GetBody(constructor) is not { } body)
        {
            return;
        }

        var scan = new ConstructorScan(context, constructor);
        DescendantTraversalHelper.VisitDescendants<AssignmentExpressionSyntax, ConstructorScan>(body, ref scan, VisitAssignment);
    }

    /// <summary>Reports one assignment when it overwrites a static field of the constructor's own type.</summary>
    /// <param name="assignment">The assignment being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="true"/>, so the whole constructor is examined.</returns>
    private static bool VisitAssignment(AssignmentExpressionSyntax assignment, ref ConstructorScan state)
    {
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            || GetAssignedName(assignment.Left) is not { } name
            || IsDeferred(assignment, state.Constructor))
        {
            return true;
        }

        if (state.ResolveContainingType() is not { } containingType
            || !DeclaresAssignableStaticField(containingType, name)
            || !IsUnguarded(assignment, name)
            || ResolveField(state.Context, assignment.Left) is not { } field)
        {
            return true;
        }

        state.Context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.StaticFieldWrittenInConstructor,
            assignment.Left.GetLocation(),
            field.Name));
        return true;
    }

    /// <summary>Returns whether a static field with this name is one the type could assign here.</summary>
    /// <param name="containingType">The constructor's type.</param>
    /// <param name="name">The assigned name.</param>
    /// <returns><see langword="true"/> when the type declares a matching, writable static field.</returns>
    /// <remarks>
    /// A name comparison against the type's own members, so the assignment target is bound only when the
    /// name really could be a static field. <c>const</c> and <c>readonly</c> fields cannot be assigned from
    /// an instance constructor at all, so a name that matches one of those is not a candidate.
    /// </remarks>
    private static bool DeclaresAssignableStaticField(INamedTypeSymbol containingType, string name)
    {
        var members = containingType.GetMembers(name);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IFieldSymbol { IsStatic: true, IsConst: false, IsReadOnly: false } field
                && !HasThreadStaticAttribute(field))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Binds an assignment target to the static field it writes.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="target">The assignment's left-hand side.</param>
    /// <returns>The written field, or <see langword="null"/> when the target is anything else.</returns>
    /// <remarks>
    /// The name match is not proof: a local can shadow a field, and a field of another type can be reached
    /// through a member access. The bind settles it.
    /// </remarks>
    private static IFieldSymbol? ResolveField(SyntaxNodeAnalysisContext context, ExpressionSyntax target)
        => context.SemanticModel.GetSymbolInfo(target, context.CancellationToken).Symbol is
            IFieldSymbol { IsStatic: true, IsConst: false } field
            ? field
            : null;

    /// <summary>Returns whether a field is per-thread state.</summary>
    /// <param name="field">The field.</param>
    /// <returns><see langword="true"/> when the field carries <c>[ThreadStatic]</c>.</returns>
    private static bool HasThreadStaticAttribute(IFieldSymbol field)
    {
        var attributes = field.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            if (attributes[i].AttributeClass?.Name == ThreadStaticAttributeName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the assignment is unguarded, and so really does overwrite the field.</summary>
    /// <param name="assignment">The assignment.</param>
    /// <param name="name">The assigned name.</param>
    /// <returns><see langword="true"/> when nothing on the way in tests the field's current value.</returns>
    /// <remarks>
    /// <c>if (Instance is null) { Instance = this; }</c> is a first-one-wins initializer, not a race to
    /// overwrite. The test may be anywhere between the assignment and the constructor body, so every
    /// enclosing <c>if</c> is checked for a mention of the field.
    /// </remarks>
    private static bool IsUnguarded(AssignmentExpressionSyntax assignment, string name)
    {
        for (SyntaxNode? node = assignment; node is not null; node = node.Parent)
        {
            if (node is ConstructorDeclarationSyntax)
            {
                return true;
            }

            if (node is IfStatementSyntax { Condition: { } condition } && MentionsName(condition, name))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an expression mentions an identifier by name.</summary>
    /// <param name="expression">The expression to search.</param>
    /// <param name="name">The name to look for.</param>
    /// <returns><see langword="true"/> when the name appears.</returns>
    private static bool MentionsName(ExpressionSyntax expression, string name)
    {
        var scan = new NameScan(name);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, NameScan>(expression, ref scan, VisitName);
        return scan.Found || (expression is IdentifierNameSyntax self && self.Identifier.ValueText == name);
    }

    /// <summary>Records whether a name matches the one being looked for.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once the name is found, which stops the walk.</returns>
    private static bool VisitName(IdentifierNameSyntax identifier, ref NameScan state)
    {
        if (identifier.Identifier.ValueText != state.Name)
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>Returns whether an assignment runs later than the constructor that contains it.</summary>
    /// <param name="assignment">The assignment.</param>
    /// <param name="constructor">The constructor being analyzed.</param>
    /// <returns><see langword="true"/> when the assignment sits inside a lambda or a local function.</returns>
    private static bool IsDeferred(AssignmentExpressionSyntax assignment, ConstructorDeclarationSyntax constructor)
    {
        for (SyntaxNode? node = assignment.Parent; node is not null && node != constructor; node = node.Parent)
        {
            if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the name an assignment writes to.</summary>
    /// <param name="target">The assignment's left-hand side.</param>
    /// <returns>The written name, or <see langword="null"/> when the target names an instance member of the object being built.</returns>
    /// <remarks><c>this.X = …</c> cannot be a static field, so it never reaches the semantic model.</remarks>
    private static string? GetAssignedName(ExpressionSyntax target) => target switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } => null,
        MemberAccessExpressionSyntax { Name: { } name } => name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Gets a constructor's body, in whichever form it is written.</summary>
    /// <param name="constructor">The constructor.</param>
    /// <returns>The body, or <see langword="null"/> when the constructor has none.</returns>
    private static SyntaxNode? GetBody(ConstructorDeclarationSyntax constructor)
        => (SyntaxNode?)constructor.Body ?? constructor.ExpressionBody;

    /// <summary>The state threaded through a constructor's assignment scan.</summary>
    /// <param name="Context">The syntax node context.</param>
    /// <param name="Constructor">The constructor being analyzed.</param>
    private record struct ConstructorScan(SyntaxNodeAnalysisContext Context, ConstructorDeclarationSyntax Constructor)
    {
        /// <summary>Gets or sets a value indicating whether the containing type has been resolved yet.</summary>
        private bool Resolved { get; set; }

        /// <summary>Gets or sets the resolved containing type.</summary>
        private INamedTypeSymbol? ContainingType { get; set; }

        /// <summary>Resolves the constructor's type, binding it at most once per constructor.</summary>
        /// <returns>The containing type, or <see langword="null"/> when it does not bind.</returns>
        public INamedTypeSymbol? ResolveContainingType()
        {
            if (Resolved)
            {
                return ContainingType;
            }

            Resolved = true;
            ContainingType = Context.SemanticModel
                .GetDeclaredSymbol(Constructor, Context.CancellationToken)?
                .ContainingType;
            return ContainingType;
        }
    }

    /// <summary>The state threaded through a name search.</summary>
    /// <param name="Name">The name being looked for.</param>
    private record struct NameScan(string Name)
    {
        /// <summary>Gets or sets a value indicating whether the name was found.</summary>
        public bool Found { get; set; }
    }
}
