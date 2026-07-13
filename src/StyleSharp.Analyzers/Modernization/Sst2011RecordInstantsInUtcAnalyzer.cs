// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an instant recorded from the local clock (SST2011): a <c>DateTime.Now</c> or
/// <c>DateTimeOffset.Now</c> read that is stored in a field or property, or returned, where the value
/// outlives the moment and UTC is what was meant.
/// </summary>
/// <remarks>
/// <para>
/// Only the local clock is reported, and only where the value is <em>recorded</em>: the whole right-hand side
/// of a store into a field or property, the whole initializer of one, or the whole returned expression.
/// A local clock read that is immediately formatted or compared (<c>DateTime.Now.ToString(...)</c>,
/// <c>DateTime.Now.Hour</c>) is left alone, because a value that never escapes the expression cannot be
/// misread later.
/// </para>
/// <para>
/// This is a different question from SST1451, which asks whether a <c>DateTime</c> being <em>constructed</em>
/// states its <c>DateTimeKind</c>. SST1451 never looks at a property read and this rule never looks at an
/// object creation, so nothing is reported twice.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2011RecordInstantsInUtcAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.RecordInstantsInUtc);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var clockTypes = ClockPropertyAccess.ClockTypes.Resolve(start.Compilation);
            if (!clockTypes.Any)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, clockTypes),
                SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    /// <summary>Returns whether a local-clock read is being recorded rather than merely consulted.</summary>
    /// <param name="access">The clock read.</param>
    /// <param name="model">The semantic model, used only to classify a store's target.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the value is stored in state or handed back to a caller.</returns>
    internal static bool IsRecorded(MemberAccessExpressionSyntax access, SemanticModel model, CancellationToken cancellationToken)
        => access.Parent switch
        {
            ReturnStatementSyntax @return => @return.Expression == access,
            ArrowExpressionClauseSyntax arrow => arrow.Expression == access,
            EqualsValueClauseSyntax equals => IsStateInitializer(equals),
            AssignmentExpressionSyntax assignment => IsStoreIntoState(assignment, access, model, cancellationToken),
            _ => false,
        };

    /// <summary>Reports one local-clock read that is being recorded.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="clockTypes">The clock types resolved for this compilation.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, in ClockPropertyAccess.ClockTypes clockTypes)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (!ClockPropertyAccess.MatchesSpelling(access, localOnly: true))
        {
            return;
        }

        if (!IsRecorded(access, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (!ClockPropertyAccess.BindsToClock(context.SemanticModel, access, clockTypes, context.CancellationToken))
        {
            return;
        }

        var diagnostic = DiagnosticHelper.Create(
            ModernizationRules.RecordInstantsInUtc,
            access.GetLocation(),
            ClockPropertyAccess.Describe(access));
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>Returns whether an initializer belongs to a field or a property rather than a local.</summary>
    /// <param name="equals">The initializer clause.</param>
    /// <returns><see langword="true"/> when the initialized declaration is state that outlives the statement.</returns>
    /// <remarks>Decided on syntax alone: a field's declarator sits under a field declaration, a local's under a statement.</remarks>
    private static bool IsStateInitializer(EqualsValueClauseSyntax equals) => equals.Parent switch
    {
        PropertyDeclarationSyntax => true,
        VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax } => true,
        _ => false,
    };

    /// <summary>Returns whether an assignment stores the clock read into a field or a property.</summary>
    /// <param name="assignment">The assignment expression.</param>
    /// <param name="access">The clock read.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the target is state and the read is the whole value being stored.</returns>
    private static bool IsStoreIntoState(
        AssignmentExpressionSyntax assignment,
        MemberAccessExpressionSyntax access,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) || assignment.Right != access)
        {
            return false;
        }

        return model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is IFieldSymbol or IPropertySymbol;
    }
}
