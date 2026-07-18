// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an instance member — a constructor, method, finalizer, or a property, indexer, or event
/// accessor — that assigns one of its own type's static fields (SST2402).
/// </summary>
/// <remarks>
/// <para>
/// Four shapes are deliberately not reported, because each of them means something other than "one
/// instance redefines the field for all the others":
/// </para>
/// <list type="bullet">
/// <item><description>A <b>static member</b> — a static constructor, method, or accessor is the correct
/// place to set static state: it acts for the type, not for one instance.</description></item>
/// <item><description>A <b>compound assignment or increment</b> (<c>Count++</c>, <c>_total += x</c>). It
/// accumulates rather than overwrites: an instance counter is a deliberate, if racy, design, and the value
/// the last instance wrote is not the whole story.</description></item>
/// <item><description>A <b>lazily-initialised guard</b> — <c>Instance ??= this;</c>, or an assignment whose
/// enclosing <c>if</c> tests the same field. The check is the author saying "only the first one wins", which
/// is the opposite of the bug this rule describes.</description></item>
/// <item><description>A <b>[ThreadStatic]</b> field, which is per-thread state deliberately kept off the
/// instance.</description></item>
/// </list>
/// <para>
/// A write inside a lambda or a local function declared in a <b>constructor</b> is not reported either: that
/// code runs when the delegate is invoked, not while the object is being built. In any other instance member
/// the same write is reported — the delegate is part of how that member mutates type-wide state, whenever
/// it runs.
/// </para>
/// <para>
/// The clean path binds nothing. The member's assignments are found on syntax, the containing type is
/// resolved only once one is present, and an assignment target is bound only after its name has matched a
/// static field the type actually declares — which most assignments in a member never do.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2402StaticFieldWrittenInConstructorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The attribute marking a field as per-thread state.</summary>
    private const string ThreadStaticAttributeName = "ThreadStaticAttribute";

    /// <summary>The member declaration kinds that can hold instance code.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.ConstructorDeclaration,
        SyntaxKind.DestructorDeclaration,
        SyntaxKind.MethodDeclaration,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.IndexerDeclaration,
        SyntaxKind.EventDeclaration);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.StaticFieldWrittenInConstructor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Analyzes one instance member for writes to its type's static fields.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.Parent is not TypeDeclarationSyntax typeDeclaration)
        {
            return;
        }

        switch (context.Node)
        {
            case ConstructorDeclarationSyntax constructor:
            {
                if (!ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword))
                {
                    Scan(context, typeDeclaration, GetBody(constructor), excludeDeferredWrites: true);
                }

                break;
            }

            case BaseMethodDeclarationSyntax method:
            {
                if (!ModifierListHelper.Contains(method.Modifiers, SyntaxKind.StaticKeyword))
                {
                    Scan(context, typeDeclaration, GetBody(method), excludeDeferredWrites: false);
                }

                break;
            }

            case BasePropertyDeclarationSyntax property:
            {
                if (!ModifierListHelper.Contains(property.Modifiers, SyntaxKind.StaticKeyword))
                {
                    ScanAccessors(context, typeDeclaration, property);
                }

                break;
            }
        }
    }

    /// <summary>Scans each body a property, indexer, or event declares.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="typeDeclaration">The member's containing type declaration.</param>
    /// <param name="property">The property, indexer, or event.</param>
    private static void ScanAccessors(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax typeDeclaration,
        BasePropertyDeclarationSyntax property)
    {
        var expressionBody = property switch
        {
            PropertyDeclarationSyntax { ExpressionBody: { } getter } => getter,
            IndexerDeclarationSyntax { ExpressionBody: { } getter } => getter,
            _ => null,
        };
        Scan(context, typeDeclaration, expressionBody, excludeDeferredWrites: false);

        if (property.AccessorList is not { } accessorList)
        {
            return;
        }

        var accessors = accessorList.Accessors;
        for (var i = 0; i < accessors.Count; i++)
        {
            var accessor = accessors[i];
            Scan(context, typeDeclaration, (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody, excludeDeferredWrites: false);
        }
    }

    /// <summary>Scans one member body for assignments to the type's static fields.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="typeDeclaration">The member's containing type declaration.</param>
    /// <param name="body">The body to scan, or <see langword="null"/> when the member has none.</param>
    /// <param name="excludeDeferredWrites">Whether writes inside lambdas and local functions run outside this
    /// member's story, as they do for a constructor.</param>
    private static void Scan(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax typeDeclaration,
        SyntaxNode? body,
        bool excludeDeferredWrites)
    {
        if (body is null)
        {
            return;
        }

        var scan = new MemberScan(context, typeDeclaration, body, excludeDeferredWrites);
        DescendantTraversalHelper.VisitDescendants<AssignmentExpressionSyntax, MemberScan>(body, ref scan, VisitAssignment);
    }

    /// <summary>Reports one assignment when it overwrites a static field of the member's own type.</summary>
    /// <param name="assignment">The assignment being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="true"/>, so the whole member is examined.</returns>
    private static bool VisitAssignment(AssignmentExpressionSyntax assignment, ref MemberScan state)
    {
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            || GetAssignedName(assignment.Left) is not { } name
            || (state.ExcludeDeferredWrites && IsDeferred(assignment, state.ScanRoot)))
        {
            return true;
        }

        if (state.ResolveContainingType() is not { } containingType
            || !DeclaresAssignableStaticField(containingType, name)
            || !IsUnguarded(assignment, name, state.ScanRoot)
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
    /// <param name="containingType">The member's type.</param>
    /// <param name="name">The assigned name.</param>
    /// <returns><see langword="true"/> when the type declares a matching, writable static field.</returns>
    /// <remarks>
    /// A name comparison against the type's own members, so the assignment target is bound only when the
    /// name really could be a static field. <c>const</c> and <c>readonly</c> fields cannot be assigned from
    /// an instance member at all, so a name that matches one of those is not a candidate.
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
    /// <param name="scanRoot">The member body being scanned.</param>
    /// <returns><see langword="true"/> when nothing on the way in tests the field's current value.</returns>
    /// <remarks>
    /// <c>if (Instance is null) { Instance = this; }</c> is a first-one-wins initializer, not a race to
    /// overwrite. The test may be anywhere between the assignment and the member body, so every enclosing
    /// <c>if</c> is checked for a mention of the field.
    /// </remarks>
    private static bool IsUnguarded(AssignmentExpressionSyntax assignment, string name, SyntaxNode scanRoot)
    {
        for (SyntaxNode? node = assignment; node is not null; node = node.Parent)
        {
            if (node == scanRoot)
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

    /// <summary>Returns whether an assignment runs later than the member that contains it.</summary>
    /// <param name="assignment">The assignment.</param>
    /// <param name="scanRoot">The member body being scanned.</param>
    /// <returns><see langword="true"/> when the assignment sits inside a lambda or a local function.</returns>
    private static bool IsDeferred(AssignmentExpressionSyntax assignment, SyntaxNode scanRoot)
    {
        for (SyntaxNode? node = assignment.Parent; node is not null && node != scanRoot; node = node.Parent)
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
    /// <returns>The written name, or <see langword="null"/> when the target names an instance member of the object itself.</returns>
    /// <remarks><c>this.X = …</c> cannot be a static field, so it never reaches the semantic model.</remarks>
    private static string? GetAssignedName(ExpressionSyntax target) => target switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } => null,
        MemberAccessExpressionSyntax { Name: { } name } => name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Gets a constructor's, method's, or finalizer's body, in whichever form it is written.</summary>
    /// <param name="member">The member.</param>
    /// <returns>The body, or <see langword="null"/> when the member has none.</returns>
    private static SyntaxNode? GetBody(BaseMethodDeclarationSyntax member)
        => (SyntaxNode?)member.Body ?? member.ExpressionBody;

    /// <summary>The state threaded through a member body's assignment scan.</summary>
    /// <param name="Context">The syntax node context.</param>
    /// <param name="TypeDeclaration">The member's containing type declaration.</param>
    /// <param name="ScanRoot">The member body being scanned.</param>
    /// <param name="ExcludeDeferredWrites">Whether writes inside lambdas and local functions run outside this
    /// member's story, as they do for a constructor.</param>
    private record struct MemberScan(
        SyntaxNodeAnalysisContext Context,
        TypeDeclarationSyntax TypeDeclaration,
        SyntaxNode ScanRoot,
        bool ExcludeDeferredWrites)
    {
        /// <summary>Gets or sets a value indicating whether the containing type has been resolved yet.</summary>
        private bool Resolved { get; set; }

        /// <summary>Gets or sets the resolved containing type.</summary>
        private INamedTypeSymbol? ContainingType { get; set; }

        /// <summary>Resolves the member's type, binding it at most once per scanned body.</summary>
        /// <returns>The containing type, or <see langword="null"/> when it does not bind.</returns>
        public INamedTypeSymbol? ResolveContainingType()
        {
            if (Resolved)
            {
                return ContainingType;
            }

            Resolved = true;
            ContainingType = Context.SemanticModel.GetDeclaredSymbol(TypeDeclaration, Context.CancellationToken);
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
