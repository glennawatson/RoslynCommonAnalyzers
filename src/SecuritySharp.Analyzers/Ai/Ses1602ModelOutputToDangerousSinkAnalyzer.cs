// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags AI model output that reaches a process, file, or raw-SQL sink (SES1602). The source is proven by
/// binding a <c>.Text</c> member access to the <c>Text</c> property of <c>Microsoft.Extensions.AI.ChatResponse</c>
/// or <c>Microsoft.Extensions.AI.ChatMessage</c> (so <c>ChatResponse&lt;T&gt;</c>, which inherits the property,
/// matches too). That model text is reported when it flows -- inline, or through a single immediately-preceding
/// local declaration in the same block -- into one of these local sink shapes:
/// <list type="bullet">
/// <item><description>the <c>fileName</c> or <c>arguments</c> argument of <c>System.Diagnostics.Process.Start</c>;</description></item>
/// <item><description>a <c>System.Diagnostics.ProcessStartInfo.FileName</c> or <c>.Arguments</c> assignment (including an object initializer);</description></item>
/// <item><description>the <c>path</c> (first string) argument of a curated set of <c>System.IO.File</c> members;</description></item>
/// <item><description>the <c>sql</c> argument of Entity Framework Core's <c>FromSqlRaw</c>, <c>ExecuteSqlRaw</c>, or <c>ExecuteSqlRawAsync</c>.</description></item>
/// </list>
/// Detection is strictly local -- no data-flow or interprocedural tracking. A one-local hop is honoured only when
/// the declaration is the statement immediately before the sink in the same block, so the local provably still
/// holds the model text at the sink. Every sink is proven by binding its symbol and containing type, never matched
/// on identifier text alone. The rule is gated on the AI response types resolving; a project that does not reference
/// <c>Microsoft.Extensions.AI</c> registers nothing and pays nothing, and each sink family is registered only when
/// its type is present in the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1602ModelOutputToDangerousSinkAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the AI chat response type whose <c>Text</c> is the guarded source.</summary>
    private const string ChatResponseMetadataName = "Microsoft.Extensions.AI.ChatResponse";

    /// <summary>The metadata name of the AI chat message type whose <c>Text</c> is the guarded source.</summary>
    private const string ChatMessageMetadataName = "Microsoft.Extensions.AI.ChatMessage";

    /// <summary>The metadata name of the process type whose <c>Start</c> call is a sink.</summary>
    private const string ProcessMetadataName = "System.Diagnostics.Process";

    /// <summary>The metadata name of the process-start descriptor whose members are sinks.</summary>
    private const string ProcessStartInfoMetadataName = "System.Diagnostics.ProcessStartInfo";

    /// <summary>The metadata name of the file helper whose path arguments are sinks.</summary>
    private const string FileMetadataName = "System.IO.File";

    /// <summary>The metadata name of the EF Core extension class exposing <c>ExecuteSqlRaw</c>.</summary>
    private const string EfDatabaseFacadeExtensionsMetadataName = "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions";

    /// <summary>The metadata name of the EF Core extension class exposing <c>FromSqlRaw</c>.</summary>
    private const string EfQueryableExtensionsMetadataName = "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions";

    /// <summary>The name of the text-bearing property on the guarded AI response types.</summary>
    private const string TextPropertyName = "Text";

    /// <summary>The name of the process launch method whose arguments are sinks.</summary>
    private const string StartMethodName = "Start";

    /// <summary>The name of the constructor/property/parameter that names the launched program.</summary>
    private const string FileNameMemberName = "FileName";

    /// <summary>The name of the process arguments property/parameter.</summary>
    private const string ArgumentsMemberName = "Arguments";

    /// <summary>The name of the <c>fileName</c> parameter on <c>Process.Start</c>.</summary>
    private const string FileNameParameterName = "fileName";

    /// <summary>The name of the <c>arguments</c> parameter on <c>Process.Start</c>.</summary>
    private const string ArgumentsParameterName = "arguments";

    /// <summary>The name of the <c>sql</c> parameter on the raw-SQL EF Core methods.</summary>
    private const string SqlParameterName = "sql";

    /// <summary>The sink label reported for a <c>Process.Start</c> filename argument.</summary>
    private const string ProcessFileNameSinkLabel = "the program launched by 'Process.Start'";

    /// <summary>The sink label reported for a <c>Process.Start</c> arguments argument.</summary>
    private const string ProcessArgumentsSinkLabel = "the command line passed to 'Process.Start'";

    /// <summary>The sink label reported for a <c>ProcessStartInfo.FileName</c> assignment.</summary>
    private const string ProcessStartInfoFileNameSinkLabel = "'ProcessStartInfo.FileName'";

    /// <summary>The sink label reported for a <c>ProcessStartInfo.Arguments</c> assignment.</summary>
    private const string ProcessStartInfoArgumentsSinkLabel = "'ProcessStartInfo.Arguments'";

    /// <summary>The sink label reported for a <c>System.IO.File</c> path argument.</summary>
    private const string FilePathSinkLabel = "a 'System.IO.File' path";

    /// <summary>The sink label reported for a raw-SQL command argument.</summary>
    private const string RawSqlSinkLabel = "a raw SQL command ('FromSqlRaw'/'ExecuteSqlRaw')";

    /// <summary>The EF Core raw-SQL method names whose <c>sql</c> argument is a sink.</summary>
    private static readonly HashSet<string> RawSqlMethodNames =
        new(StringComparer.Ordinal) { "FromSqlRaw", "ExecuteSqlRaw", "ExecuteSqlRawAsync" };

    /// <summary>A curated, high-signal set of <c>System.IO.File</c> members whose leading path argument is a sink.</summary>
    private static readonly HashSet<string> FileSinkMethodNames = new(StringComparer.Ordinal)
    {
        "ReadAllText", "ReadAllTextAsync", "ReadAllBytes", "ReadAllBytesAsync", "ReadAllLines", "ReadAllLinesAsync",
        "ReadLines", "ReadLinesAsync", "WriteAllText", "WriteAllTextAsync", "WriteAllBytes", "WriteAllBytesAsync",
        "WriteAllLines", "WriteAllLinesAsync", "AppendAllText", "AppendAllTextAsync", "AppendAllLines", "AppendAllLinesAsync",
        "Open", "OpenRead", "OpenWrite", "OpenText", "Create", "CreateText", "AppendText", "Delete", "Copy", "Move", "Replace",
    };

    /// <summary>The candidate sink family classified from an invocation's method name before binding.</summary>
    private enum InvocationSinkKind
    {
        /// <summary>Not a sink invocation.</summary>
        None,

        /// <summary>A <c>Process.Start</c> call.</summary>
        ProcessStart,

        /// <summary>A <c>System.IO.File</c> member call.</summary>
        FilePath,

        /// <summary>An EF Core raw-SQL call.</summary>
        RawSql,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.ModelOutputToDangerousSink);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var compilation = start.Compilation;

            // Source gate: without an AI response type there is no model-output source, so the rule is inert.
            var chatResponseType = compilation.GetTypeByMetadataName(ChatResponseMetadataName);
            var chatMessageType = compilation.GetTypeByMetadataName(ChatMessageMetadataName);
            if (chatResponseType is null && chatMessageType is null)
            {
                return;
            }

            var processType = compilation.GetTypeByMetadataName(ProcessMetadataName);
            var processStartInfoType = compilation.GetTypeByMetadataName(ProcessStartInfoMetadataName);
            var fileType = compilation.GetTypeByMetadataName(FileMetadataName);
            var efFacadeExtensionsType = compilation.GetTypeByMetadataName(EfDatabaseFacadeExtensionsMetadataName);
            var efQueryableExtensionsType = compilation.GetTypeByMetadataName(EfQueryableExtensionsMetadataName);

            var sinks = new ModelOutputSinkContext(
                chatResponseType,
                chatMessageType,
                processType,
                processStartInfoType,
                fileType,
                efFacadeExtensionsType,
                efQueryableExtensionsType);

            if (processType is not null || fileType is not null || efFacadeExtensionsType is not null || efQueryableExtensionsType is not null)
            {
                start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, sinks), SyntaxKind.InvocationExpression);
            }

            if (processStartInfoType is not null)
            {
                start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, sinks), SyntaxKind.SimpleAssignmentExpression);
            }
        });
    }

    /// <summary>Reports SES1602 for a sink invocation whose dangerous argument is model output.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="sinks">The resolved source and sink types for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, ModelOutputSinkContext sinks)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: an 'X.Method(...)' call whose name could name a sink and that carries arguments.
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        // Cheap name screen before any binding: only a name that could match a resolved sink family proceeds.
        var kind = ClassifyInvocationName(memberAccess.Name.Identifier.ValueText, sinks);
        if (kind == InvocationSinkKind.None)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (kind == InvocationSinkKind.ProcessStart)
        {
            AnalyzeProcessStart(context, invocation, method, sinks);
        }
        else if (kind == InvocationSinkKind.FilePath)
        {
            AnalyzeFilePath(context, invocation, method, sinks);
        }
        else
        {
            AnalyzeRawSql(context, invocation, method, sinks);
        }
    }

    /// <summary>Classifies an invocation's method name into a candidate sink family before any binding.</summary>
    /// <param name="methodName">The invoked member's simple name.</param>
    /// <param name="sinks">The resolved source and sink types for the compilation.</param>
    /// <returns>The candidate sink family, or <see cref="InvocationSinkKind.None"/>.</returns>
    private static InvocationSinkKind ClassifyInvocationName(string methodName, ModelOutputSinkContext sinks)
    {
        if (sinks.ProcessType is not null && methodName == StartMethodName)
        {
            return InvocationSinkKind.ProcessStart;
        }

        if (sinks.FileType is not null && FileSinkMethodNames.Contains(methodName))
        {
            return InvocationSinkKind.FilePath;
        }

        var hasEfSink = sinks.EfFacadeExtensionsType is not null || sinks.EfQueryableExtensionsType is not null;
        return hasEfSink && RawSqlMethodNames.Contains(methodName) ? InvocationSinkKind.RawSql : InvocationSinkKind.None;
    }

    /// <summary>Reports SES1602 for a <c>Process.Start</c> call whose filename or arguments is model output.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The <c>Start</c> invocation.</param>
    /// <param name="method">The bound invoked method.</param>
    /// <param name="sinks">The resolved source and sink types for the compilation.</param>
    private static void AnalyzeProcessStart(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, IMethodSymbol method, ModelOutputSinkContext sinks)
    {
        if (method.Name != StartMethodName || !SymbolEqualityComparer.Default.Equals(method.ContainingType, sinks.ProcessType))
        {
            return;
        }

        ReportIfModelOutput(context, GetArgumentForParameter(invocation.ArgumentList, method, FileNameParameterName), sinks, ProcessFileNameSinkLabel);
        ReportIfModelOutput(context, GetArgumentForParameter(invocation.ArgumentList, method, ArgumentsParameterName), sinks, ProcessArgumentsSinkLabel);
    }

    /// <summary>Reports SES1602 for a <c>System.IO.File</c> call whose leading path argument is model output.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The <c>File</c> member invocation.</param>
    /// <param name="method">The bound invoked method.</param>
    /// <param name="sinks">The resolved source and sink types for the compilation.</param>
    private static void AnalyzeFilePath(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, IMethodSymbol method, ModelOutputSinkContext sinks)
    {
        // The path is the first parameter of every gated File member, and it is always a string.
        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, sinks.FileType)
            || method.Parameters.Length == 0
            || method.Parameters[0].Type.SpecialType != SpecialType.System_String)
        {
            return;
        }

        ReportIfModelOutput(context, GetLeadingArgument(invocation.ArgumentList, method.Parameters[0].Name), sinks, FilePathSinkLabel);
    }

    /// <summary>Reports SES1602 for an EF Core raw-SQL call whose <c>sql</c> argument is model output.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The raw-SQL invocation.</param>
    /// <param name="method">The bound invoked method.</param>
    /// <param name="sinks">The resolved source and sink types for the compilation.</param>
    private static void AnalyzeRawSql(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, IMethodSymbol method, ModelOutputSinkContext sinks)
    {
        if (!RawSqlMethodNames.Contains(method.Name)
            || !(SymbolEqualityComparer.Default.Equals(method.ContainingType, sinks.EfFacadeExtensionsType)
                || SymbolEqualityComparer.Default.Equals(method.ContainingType, sinks.EfQueryableExtensionsType)))
        {
            return;
        }

        // Called through member access, the extension method is reduced: its first parameter is 'sql', which
        // lines up with the first call-site argument.
        ReportIfModelOutput(context, GetArgumentForParameter(invocation.ArgumentList, method, SqlParameterName), sinks, RawSqlSinkLabel);
    }

    /// <summary>Reports SES1602 for a <c>ProcessStartInfo.FileName</c>/<c>.Arguments</c> assignment whose value is model output.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="sinks">The resolved source and sink types for the compilation.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, ModelOutputSinkContext sinks)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: a target named 'FileName' or 'Arguments' (a '.Member' write or an initializer 'Member = ...').
        var targetLabel = ClassifyAssignmentTarget(assignment.Left);
        if (targetLabel is null || !IsModelOutput(context.SemanticModel, assignment.Right, sinks, context.CancellationToken))
        {
            return;
        }

        // Bind the target to prove it is really ProcessStartInfo.FileName/Arguments, never matched on name alone.
        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, sinks.ProcessStartInfoType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.ModelOutputToDangerousSink,
            assignment.Right.SyntaxTree,
            assignment.Right.Span,
            targetLabel));
    }

    /// <summary>Reports SES1602 at an expression when it carries model output.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The candidate sink expression, or <see langword="null"/> when absent.</param>
    /// <param name="sinks">The resolved source and sink types for the compilation.</param>
    /// <param name="sinkLabel">The sink label placed in the diagnostic message.</param>
    private static void ReportIfModelOutput(SyntaxNodeAnalysisContext context, ExpressionSyntax? expression, ModelOutputSinkContext sinks, string sinkLabel)
    {
        if (expression is null || !IsModelOutput(context.SemanticModel, expression, sinks, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.ModelOutputToDangerousSink,
            expression.SyntaxTree,
            expression.Span,
            sinkLabel));
    }

    /// <summary>Classifies an assignment target as a <c>ProcessStartInfo</c> filename or arguments write.</summary>
    /// <param name="left">The assignment's left-hand side.</param>
    /// <returns>The sink label for the target, or <see langword="null"/> when the target is unrelated.</returns>
    private static string? ClassifyAssignmentTarget(ExpressionSyntax left)
    {
        var name = left switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

        return name switch
        {
            FileNameMemberName => ProcessStartInfoFileNameSinkLabel,
            ArgumentsMemberName => ProcessStartInfoArgumentsSinkLabel,
            _ => null,
        };
    }

    /// <summary>Returns whether an expression is model-output text: inline, or a single immediately-preceding local.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="sinks">The resolved source and sink types for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the expression carries model output.</returns>
    private static bool IsModelOutput(SemanticModel model, ExpressionSyntax expression, ModelOutputSinkContext sinks, CancellationToken cancellationToken)
    {
        if (IsModelTextAccess(model, expression, sinks, cancellationToken))
        {
            return true;
        }

        return expression is IdentifierNameSyntax identifier && IsImmediatelyPrecedingModelLocal(model, identifier, sinks, cancellationToken);
    }

    /// <summary>Returns whether an expression is a <c>.Text</c> access bound to a guarded AI response type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="sinks">The resolved source and sink types for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for <c>ChatResponse.Text</c>/<c>ChatMessage.Text</c>.</returns>
    private static bool IsModelTextAccess(SemanticModel model, ExpressionSyntax expression, ModelOutputSinkContext sinks, CancellationToken cancellationToken)
    {
        if (expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: TextPropertyName })
        {
            return false;
        }

        return model.GetSymbolInfo(expression, cancellationToken).Symbol is IPropertySymbol { Name: TextPropertyName } property
            && IsGuardedResponseType(property.ContainingType, sinks);
    }

    /// <summary>Returns whether a bare identifier binds to a local whose immediately-preceding declaration initializes it to model text.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="identifier">The identifier used at the sink.</param>
    /// <param name="sinks">The resolved source and sink types for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the local provably still holds model text at the sink.</returns>
    private static bool IsImmediatelyPrecedingModelLocal(SemanticModel model, IdentifierNameSyntax identifier, ModelOutputSinkContext sinks, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(identifier, cancellationToken).Symbol is not ILocalSymbol local
            || local.DeclaringSyntaxReferences.Length != 1
            || local.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is not VariableDeclaratorSyntax { Initializer.Value: { } initializer } declarator
            || declarator.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() is not { } declarationStatement)
        {
            return false;
        }

        // Adjacency: the declaration must be the statement directly before the sink statement in the same block,
        // so no statement between them can reassign the local -- the value at the sink is provably the initializer.
        if (GetEnclosingBlockStatement(identifier) is not { } sinkStatement
            || declarationStatement.Parent is not BlockSyntax block
            || !ReferenceEquals(sinkStatement.Parent, block))
        {
            return false;
        }

        var statements = block.Statements;
        var declarationIndex = statements.IndexOf(declarationStatement);
        return declarationIndex >= 0
            && statements.IndexOf(sinkStatement) == declarationIndex + 1
            && IsModelTextAccess(model, initializer, sinks, cancellationToken);
    }

    /// <summary>Returns the nearest ancestor statement that is a direct child of a block.</summary>
    /// <param name="node">The node to search up from.</param>
    /// <returns>The block-level statement, or <see langword="null"/> when none.</returns>
    private static StatementSyntax? GetEnclosingBlockStatement(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is StatementSyntax statement && statement.Parent is BlockSyntax)
            {
                return statement;
            }
        }

        return null;
    }

    /// <summary>Returns whether a type is one of the guarded AI response types (matching an inherited <c>Text</c>).</summary>
    /// <param name="type">The property's containing type.</param>
    /// <param name="sinks">The resolved source and sink types for the compilation.</param>
    /// <returns><see langword="true"/> when the type is the gated <c>ChatResponse</c> or <c>ChatMessage</c>.</returns>
    private static bool IsGuardedResponseType(INamedTypeSymbol type, ModelOutputSinkContext sinks)
        => SymbolEqualityComparer.Default.Equals(type, sinks.ChatResponseType)
            || SymbolEqualityComparer.Default.Equals(type, sinks.ChatMessageType);

    /// <summary>Returns the argument bound to a named parameter, honouring an explicit name-colon and positional order.</summary>
    /// <param name="argumentList">The invocation's argument list.</param>
    /// <param name="method">The bound invoked method.</param>
    /// <param name="parameterName">The target parameter name.</param>
    /// <returns>The argument expression, or <see langword="null"/> when the parameter has no argument.</returns>
    private static ExpressionSyntax? GetArgumentForParameter(ArgumentListSyntax argumentList, IMethodSymbol method, string parameterName)
    {
        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { Name.Identifier.ValueText: { } explicitName } && explicitName == parameterName)
            {
                return arguments[i].Expression;
            }
        }

        var parameters = method.Parameters;
        var ordinal = -1;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Name == parameterName)
            {
                ordinal = i;
                break;
            }
        }

        return ordinal >= 0 && ordinal < arguments.Count && arguments[ordinal].NameColon is null
            ? arguments[ordinal].Expression
            : null;
    }

    /// <summary>Returns the leading (path/sql) argument, honouring an explicit name-colon for the first parameter.</summary>
    /// <param name="argumentList">The invocation's argument list (already known to be non-empty).</param>
    /// <param name="parameterName">The first parameter's name.</param>
    /// <returns>The leading argument expression, or <see langword="null"/> when it cannot be identified positionally.</returns>
    private static ExpressionSyntax? GetLeadingArgument(ArgumentListSyntax argumentList, string parameterName)
    {
        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { Name.Identifier.ValueText: { } explicitName } && explicitName == parameterName)
            {
                return arguments[i].Expression;
            }
        }

        return arguments[0].NameColon is null ? arguments[0].Expression : null;
    }

    /// <summary>Holds the source and sink types resolved once per compilation.</summary>
    private sealed class ModelOutputSinkContext
    {
        /// <summary>Initializes a new instance of the <see cref="ModelOutputSinkContext"/> class.</summary>
        /// <param name="chatResponseType">The resolved <c>ChatResponse</c> type, or <see langword="null"/>.</param>
        /// <param name="chatMessageType">The resolved <c>ChatMessage</c> type, or <see langword="null"/>.</param>
        /// <param name="processType">The resolved <c>Process</c> type, or <see langword="null"/>.</param>
        /// <param name="processStartInfoType">The resolved <c>ProcessStartInfo</c> type, or <see langword="null"/>.</param>
        /// <param name="fileType">The resolved <c>File</c> type, or <see langword="null"/>.</param>
        /// <param name="efFacadeExtensionsType">The resolved EF Core database-facade extensions type, or <see langword="null"/>.</param>
        /// <param name="efQueryableExtensionsType">The resolved EF Core queryable extensions type, or <see langword="null"/>.</param>
        public ModelOutputSinkContext(
            INamedTypeSymbol? chatResponseType,
            INamedTypeSymbol? chatMessageType,
            INamedTypeSymbol? processType,
            INamedTypeSymbol? processStartInfoType,
            INamedTypeSymbol? fileType,
            INamedTypeSymbol? efFacadeExtensionsType,
            INamedTypeSymbol? efQueryableExtensionsType)
        {
            ChatResponseType = chatResponseType;
            ChatMessageType = chatMessageType;
            ProcessType = processType;
            ProcessStartInfoType = processStartInfoType;
            FileType = fileType;
            EfFacadeExtensionsType = efFacadeExtensionsType;
            EfQueryableExtensionsType = efQueryableExtensionsType;
        }

        /// <summary>Gets the resolved <c>ChatResponse</c> type, or <see langword="null"/>.</summary>
        public INamedTypeSymbol? ChatResponseType { get; }

        /// <summary>Gets the resolved <c>ChatMessage</c> type, or <see langword="null"/>.</summary>
        public INamedTypeSymbol? ChatMessageType { get; }

        /// <summary>Gets the resolved <c>Process</c> type, or <see langword="null"/>.</summary>
        public INamedTypeSymbol? ProcessType { get; }

        /// <summary>Gets the resolved <c>ProcessStartInfo</c> type, or <see langword="null"/>.</summary>
        public INamedTypeSymbol? ProcessStartInfoType { get; }

        /// <summary>Gets the resolved <c>File</c> type, or <see langword="null"/>.</summary>
        public INamedTypeSymbol? FileType { get; }

        /// <summary>Gets the resolved EF Core database-facade extensions type, or <see langword="null"/>.</summary>
        public INamedTypeSymbol? EfFacadeExtensionsType { get; }

        /// <summary>Gets the resolved EF Core queryable extensions type, or <see langword="null"/>.</summary>
        public INamedTypeSymbol? EfQueryableExtensionsType { get; }
    }
}
