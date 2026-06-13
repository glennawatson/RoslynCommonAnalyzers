// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Grouped readability analyzer that flags redundant statements which can be deleted without
/// changing behavior. One tree walk reports every id in the family.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST1174 — a <c>return;</c> at the tail of a void member, or a <c>continue;</c> at the tail of a loop body, has no effect.</description></item>
/// <item><description>SST1176 — a field, event, or auto-property is initialized to the type's default value (opt-in).</description></item>
/// <item><description>SST1177 — a base list restates a compiler-implied type (<c>class C : object</c>, <c>enum E : int</c>).</description></item>
/// <item><description>SST1178 — a constructor calls a parameterless <c>: base()</c> that the compiler already emits.</description></item>
/// <item><description>SST1179 — a <c>default:</c> switch section whose only statement is <c>break;</c> matches having no default.</description></item>
/// <item><description>SST1180 — an <c>else</c> clause has an empty body.</description></item>
/// <item><description>SST1181 — an <c>override</c> does nothing but forward to the same base member.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantCodeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ReadabilityRules.NoRedundantJump,
        ReadabilityRules.NoMemberInitializedToDefault,
        ReadabilityRules.NoRedundantInheritanceList,
        ReadabilityRules.NoRedundantBaseConstructorCall,
        ReadabilityRules.NoRedundantDefaultSwitchSection,
        ReadabilityRules.NoEmptyElseClause,
        ReadabilityRules.NoRedundantOverride);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeReturn, SyntaxKind.ReturnStatement);
        context.RegisterSyntaxNodeAction(AnalyzeContinue, SyntaxKind.ContinueStatement);
        context.RegisterSyntaxNodeAction(AnalyzeFieldInitializer, SyntaxKind.FieldDeclaration, SyntaxKind.EventFieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzePropertyInitializer, SyntaxKind.PropertyDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeBaseList, SyntaxKind.ClassDeclaration, SyntaxKind.RecordDeclaration, SyntaxKind.EnumDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeBaseConstructorCall, SyntaxKind.BaseConstructorInitializer);
        context.RegisterSyntaxNodeAction(AnalyzeDefaultSwitchSection, SyntaxKind.SwitchSection);
        context.RegisterSyntaxNodeAction(AnalyzeElseClause, SyntaxKind.ElseClause);
        context.RegisterSyntaxNodeAction(AnalyzeRedundantOverride, SyntaxKind.MethodDeclaration, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Returns whether an initializer value is syntactically the default for any type it can bind to.</summary>
    /// <param name="value">The initializer expression.</param>
    /// <returns><see langword="true"/> for <c>0</c>, <c>false</c>, <c>null</c>, <c>default</c>, or <c>default(T)</c>.</returns>
    internal static bool IsDefaultValue(ExpressionSyntax value) => value switch
    {
        DefaultExpressionSyntax => true,
        LiteralExpressionSyntax literal => literal.Kind() switch
        {
            SyntaxKind.DefaultLiteralExpression => true,
            SyntaxKind.NullLiteralExpression => true,
            SyntaxKind.FalseLiteralExpression => true,
            SyntaxKind.NumericLiteralExpression => IsNumericZero(literal.Token.Value),
            SyntaxKind.CharacterLiteralExpression => literal.Token.Value is '\0',
            _ => false
        },
        _ => false
    };

    /// <summary>Returns whether a statement is the last statement of its directly enclosing block.</summary>
    /// <param name="statement">The statement to test.</param>
    /// <param name="block">The enclosing block when the statement is its tail; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the statement is the final statement of a block.</returns>
    internal static bool IsTailOfBlock(StatementSyntax statement, out BlockSyntax block)
    {
        block = null!;
        if (statement.Parent is not BlockSyntax owner)
        {
            return false;
        }

        var statements = owner.Statements;
        if (statements.Count == 0 || statements[statements.Count - 1] != statement)
        {
            return false;
        }

        block = owner;
        return true;
    }

    /// <summary>Reports SST1174 for a <c>return;</c> at the tail of a void member body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeReturn(SyntaxNodeAnalysisContext context)
    {
        var statement = (ReturnStatementSyntax)context.Node;

        // 'return;' with no value is only legal in a void/Task context, so the return type need not
        // be checked. Restricting to a member body that directly owns the block keeps try/if tails safe.
        if (statement.Expression is not null
            || !IsTailOfBlock(statement, out var block)
            || !OwnsMemberBody(block.Parent))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantJump, statement.GetLocation(), "return"));
    }

    /// <summary>Reports SST1174 for a <c>continue;</c> at the tail of a loop body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeContinue(SyntaxNodeAnalysisContext context)
    {
        var statement = (ContinueStatementSyntax)context.Node;
        if (!IsTailOfBlock(statement, out var block) || !IsLoop(block.Parent))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantJump, statement.GetLocation(), "continue"));
    }

    /// <summary>Reports SST1176 for a field or event field declarator initialized to the type's default.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeFieldInitializer(SyntaxNodeAnalysisContext context)
    {
        var field = (BaseFieldDeclarationSyntax)context.Node;

        // 'const' fields require an initializer, so it can never be dropped.
        if (ModifierListHelper.Contains(field.Modifiers, SyntaxKind.ConstKeyword))
        {
            return;
        }

        var variables = field.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            var declarator = variables[i];
            if (declarator.Initializer is not { } initializer || !IsDefaultValue(initializer.Value))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoMemberInitializedToDefault, initializer.Value.GetLocation(), declarator.Identifier.ValueText));
        }
    }

    /// <summary>Reports SST1176 for an auto-property initialized to the type's default.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzePropertyInitializer(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;

        // Only auto-properties can carry an initializer, so no auto-property check is needed.
        if (property.Initializer is not { } initializer || !IsDefaultValue(initializer.Value))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoMemberInitializedToDefault, initializer.Value.GetLocation(), property.Identifier.ValueText));
    }

    /// <summary>Reports SST1177 for a base list whose first entry restates a compiler-implied type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeBaseList(SyntaxNodeAnalysisContext context)
    {
        var declaration = (BaseTypeDeclarationSyntax)context.Node;
        if (declaration.BaseList is not { } baseList || baseList.Types.Count == 0)
        {
            return;
        }

        var firstBaseType = baseList.Types[0];
        if (context.SemanticModel.GetSymbolInfo(firstBaseType.Type, context.CancellationToken).Symbol is not ITypeSymbol symbol)
        {
            return;
        }

        // An enum's default underlying type is 'int'; a class/record's implicit base is 'object'.
        var redundant = declaration.IsKind(SyntaxKind.EnumDeclaration)
            ? symbol.SpecialType == SpecialType.System_Int32
            : symbol.SpecialType == SpecialType.System_Object;
        if (!redundant)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantInheritanceList, firstBaseType.GetLocation(), symbol.ToDisplayString()));
    }

    /// <summary>Reports SST1178 for a parameterless <c>: base()</c> constructor initializer.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeBaseConstructorCall(SyntaxNodeAnalysisContext context)
    {
        var initializer = (ConstructorInitializerSyntax)context.Node;
        if (initializer.ArgumentList.Arguments.Count != 0)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantBaseConstructorCall, initializer.GetLocation()));
    }

    /// <summary>Reports SST1179 for a <c>default:</c> switch section whose only statement is <c>break;</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeDefaultSwitchSection(SyntaxNodeAnalysisContext context)
    {
        var section = (SwitchSectionSyntax)context.Node;

        // Only a lone 'default:' label is safe to drop. A shared 'case X: default:' section still
        // needs its case labels, and a multi-statement body may rely on the explicit section.
        if (section.Labels.Count != 1
            || !section.Labels[0].IsKind(SyntaxKind.DefaultSwitchLabel)
            || section.Statements.Count != 1
            || !section.Statements[0].IsKind(SyntaxKind.BreakStatement))
        {
            return;
        }

        var defaultLabel = (DefaultSwitchLabelSyntax)section.Labels[0];
        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantDefaultSwitchSection, defaultLabel.Keyword.GetLocation()));
    }

    /// <summary>Reports SST1180 for an <c>else</c> clause whose body is empty.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeElseClause(SyntaxNodeAnalysisContext context)
    {
        var elseClause = (ElseClauseSyntax)context.Node;

        // 'else if (...)' carries an IfStatement body and is never empty, so only a bare block or
        // stray semicolon reaches the report.
        var isEmpty = elseClause.Statement switch
        {
            BlockSyntax block => block.Statements.Count == 0,
            EmptyStatementSyntax => true,
            _ => false
        };
        if (!isEmpty)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoEmptyElseClause, elseClause.ElseKeyword.GetLocation()));
    }

    /// <summary>Reports SST1181 for an <c>override</c> that only forwards to the same base member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeRedundantOverride(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is MethodDeclarationSyntax method && OverrideForwardingAnalysis.IsPlainForwardingMethod(method))
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantOverride, method.Identifier.GetLocation(), method.Identifier.ValueText));
            return;
        }

        if (context.Node is not PropertyDeclarationSyntax property || !OverrideForwardingAnalysis.IsPlainForwardingProperty(property))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoRedundantOverride, property.Identifier.GetLocation(), property.Identifier.ValueText));
    }

    /// <summary>Returns whether a boxed numeric literal value is zero.</summary>
    /// <param name="value">The literal token value.</param>
    /// <returns><see langword="true"/> when the value is a numeric zero.</returns>
    [SuppressMessage("Critical Code Smell", "the rule:Methods and properties should not be too complex", Justification = "A flat numeric-type switch is a zero-allocation jump table.")]
    private static bool IsNumericZero(object? value) => value switch
    {
        int i => i == 0,
        long l => l == 0,
        uint ui => ui == 0,
        ulong ul => ul == 0,
        short s => s == 0,
        ushort us => us == 0,
        byte b => b == 0,
        sbyte sb => sb == 0,
        double d => d.CompareTo(0d) == 0,
        float f => f.CompareTo(0f) == 0,
        decimal m => m == 0,
        _ => false
    };

    /// <summary>Returns whether a node is a member/function whose block body a tail <c>return;</c> can leave.</summary>
    /// <param name="node">The candidate owner of the block.</param>
    /// <returns><see langword="true"/> for a method, local function, accessor, constructor, operator, or lambda.</returns>
    private static bool OwnsMemberBody(SyntaxNode? node) => node is MethodDeclarationSyntax
        or LocalFunctionStatementSyntax
        or AccessorDeclarationSyntax
        or ConstructorDeclarationSyntax
        or DestructorDeclarationSyntax
        or OperatorDeclarationSyntax
        or ConversionOperatorDeclarationSyntax
        or AnonymousFunctionExpressionSyntax;

    /// <summary>Returns whether a node is a loop statement whose body a tail <c>continue;</c> can leave.</summary>
    /// <param name="node">The candidate loop owner of the block.</param>
    /// <returns><see langword="true"/> for a <c>for</c>, <c>foreach</c>, <c>while</c>, or <c>do</c> loop.</returns>
    private static bool IsLoop(SyntaxNode? node) => node is ForStatementSyntax
        or ForEachStatementSyntax
        or WhileStatementSyntax
        or DoStatementSyntax;
}
