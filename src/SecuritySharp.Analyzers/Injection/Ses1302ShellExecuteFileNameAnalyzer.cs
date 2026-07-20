// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a <c>ProcessStartInfo</c> that shell-executes a non-constant filename (SES1302). The rule
/// reports a single <c>new System.Diagnostics.ProcessStartInfo { ... }</c> object creation whose
/// initializer sets a member named <c>UseShellExecute</c> to the literal <c>true</c> and whose
/// <c>FileName</c> -- taken from an initializer <c>FileName = ...</c> assignment or, when the initializer
/// does not set it, from the constructor's <c>fileName</c> argument -- is not a compile-time constant.
/// With <c>UseShellExecute</c> enabled the OS shell resolves and parses the filename (PATH lookup,
/// registered handlers, document/URL launch), so a value the program did not fix in source is a
/// command-injection and unexpected-program risk; the non-constant filename span is reported. Detection
/// is strictly local to the one object-creation expression -- properties are never tracked across
/// separate statements -- and the type is proven by binding its <c>UseShellExecute</c> member by symbol
/// and containing type, never matched on identifier text alone. The rule is resolved once per compilation
/// by probing
/// <c>System.Diagnostics.ProcessStartInfo</c>; on a target framework without it nothing is registered,
/// so a project that cannot use the type pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1302ShellExecuteFileNameAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the process-start descriptor type this rule guards.</summary>
    private const string ProcessStartInfoMetadataName = "System.Diagnostics.ProcessStartInfo";

    /// <summary>The name of the member whose <c>true</c> value routes launching through the OS shell.</summary>
    private const string UseShellExecuteMemberName = "UseShellExecute";

    /// <summary>The name of the member (and constructor parameter) that names the program to launch.</summary>
    private const string FileNameMemberName = "FileName";

    /// <summary>The name of the constructor parameter that supplies the filename.</summary>
    private const string FileNameParameterName = "fileName";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.ShellExecuteFileName);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var processStartInfoType = start.Compilation.GetTypeByMetadataName(ProcessStartInfoMetadataName);
            if (processStartInfoType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeObjectCreation(nodeContext, processStartInfoType), SyntaxKind.ObjectCreationExpression);
        });
    }

    /// <summary>Reports SES1302 for a shell-executed <c>ProcessStartInfo</c> whose filename is non-constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="processStartInfoType">The gated <c>ProcessStartInfo</c> type resolved for the compilation.</param>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol processStartInfoType)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // Syntactic prefilter: only an object initializer that sets 'UseShellExecute = true' (literal) can
        // match. This rejects almost every object creation before the semantic model is touched, and its
        // single scan also captures the initializer's 'FileName = ...' assignment when present.
        if (objectCreation.Initializer is not { } initializer)
        {
            return;
        }

        ScanInitializer(initializer, out var useShellExecuteAssignment, out var fileNameAssignment);
        if (useShellExecuteAssignment is null)
        {
            return;
        }

        var model = context.SemanticModel;

        // Bind 'UseShellExecute' by symbol + containing type: this proves the object really is a
        // ProcessStartInfo (an unrelated type that merely shares the name fails here) and never relies on
        // identifier text alone. The 'FileName' member and constructor of that same creation follow from it.
        if (!IsMemberOf(model, useShellExecuteAssignment.Left, UseShellExecuteMemberName, processStartInfoType, context.CancellationToken))
        {
            return;
        }

        var fileNameExpression = GetFileNameExpression(model, objectCreation, fileNameAssignment, context.CancellationToken);

        // No filename is set locally, or it is a compile-time constant (a fixed program path): nothing to flag.
        if (fileNameExpression is null
            || model.GetConstantValue(fileNameExpression, context.CancellationToken).HasValue)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.ShellExecuteFileName,
            fileNameExpression.SyntaxTree,
            fileNameExpression.Span));
    }

    /// <summary>Returns the effective filename expression: the initializer's <c>FileName</c>, else the constructor's <c>fileName</c> argument.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="objectCreation">The <c>ProcessStartInfo</c> object creation.</param>
    /// <param name="fileNameAssignment">The initializer's <c>FileName = ...</c> assignment, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The filename expression, or <see langword="null"/> when none is set locally.</returns>
    private static ExpressionSyntax? GetFileNameExpression(
        SemanticModel model,
        ObjectCreationExpressionSyntax objectCreation,
        AssignmentExpressionSyntax? fileNameAssignment,
        CancellationToken cancellationToken)
    {
        // The initializer's 'FileName = ...' is the last write and wins over any constructor argument.
        if (fileNameAssignment is not null)
        {
            return fileNameAssignment.Right;
        }

        // No initializer filename: fall back to the constructor's 'fileName' argument, binding the
        // constructor only to map that parameter (its containing type is already established as ProcessStartInfo).
        return model.GetSymbolInfo(objectCreation, cancellationToken).Symbol is IMethodSymbol constructor
            ? GetConstructorFileNameArgument(objectCreation.ArgumentList, constructor)
            : null;
    }

    /// <summary>Scans an object initializer for the <c>UseShellExecute = true</c> and <c>FileName = ...</c> assignments.</summary>
    /// <param name="initializer">The object initializer.</param>
    /// <param name="useShellExecuteAssignment">Receives the <c>UseShellExecute = true</c> literal assignment, or <see langword="null"/>.</param>
    /// <param name="fileNameAssignment">Receives the <c>FileName = ...</c> assignment, or <see langword="null"/>.</param>
    private static void ScanInitializer(
        InitializerExpressionSyntax initializer,
        out AssignmentExpressionSyntax? useShellExecuteAssignment,
        out AssignmentExpressionSyntax? fileNameAssignment)
    {
        useShellExecuteAssignment = null;
        fileNameAssignment = null;

        var expressions = initializer.Expressions;
        for (var i = 0; i < expressions.Count; i++)
        {
            if (expressions[i] is AssignmentExpressionSyntax { Left: IdentifierNameSyntax memberName } assignment)
            {
                var name = memberName.Identifier.ValueText;
                if (name == UseShellExecuteMemberName && assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression))
                {
                    useShellExecuteAssignment = assignment;
                }
                else if (name == FileNameMemberName)
                {
                    fileNameAssignment = assignment;
                }
            }
        }
    }

    /// <summary>Returns the constructor argument bound to the <c>fileName</c> parameter, honouring a named argument.</summary>
    /// <param name="argumentList">The object-creation argument list, or <see langword="null"/> when omitted.</param>
    /// <param name="constructor">The bound <c>ProcessStartInfo</c> constructor.</param>
    /// <returns>The filename argument expression, or <see langword="null"/> when the constructor takes no filename.</returns>
    private static ExpressionSyntax? GetConstructorFileNameArgument(ArgumentListSyntax? argumentList, IMethodSymbol constructor)
    {
        if (argumentList is null || argumentList.Arguments.Count == 0)
        {
            return null;
        }

        var fileNameOrdinal = -1;
        var parameters = constructor.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Name == FileNameParameterName)
            {
                fileNameOrdinal = i;
                break;
            }
        }

        if (fileNameOrdinal < 0)
        {
            return null;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { Name.Identifier.ValueText: FileNameParameterName })
            {
                return arguments[i].Expression;
            }
        }

        // Positional: the filename occupies its parameter's slot when no argument names it.
        return fileNameOrdinal < arguments.Count && arguments[fileNameOrdinal].NameColon is null
            ? arguments[fileNameOrdinal].Expression
            : null;
    }

    /// <summary>Returns whether an expression binds to a member of the given name on the gated type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The member reference expression (an initializer assignment target).</param>
    /// <param name="memberName">The expected member name.</param>
    /// <param name="containingType">The gated <c>ProcessStartInfo</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the expression resolves to that member on the gated type.</returns>
    private static bool IsMemberOf(
        SemanticModel model,
        ExpressionSyntax expression,
        string memberName,
        INamedTypeSymbol containingType,
        CancellationToken cancellationToken)
        => model.GetSymbolInfo(expression, cancellationToken).Symbol is { } member
            && member.Name == memberName
            && SymbolEqualityComparer.Default.Equals(member.ContainingType, containingType);
}
