// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a declaration that reuses the name of a field or property already visible from an enclosing scope
/// (SST1484): a local, parameter or pattern variable that shadows a member, and a nested type's field or
/// property that shadows a containing type's static member. Reporting a field that hides an inherited field
/// of the same name is opt-in with <c>stylesharp.SST1484.check_base_types</c>.
/// </summary>
/// <remarks>
/// <para>
/// The constructor idiom is the thing this rule must never break. <c>this.name = name;</c> — and its bare
/// <c>name = name;</c>, expression-bodied and tuple spellings — is how C# construction is written, so a
/// parameter that is assigned to the member it shadows is never reported. The exemption is deliberately
/// phrased as "the parameter feeds the member", which also covers a setter, an initialize method, and a
/// constructor that forwards the parameter to another constructor. A primary constructor's parameters
/// (including a positional record's) are never reported at all: the language scopes them over the whole type
/// body on purpose, and backing a member is their entire job.
/// </para>
/// <para>
/// Ordered so the clean path never touches the semantic model. The member table is built once per type
/// declaration and cached on the declaration node, so a local costs a parent walk and two hash probes; only
/// the first declaration inside a type pays for the type symbol, and only a name that actually collides pays
/// for the static-context walk or the assignment scan. A nested type's field or property is measured against
/// its containing types' cached tables the same way, and a member of a non-nested type is rejected by a
/// single parent probe before anything is looked up.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1484ShadowedDeclarationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The discard name, which names nothing and so can shadow nothing.</summary>
    private const string DiscardName = "_";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.ShadowedDeclaration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the per-compilation caches, then analyzes every declaration that introduces a name.</summary>
    /// <param name="context">The compilation start context.</param>
    /// <remarks>
    /// One <c>SingleVariableDesignation</c> registration covers every declaration the pattern forms
    /// introduce — an <c>out var</c>, an <c>is</c> pattern, a <c>case</c> label and a deconstruction — so
    /// each of them is reached exactly once, with no node visited twice.
    /// </remarks>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var tablesByType = new ConcurrentDictionary<TypeDeclarationSyntax, ShadowedMemberTable>();
        var optionsByTree = new ConcurrentDictionary<SyntaxTree, ShadowedDeclarationOptions>();
        context.RegisterSyntaxNodeAction(
            nodeContext => Analyze(nodeContext, tablesByType, optionsByTree),
            SyntaxKind.Parameter,
            SyntaxKind.VariableDeclarator,
            SyntaxKind.ForEachStatement,
            SyntaxKind.SingleVariableDesignation,
            SyntaxKind.CatchDeclaration,
            SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Routes one declaration to the rule that governs it.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="tablesByType">The per-type-declaration member table cache.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<TypeDeclarationSyntax, ShadowedMemberTable> tablesByType,
        ConcurrentDictionary<SyntaxTree, ShadowedDeclarationOptions> optionsByTree)
    {
        switch (context.Node)
        {
            case ParameterSyntax parameter:
            {
                AnalyzeParameter(context, parameter, tablesByType);
                break;
            }

            case VariableDeclaratorSyntax declarator:
            {
                AnalyzeVariableDeclarator(context, declarator, tablesByType, optionsByTree);
                break;
            }

            case ForEachStatementSyntax loop:
            {
                AnalyzeLocal(context, loop, loop.Identifier, tablesByType);
                break;
            }

            case SingleVariableDesignationSyntax designation:
            {
                AnalyzeLocal(context, designation, designation.Identifier, tablesByType);
                break;
            }

            case CatchDeclarationSyntax clause:
            {
                AnalyzeLocal(context, clause, clause.Identifier, tablesByType);
                break;
            }

            case PropertyDeclarationSyntax property:
            {
                AnalyzeProperty(context, property, tablesByType);
                break;
            }
        }
    }

    /// <summary>Reports a parameter that shadows a member without feeding it.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="parameter">The parameter declaration.</param>
    /// <param name="tablesByType">The per-type-declaration member table cache.</param>
    private static void AnalyzeParameter(
        SyntaxNodeAnalysisContext context,
        ParameterSyntax parameter,
        ConcurrentDictionary<TypeDeclarationSyntax, ShadowedMemberTable> tablesByType)
    {
        // A primary constructor's parameter — a positional record's included — is scoped over the type body
        // by design and exists to back a member, and a delegate's parameter has no body to be ambiguous in.
        // Neither is a shadow, and both are rejected before anything is looked up.
        if (parameter.Parent is ParameterListSyntax { Parent: TypeDeclarationSyntax or DelegateDeclarationSyntax })
        {
            return;
        }

        if (!TryGetShadowedMember(context, parameter, parameter.Identifier, tablesByType, out var member)
            || FeedsShadowedMember(parameter, parameter.Identifier.ValueText))
        {
            return;
        }

        Report(context, parameter.Identifier, member);
    }

    /// <summary>Routes a variable declarator to the local rule or the field rule.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="declarator">The variable declarator.</param>
    /// <param name="tablesByType">The per-type-declaration member table cache.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <remarks>
    /// An event's declarator is not measured: an event is not a value a simple name can silently be assigned
    /// to, which is the mistake this rule exists to catch.
    /// </remarks>
    private static void AnalyzeVariableDeclarator(
        SyntaxNodeAnalysisContext context,
        VariableDeclaratorSyntax declarator,
        ConcurrentDictionary<TypeDeclarationSyntax, ShadowedMemberTable> tablesByType,
        ConcurrentDictionary<SyntaxTree, ShadowedDeclarationOptions> optionsByTree)
    {
        if (declarator.Parent is not VariableDeclarationSyntax declaration)
        {
            return;
        }

        switch (declaration.Parent)
        {
            case FieldDeclarationSyntax field:
            {
                AnalyzeField(context, declarator, field, tablesByType, optionsByTree);
                break;
            }

            case EventFieldDeclarationSyntax:
                break;

            default:
            {
                AnalyzeLocal(context, declarator, declarator.Identifier, tablesByType);
                break;
            }
        }
    }

    /// <summary>Reports a field that shadows a containing type's static member or hides an inherited field.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="declarator">The field's declarator.</param>
    /// <param name="field">The field declaration.</param>
    /// <param name="tablesByType">The per-type-declaration member table cache.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <remarks>
    /// The containing-type check runs first and answers for a field of a non-nested type — the common case —
    /// with a single parent probe. The inherited-field check reads its option next, so a field in the default
    /// configuration is rejected by one cached hash probe and never reaches the member table. A field marked
    /// <c>new</c> says the hiding is deliberate, and is taken at its word.
    /// </remarks>
    private static void AnalyzeField(
        SyntaxNodeAnalysisContext context,
        VariableDeclaratorSyntax declarator,
        FieldDeclarationSyntax field,
        ConcurrentDictionary<TypeDeclarationSyntax, ShadowedMemberTable> tablesByType,
        ConcurrentDictionary<SyntaxTree, ShadowedDeclarationOptions> optionsByTree)
    {
        if (TryReportNestedTypeMember(context, field, field.Modifiers, declarator.Identifier, tablesByType))
        {
            return;
        }

        if (!GetOptions(context, optionsByTree).CheckBaseTypes
            || ModifierListHelper.Contains(field.Modifiers, SyntaxKind.NewKeyword))
        {
            return;
        }

        if (!TryGetShadowedMember(context, declarator, declarator.Identifier, tablesByType, out var member)
            || !member.HidesInheritedField)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MaintainabilityRules.ShadowedDeclaration,
            declarator.Identifier.GetLocation(),
            declarator.Identifier.ValueText,
            ShadowedMember.InheritedFieldDescription));
    }

    /// <summary>Reports a nested type's property that shadows a containing type's static member.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="property">The property declaration.</param>
    /// <param name="tablesByType">The per-type-declaration member table cache.</param>
    /// <remarks>
    /// An explicit interface implementation is not measured: it is not reachable by simple name, so it can
    /// make nothing ambiguous.
    /// </remarks>
    private static void AnalyzeProperty(
        SyntaxNodeAnalysisContext context,
        PropertyDeclarationSyntax property,
        ConcurrentDictionary<TypeDeclarationSyntax, ShadowedMemberTable> tablesByType)
    {
        if (property.ExplicitInterfaceSpecifier is not null)
        {
            return;
        }

        TryReportNestedTypeMember(context, property, property.Modifiers, property.Identifier, tablesByType);
    }

    /// <summary>Reports a nested type's field or property that shadows a containing type's static member.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="member">The field or property declaration.</param>
    /// <param name="modifiers">The member's modifiers.</param>
    /// <param name="identifier">The declared identifier.</param>
    /// <param name="tablesByType">The per-type-declaration member table cache.</param>
    /// <returns><see langword="true"/> when the member was reported.</returns>
    /// <remarks>
    /// A member of a non-nested type is rejected by a single parent probe before anything is looked up. The
    /// walk then mirrors C#'s simple-name lookup: the nearest containing type that claims the name answers,
    /// and only a static claim is reported — an outer instance member is not reachable by simple name from a
    /// nested type, so nothing is ambiguous. A member marked <c>new</c> says its hiding is deliberate, and an
    /// <c>override</c> keeps the name its base declared, so neither is this declaration's naming choice.
    /// </remarks>
    private static bool TryReportNestedTypeMember(
        SyntaxNodeAnalysisContext context,
        MemberDeclarationSyntax member,
        SyntaxTokenList modifiers,
        SyntaxToken identifier,
        ConcurrentDictionary<TypeDeclarationSyntax, ShadowedMemberTable> tablesByType)
    {
        if (member.Parent is not TypeDeclarationSyntax { Parent: TypeDeclarationSyntax } nestedType)
        {
            return false;
        }

        var name = identifier.ValueText;
        if (!CanShadow(name) || ModifierListHelper.ContainsEither(modifiers, SyntaxKind.NewKeyword, SyntaxKind.OverrideKeyword))
        {
            return false;
        }

        for (var outer = nestedType.Parent as TypeDeclarationSyntax; outer is not null; outer = outer.Parent as TypeDeclarationSyntax)
        {
            if (!GetTable(context, outer, tablesByType).TryGet(name, out var shadowed))
            {
                continue;
            }

            if (!shadowed.IsStatic)
            {
                return false;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                MaintainabilityRules.ShadowedDeclaration,
                identifier.GetLocation(),
                name,
                shadowed.ContainingTypeDescription));
            return true;
        }

        return false;
    }

    /// <summary>Reports a local, a loop variable or a pattern variable that shadows a member.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="declaration">The declaration that introduces the name.</param>
    /// <param name="identifier">The declared identifier.</param>
    /// <param name="tablesByType">The per-type-declaration member table cache.</param>
    private static void AnalyzeLocal(
        SyntaxNodeAnalysisContext context,
        SyntaxNode declaration,
        SyntaxToken identifier,
        ConcurrentDictionary<TypeDeclarationSyntax, ShadowedMemberTable> tablesByType)
    {
        if (!TryGetShadowedMember(context, declaration, identifier, tablesByType, out var member))
        {
            return;
        }

        Report(context, identifier, member);
    }

    /// <summary>Finds the member a declared name already denotes, when the name is ambiguous where it stands.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="declaration">The declaration that introduces the name.</param>
    /// <param name="identifier">The declared identifier.</param>
    /// <param name="tablesByType">The per-type-declaration member table cache.</param>
    /// <param name="member">The member the name already denotes.</param>
    /// <returns><see langword="true"/> when the declaration makes the name ambiguous.</returns>
    /// <remarks>
    /// An instance member is not in scope inside a static member, so a local in a static method that reuses
    /// an instance field's name is not ambiguous and is not reported. The static-context walk runs last,
    /// because only a name that already collided can care about it.
    /// </remarks>
    private static bool TryGetShadowedMember(
        SyntaxNodeAnalysisContext context,
        SyntaxNode declaration,
        SyntaxToken identifier,
        ConcurrentDictionary<TypeDeclarationSyntax, ShadowedMemberTable> tablesByType,
        out ShadowedMember member)
    {
        member = default;
        var name = identifier.ValueText;
        if (!CanShadow(name))
        {
            return false;
        }

        if (declaration.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } typeDeclaration
            || !GetTable(context, typeDeclaration, tablesByType).TryGet(name, out member))
        {
            return false;
        }

        return member.IsStatic || !IsInStaticContext(declaration);
    }

    /// <summary>Returns whether a declared name is one that can shadow at all.</summary>
    /// <param name="name">The declared name.</param>
    /// <returns><see langword="false"/> for a missing identifier or the discard, which name nothing.</returns>
    private static bool CanShadow(string name) => name.Length != 0 && name != DiscardName;

    /// <summary>Reports one shadowing declaration.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="identifier">The declared identifier.</param>
    /// <param name="member">The member it shadows.</param>
    private static void Report(SyntaxNodeAnalysisContext context, SyntaxToken identifier, in ShadowedMember member)
        => context.ReportDiagnostic(Diagnostic.Create(
            MaintainabilityRules.ShadowedDeclaration,
            identifier.GetLocation(),
            identifier.ValueText,
            member.Description));

    /// <summary>Reads the member table for a type declaration, building each type's table at most once.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="typeDeclaration">The declaration the name is declared inside.</param>
    /// <param name="tablesByType">The per-type-declaration member table cache.</param>
    /// <returns>The member table.</returns>
    private static ShadowedMemberTable GetTable(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax typeDeclaration,
        ConcurrentDictionary<TypeDeclarationSyntax, ShadowedMemberTable> tablesByType)
    {
        if (tablesByType.TryGetValue(typeDeclaration, out var table))
        {
            return table;
        }

        table = context.SemanticModel.GetDeclaredSymbol(typeDeclaration, context.CancellationToken) is INamedTypeSymbol type
            ? ShadowedMemberTable.Create(type)
            : ShadowedMemberTable.Empty;
        tablesByType.TryAdd(typeDeclaration, table);
        return table;
    }

    /// <summary>Reads the settings for the declaration's tree, parsing each tree's options at most once.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns>The resolved settings.</returns>
    private static ShadowedDeclarationOptions GetOptions(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, ShadowedDeclarationOptions> optionsByTree)
    {
        var tree = context.Node.SyntaxTree;
        if (optionsByTree.TryGetValue(tree, out var options))
        {
            return options;
        }

        options = ShadowedDeclarationOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        optionsByTree.TryAdd(tree, options);
        return options;
    }

    /// <summary>Returns whether a declaration stands where instance members are out of scope.</summary>
    /// <param name="declaration">The declaration that introduces the name.</param>
    /// <returns><see langword="true"/> inside a static member, a static local function, or a static lambda.</returns>
    /// <remarks>
    /// A non-static local function or lambda inherits its enclosing member's context, so the walk continues
    /// through it rather than answering from it.
    /// </remarks>
    private static bool IsInStaticContext(SyntaxNode declaration)
    {
        for (var current = declaration.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case LocalFunctionStatementSyntax local:
                {
                    if (ModifierListHelper.Contains(local.Modifiers, SyntaxKind.StaticKeyword))
                    {
                        return true;
                    }

                    break;
                }

                case AnonymousFunctionExpressionSyntax lambda:
                {
                    if (ModifierListHelper.Contains(lambda.Modifiers, SyntaxKind.StaticKeyword))
                    {
                        return true;
                    }

                    break;
                }

                case BaseFieldDeclarationSyntax fieldDeclaration:
                    return ModifierListHelper.ContainsEither(fieldDeclaration.Modifiers, SyntaxKind.StaticKeyword, SyntaxKind.ConstKeyword);
                case BaseMethodDeclarationSyntax method:
                    return ModifierListHelper.Contains(method.Modifiers, SyntaxKind.StaticKeyword);
                case BasePropertyDeclarationSyntax property:
                    return ModifierListHelper.Contains(property.Modifiers, SyntaxKind.StaticKeyword);
                case TypeDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    /// <summary>Returns whether a parameter exists to fill in the member it shadows.</summary>
    /// <param name="parameter">The parameter declaration.</param>
    /// <param name="name">The shadowed member's name.</param>
    /// <returns><see langword="true"/> when the parameter feeds the member rather than obscuring it.</returns>
    /// <remarks>
    /// <c>this.name = name;</c> is how a C# constructor is written, and reporting it would fire on almost
    /// every codebase in existence. Rather than special-casing the constructor, the exemption asks the
    /// question that makes the idiom safe: does this declaration assign the member it shadows? That answer
    /// covers the bare <c>name = name;</c> spelling, the expression-bodied constructor, the tuple
    /// assignment, and a setter or initialize method that stores its argument. A constructor that forwards
    /// the parameter to another constructor is exempt for the same reason — the parameter is being passed
    /// on, not silently read in place of the member.
    /// </remarks>
    private static bool FeedsShadowedMember(ParameterSyntax parameter, string name)
    {
        if (GetOwningDeclaration(parameter) is not { } owner)
        {
            return false;
        }

        return (owner is ConstructorDeclarationSyntax { Initializer: { } initializer } && ForwardsParameter(initializer, name))
            || ContainsAssignmentTo(owner, name);
    }

    /// <summary>Gets the declaration whose body a parameter belongs to.</summary>
    /// <param name="parameter">The parameter declaration.</param>
    /// <returns>The owning declaration, or <see langword="null"/> when there is none.</returns>
    private static SyntaxNode? GetOwningDeclaration(ParameterSyntax parameter) => parameter.Parent switch
    {
        BaseParameterListSyntax list => list.Parent,
        SimpleLambdaExpressionSyntax lambda => lambda,
        _ => null,
    };

    /// <summary>Returns whether a constructor initializer passes the parameter on to another constructor.</summary>
    /// <param name="initializer">The <c>this</c> or <c>base</c> initializer.</param>
    /// <param name="name">The parameter's name.</param>
    /// <returns><see langword="true"/> when the parameter is forwarded.</returns>
    private static bool ForwardsParameter(ConstructorInitializerSyntax initializer, string name)
    {
        var arguments = initializer.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].Expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether anything inside a declaration assigns the named member.</summary>
    /// <param name="node">The node being scanned.</param>
    /// <param name="name">The shadowed member's name.</param>
    /// <returns><see langword="true"/> when an assignment targets the name.</returns>
    /// <remarks>
    /// An indexed child walk rather than <c>DescendantNodes()</c>, and it only runs for a parameter whose
    /// name has already collided — so a clean signature never reaches it.
    /// </remarks>
    private static bool ContainsAssignmentTo(SyntaxNode node, string name)
    {
        if (node is AssignmentExpressionSyntax assignment && TargetsMember(assignment.Left, name))
        {
            return true;
        }

        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].AsNode() is { } child && ContainsAssignmentTo(child, name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an assignment target names the shadowed member.</summary>
    /// <param name="target">The left-hand side of an assignment.</param>
    /// <param name="name">The shadowed member's name.</param>
    /// <returns><see langword="true"/> when the assignment is the one that fills the member in.</returns>
    private static bool TargetsMember(ExpressionSyntax target, string name) => target switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == name,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax or BaseExpressionSyntax } access =>
            access.Name.Identifier.ValueText == name,
        ParenthesizedExpressionSyntax parenthesized => TargetsMember(parenthesized.Expression, name),
        TupleExpressionSyntax tuple => TupleTargetsMember(tuple, name),
        _ => false,
    };

    /// <summary>Returns whether any element of a tuple assignment names the shadowed member.</summary>
    /// <param name="tuple">The tuple on the left of an assignment.</param>
    /// <param name="name">The shadowed member's name.</param>
    /// <returns><see langword="true"/> when one of the elements is the member.</returns>
    private static bool TupleTargetsMember(TupleExpressionSyntax tuple, string name)
    {
        var arguments = tuple.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (TargetsMember(arguments[i].Expression, name))
            {
                return true;
            }
        }

        return false;
    }
}
