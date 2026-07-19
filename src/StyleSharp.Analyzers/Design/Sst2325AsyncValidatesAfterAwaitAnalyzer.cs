// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an argument guard stranded after an async method's first await (SST2325). An async method runs
/// synchronously only up to its first <c>await</c>; everything past that runs on a continuation once the
/// returned task is awaited, so a guard placed there does not throw at the call site but later, when the
/// task is consumed.
/// </summary>
/// <remarks>
/// <para>
/// Detection is purely syntactic and needs no semantic model. The clean path is a look at a method's
/// modifiers: only a method carrying <c>async</c> and returning something other than <c>void</c> pays for a
/// single scan of its own top-level statements. The scan finds the first statement that unconditionally
/// awaits — <c>await X();</c>, <c>x = await X();</c>, or <c>T x = await X();</c> — and then reports each
/// later top-level statement that is an argument guard: an <c>if</c> throwing an <c>Argument…Exception</c>,
/// or an <c>Argument…Exception.ThrowIf…</c> call, whose condition or arguments name one of the method's
/// parameters.
/// </para>
/// <para>
/// A guard placed <em>before</em> the first await is the correct shape and is never reported. An await or a
/// guard inside a nested lambda or local function belongs to that function, not to the method around it, so
/// neither the first-await search nor the guard search descends into one. An await buried in an <c>if</c> or
/// a loop is not treated as the first-await boundary, because whether it runs before the guard is a runtime
/// question this rule does not answer.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2325AsyncValidatesAfterAwaitAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The prefix every runtime argument throw-helper shares.</summary>
    private const string ThrowHelperPrefix = "ThrowIf";

    /// <summary>The prefix the argument-exception family shares.</summary>
    private const string ArgumentPrefix = "Argument";

    /// <summary>The suffix every exception type name shares.</summary>
    private const string ExceptionSuffix = "Exception";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.AsyncValidatesAfterAwait);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Analyzes one method for argument guards stranded after its first await.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword)
            || IsVoid(method.ReturnType)
            || method.Body is not { } body
            || method.ParameterList.Parameters.Count == 0)
        {
            return;
        }

        var statements = body.Statements;
        var firstAwait = FirstUnconditionalAwaitIndex(statements);
        if (firstAwait < 0)
        {
            return;
        }

        for (var i = firstAwait + 1; i < statements.Count; i++)
        {
            if (TryGetArgumentGuard(statements[i], method.ParameterList, out var location))
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    DesignRules.AsyncValidatesAfterAwait,
                    location,
                    method.Identifier.ValueText));
            }
        }
    }

    /// <summary>Returns the index of the first statement that unconditionally awaits.</summary>
    /// <param name="statements">The method body's top-level statements.</param>
    /// <returns>The index, or <c>-1</c> when no leading statement awaits outright.</returns>
    private static int FirstUnconditionalAwaitIndex(SyntaxList<StatementSyntax> statements)
    {
        for (var i = 0; i < statements.Count; i++)
        {
            if (StatementAwaitsOutright(statements[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns whether a statement's own execution reaches an await no matter what.</summary>
    /// <param name="statement">The top-level statement.</param>
    /// <returns><see langword="true"/> for <c>await X();</c>, <c>x = await X();</c>, or <c>T x = await X();</c>.</returns>
    /// <remarks>
    /// Only the shapes where the await is evaluated the moment the statement runs qualify. An await inside a
    /// condition, a loop, or a nested expression is left out, because whether it runs before a later guard is
    /// a runtime question, and reporting on it would be a guess.
    /// </remarks>
    private static bool StatementAwaitsOutright(StatementSyntax statement)
    {
        switch (statement)
        {
            case ExpressionStatementSyntax { Expression: AwaitExpressionSyntax }:
            case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { Right: AwaitExpressionSyntax } }:
            {
                return true;
            }

            case LocalDeclarationStatementSyntax declaration:
            {
                var variables = declaration.Declaration.Variables;
                for (var i = 0; i < variables.Count; i++)
                {
                    if (variables[i].Initializer?.Value is AwaitExpressionSyntax)
                    {
                        return true;
                    }
                }

                return false;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Returns whether a statement is an argument guard, and where to report it.</summary>
    /// <param name="statement">The top-level statement.</param>
    /// <param name="parameters">The method's parameters.</param>
    /// <param name="location">The throw or throw-helper location to report.</param>
    /// <returns><see langword="true"/> for a parameter check that throws an <c>Argument…Exception</c>.</returns>
    private static bool TryGetArgumentGuard(StatementSyntax statement, ParameterListSyntax parameters, out Location location)
    {
        switch (statement)
        {
            case IfStatementSyntax { Else: null } guard
                when GetThrow(guard.Statement) is { } thrown
                    && ThrowsArgumentException(thrown)
                    && (ReferencesParameter(guard.Condition, parameters) || ReferencesParameter(thrown, parameters)):
            {
                location = thrown.GetLocation();
                return true;
            }

            case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }
                when IsArgumentThrowHelper(invocation) && ReferencesParameter(invocation.ArgumentList, parameters):
            {
                location = invocation.GetLocation();
                return true;
            }

            default:
            {
                location = Location.None;
                return false;
            }
        }
    }

    /// <summary>Gets the throw statement a guard's body ends in.</summary>
    /// <param name="statement">The guard's body.</param>
    /// <returns>The throw statement, or <see langword="null"/> when reaching the body does not throw.</returns>
    private static ThrowStatementSyntax? GetThrow(StatementSyntax statement) => statement switch
    {
        ThrowStatementSyntax thrown => thrown,
        BlockSyntax { Statements: [.., ThrowStatementSyntax thrown] } => thrown,
        _ => null,
    };

    /// <summary>Returns whether a throw creates one of the argument-exception family.</summary>
    /// <param name="thrown">The throw statement.</param>
    /// <returns><see langword="true"/> when the thrown type is named <c>Argument…Exception</c>.</returns>
    private static bool ThrowsArgumentException(ThrowStatementSyntax thrown)
        => thrown.Expression is ObjectCreationExpressionSyntax creation
            && GetSimpleName(creation.Type) is { } name
            && IsArgumentExceptionName(name);

    /// <summary>Returns whether an invocation is an argument-exception throw-helper.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns><see langword="true"/> for <c>Argument…Exception.ThrowIf…(…)</c>.</returns>
    private static bool IsArgumentThrowHelper(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax access
            && access.Name.Identifier.ValueText.StartsWith(ThrowHelperPrefix, StringComparison.Ordinal)
            && GetSimpleName(access.Expression) is { } receiver
            && IsArgumentExceptionName(receiver);

    /// <summary>Returns whether a simple name is that of an argument-exception type.</summary>
    /// <param name="name">The simple type name.</param>
    /// <returns><see langword="true"/> when it starts with <c>Argument</c> and ends with <c>Exception</c>.</returns>
    private static bool IsArgumentExceptionName(string name)
        => name.StartsWith(ArgumentPrefix, StringComparison.Ordinal)
            && name.EndsWith(ExceptionSuffix, StringComparison.Ordinal);

    /// <summary>Gets the rightmost name of a possibly qualified type or expression.</summary>
    /// <param name="node">The type name or expression.</param>
    /// <returns>The simple name, or <see langword="null"/> when the node is not a name.</returns>
    /// <remarks>
    /// <c>System.ArgumentNullException</c> and <c>global::System.ArgumentNullException</c> both arrive here as
    /// a qualified name, so the rightmost identifier is all this needs; the receiver of a throw-helper call
    /// arrives as a member access instead.
    /// </remarks>
    private static string? GetSimpleName(SyntaxNode node) => node switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Returns whether an async method's return type is <c>void</c>.</summary>
    /// <param name="returnType">The declared return type.</param>
    /// <returns><see langword="true"/> for <c>async void</c>, which is a separate defect this rule leaves alone.</returns>
    private static bool IsVoid(TypeSyntax returnType)
        => returnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);

    /// <summary>Returns whether a node reads one of the method's parameters.</summary>
    /// <param name="node">The node to search.</param>
    /// <param name="parameters">The method's parameters.</param>
    /// <returns><see langword="true"/> when a parameter's name appears.</returns>
    private static bool ReferencesParameter(SyntaxNode node, ParameterListSyntax parameters)
    {
        var scan = new ParameterScan(parameters);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, ParameterScan>(node, ref scan, VisitIdentifier);
        return scan.Found || (node is IdentifierNameSyntax self && NamesParameter(self, parameters));
    }

    /// <summary>Records whether an identifier names one of the method's parameters.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once a parameter is found, which stops the walk.</returns>
    private static bool VisitIdentifier(IdentifierNameSyntax identifier, ref ParameterScan state)
    {
        if (!NamesParameter(identifier, state.Parameters))
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>Returns whether an identifier names one of the parameters.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="parameters">The method's parameters.</param>
    /// <returns><see langword="true"/> when the name matches a parameter.</returns>
    private static bool NamesParameter(IdentifierNameSyntax identifier, ParameterListSyntax parameters)
    {
        var name = identifier.Identifier.ValueText;
        var list = parameters.Parameters;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The state threaded through the search for a parameter reference.</summary>
    /// <param name="Parameters">The method's parameters.</param>
    private record struct ParameterScan(ParameterListSyntax Parameters)
    {
        /// <summary>Gets or sets a value indicating whether a parameter was referenced.</summary>
        public bool Found { get; set; }
    }
}
