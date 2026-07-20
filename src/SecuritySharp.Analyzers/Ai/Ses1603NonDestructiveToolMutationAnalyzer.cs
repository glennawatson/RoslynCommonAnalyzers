// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a model-facing tool method that declares itself read-only or non-destructive yet calls a
/// state-changing API (SES1603). A host that automates tool use reads a tool's annotations to decide
/// whether it may invoke the tool without asking the user; a tool marked read-only or non-destructive
/// can be auto-run. This rule reports the destructive call inside a method carrying
/// <c>[McpServerTool]</c> whose <c>ReadOnly</c> is explicitly set to <see langword="true"/> or whose
/// <c>Destructive</c> is explicitly set to <see langword="false"/> -- the two members that express the
/// safety promise. An unset hint is left alone: the attribute stores those hints in nullable backing
/// fields, so an unset value means "no promise" (the host assumes destructive) rather than "safe". The
/// destructive set is a curated, local match: <c>File.Delete</c>/<c>File.WriteAllText</c>,
/// <c>Directory.Delete</c>, <c>Process.Start</c>, <c>DbCommand.ExecuteNonQuery</c>, an Entity Framework
/// <c>ExecuteSqlRaw</c>/<c>ExecuteSqlInterpolated</c>/<c>ExecuteDelete</c>/<c>ExecuteUpdate</c>, or a
/// <c>DbContext.SaveChanges</c> (with their async twins). The whole rule is gated on the tool attribute
/// resolving, and each destructive sink is matched only against the types actually present in the
/// compilation, so a project without the model-tool SDK or without a given sink registers nothing and
/// pays nothing. The Semantic Kernel <c>[KernelFunction]</c> attribute is not covered: it carries no
/// read-only or destructive member, so a tool declared with it makes no safety promise this rule could
/// contradict.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1603NonDestructiveToolMutationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the tool attribute whose read-only/destructive hints are honoured.</summary>
    private const string McpServerToolMetadataName = "ModelContextProtocol.Server.McpServerToolAttribute";

    /// <summary>The named-argument that promises the tool does not modify its environment.</summary>
    private const string ReadOnlyArgumentName = "ReadOnly";

    /// <summary>The named-argument that promises the tool performs no destructive update.</summary>
    private const string DestructiveArgumentName = "Destructive";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.NonDestructiveToolMutation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var toolAttribute = start.Compilation.GetTypeByMetadataName(McpServerToolMetadataName);
            if (toolAttribute is null)
            {
                return;
            }

            var sinks = DestructiveSinks.Resolve(start.Compilation);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeMethod(nodeContext, toolAttribute, sinks),
                SyntaxKind.MethodDeclaration);
        });
    }

    /// <summary>Reports SES1603 for a read-only/non-destructive tool method whose body calls a destructive API.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="toolAttribute">The resolved model-tool attribute type.</param>
    /// <param name="sinks">The destructive sink types resolved for the compilation.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, INamedTypeSymbol toolAttribute, DestructiveSinks sinks)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Syntactic prefilter: a tool method must carry the attribute, and must have a body to scan.
        if (method.AttributeLists.Count == 0
            || (method.Body is null && method.ExpressionBody is null)
            || !DeclaresSafeTool(context, method.AttributeLists, toolAttribute))
        {
            return;
        }

        var scan = FindDestructiveCall(context, method, sinks);
        if (scan.Found is not { } destructiveCall || scan.Callee is not { } callee)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.NonDestructiveToolMutation,
            destructiveCall.SyntaxTree,
            destructiveCall.Span,
            callee.ContainingType.Name + "." + callee.Name));
    }

    /// <summary>Returns whether a declaration carries a tool attribute that explicitly promises read-only or non-destructive behaviour.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="attributeLists">The method's attribute lists.</param>
    /// <param name="toolAttribute">The resolved model-tool attribute type.</param>
    /// <returns><see langword="true"/> when a tool attribute sets <c>ReadOnly = true</c> or <c>Destructive = false</c>.</returns>
    private static bool DeclaresSafeTool(
        SyntaxNodeAnalysisContext context,
        SyntaxList<AttributeListSyntax> attributeLists,
        INamedTypeSymbol toolAttribute)
    {
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var attribute = attributes[j];
                if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol { ContainingType: { } attributeType }
                    || !IsOrDerivesFrom(attributeType, toolAttribute))
                {
                    continue;
                }

                if (PromisesSafety(context, attribute))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a tool attribute sets <c>ReadOnly = true</c> or <c>Destructive = false</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="attribute">The bound tool attribute.</param>
    /// <returns><see langword="true"/> when an explicit safety hint is present.</returns>
    private static bool PromisesSafety(SyntaxNodeAnalysisContext context, AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is not { } argumentList)
        {
            return false;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (argument.NameEquals is not { Name.Identifier.ValueText: { } name }
                || context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken).Value is not bool value)
            {
                continue;
            }

            if ((name == ReadOnlyArgumentName && value) || (name == DestructiveArgumentName && !value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Scans a tool method body for the first destructive call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="method">The tool method declaration.</param>
    /// <param name="sinks">The destructive sink types resolved for the compilation.</param>
    /// <returns>The scan state, whose <see cref="DestructiveScan.Found"/> holds the first destructive call when present.</returns>
    private static DestructiveScan FindDestructiveCall(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method, DestructiveSinks sinks)
    {
        var scan = new DestructiveScan(context.SemanticModel, sinks, context.CancellationToken);
        SyntaxNode body = method.Body is { } block ? block : method.ExpressionBody!;
        DescendantTraversalHelper.VisitDescendants<InvocationExpressionSyntax, DestructiveScan>(body, ref scan, VisitInvocation);
        return scan;
    }

    /// <summary>Records the first invocation whose bound callee is a destructive API and stops the walk.</summary>
    /// <param name="invocation">The current invocation.</param>
    /// <param name="scan">The threaded scan state.</param>
    /// <returns><see langword="true"/> to continue, or <see langword="false"/> to stop at the first match.</returns>
    private static bool VisitInvocation(InvocationExpressionSyntax invocation, ref DestructiveScan scan)
    {
        if (scan.Model.GetSymbolInfo(invocation, scan.CancellationToken).Symbol is not IMethodSymbol method
            || !scan.Sinks.IsDestructive(method))
        {
            return true;
        }

        scan.Found = invocation;
        scan.Callee = method;
        return false;
    }

    /// <summary>Returns whether an attribute class is, or derives from, a marker attribute type.</summary>
    /// <param name="attributeType">The bound attribute class.</param>
    /// <param name="marker">The marker attribute type to match.</param>
    /// <returns><see langword="true"/> when the attribute is the marker or a subclass of it.</returns>
    private static bool IsOrDerivesFrom(INamedTypeSymbol attributeType, INamedTypeSymbol marker)
    {
        for (var current = attributeType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, marker))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The mutable state threaded through a method-body scan for a destructive call.</summary>
    private sealed class DestructiveScan
    {
        /// <summary>Initializes a new instance of the <see cref="DestructiveScan"/> class.</summary>
        /// <param name="model">The semantic model for the analysed tree.</param>
        /// <param name="sinks">The destructive sink types resolved for the compilation.</param>
        /// <param name="cancellationToken">A token that cancels the walk.</param>
        public DestructiveScan(SemanticModel model, DestructiveSinks sinks, CancellationToken cancellationToken)
        {
            Model = model;
            Sinks = sinks;
            CancellationToken = cancellationToken;
            Found = null;
            Callee = null;
        }

        /// <summary>Gets the semantic model for the analysed tree.</summary>
        public SemanticModel Model { get; }

        /// <summary>Gets the destructive sink types resolved for the compilation.</summary>
        public DestructiveSinks Sinks { get; }

        /// <summary>Gets the token that cancels the walk.</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>Gets or sets the first destructive invocation found.</summary>
        public InvocationExpressionSyntax? Found { get; set; }

        /// <summary>Gets or sets the bound callee of the first destructive invocation found.</summary>
        public IMethodSymbol? Callee { get; set; }
    }

    /// <summary>The destructive sink types resolved once per compilation, matched by bound symbol.</summary>
    private sealed class DestructiveSinks
    {
        /// <summary>The Entity Framework namespace whose bulk-mutation extensions are treated as destructive.</summary>
        private const string EntityFrameworkNamespace = "Microsoft.EntityFrameworkCore";

        /// <summary>The resolved <c>System.IO.File</c> type, or <see langword="null"/> when absent.</summary>
        private readonly INamedTypeSymbol? _file;

        /// <summary>The resolved <c>System.IO.Directory</c> type, or <see langword="null"/> when absent.</summary>
        private readonly INamedTypeSymbol? _directory;

        /// <summary>The resolved <c>System.Diagnostics.Process</c> type, or <see langword="null"/> when absent.</summary>
        private readonly INamedTypeSymbol? _process;

        /// <summary>The resolved <c>System.Data.Common.DbCommand</c> type, or <see langword="null"/> when absent.</summary>
        private readonly INamedTypeSymbol? _dbCommand;

        /// <summary>The resolved Entity Framework <c>DbContext</c> type, or <see langword="null"/> when absent.</summary>
        private readonly INamedTypeSymbol? _dbContext;

        /// <summary>Initializes a new instance of the <see cref="DestructiveSinks"/> class.</summary>
        /// <param name="file">The resolved <c>System.IO.File</c> type, if present.</param>
        /// <param name="directory">The resolved <c>System.IO.Directory</c> type, if present.</param>
        /// <param name="process">The resolved <c>System.Diagnostics.Process</c> type, if present.</param>
        /// <param name="dbCommand">The resolved <c>System.Data.Common.DbCommand</c> type, if present.</param>
        /// <param name="dbContext">The resolved Entity Framework <c>DbContext</c> type, if present.</param>
        private DestructiveSinks(
            INamedTypeSymbol? file,
            INamedTypeSymbol? directory,
            INamedTypeSymbol? process,
            INamedTypeSymbol? dbCommand,
            INamedTypeSymbol? dbContext)
        {
            _file = file;
            _directory = directory;
            _process = process;
            _dbCommand = dbCommand;
            _dbContext = dbContext;
        }

        /// <summary>Resolves the destructive sink types present in a compilation.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The resolved sinks.</returns>
        public static DestructiveSinks Resolve(Compilation compilation) =>
            new(
                compilation.GetTypeByMetadataName("System.IO.File"),
                compilation.GetTypeByMetadataName("System.IO.Directory"),
                compilation.GetTypeByMetadataName("System.Diagnostics.Process"),
                compilation.GetTypeByMetadataName("System.Data.Common.DbCommand"),
                compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext"));

        /// <summary>Returns whether a bound method is one of the curated destructive sinks.</summary>
        /// <param name="method">The bound callee.</param>
        /// <returns><see langword="true"/> when the callee deletes, overwrites, executes, or persists state.</returns>
        public bool IsDestructive(IMethodSymbol method)
        {
            var containingType = method.ContainingType;
            return containingType is not null
                && (IsFileSystemSink(method.Name, containingType)
                    || IsProcessSink(method.Name, containingType)
                    || IsDatabaseSink(method.Name, containingType));
        }

        /// <summary>Returns whether a method name is an Entity Framework bulk-mutation extension.</summary>
        /// <param name="name">The method name.</param>
        /// <returns><see langword="true"/> for a raw-SQL, bulk-delete, or bulk-update extension.</returns>
        private static bool IsEntityFrameworkBulkMutation(string name) => name switch
        {
            "ExecuteSqlRaw" or "ExecuteSqlRawAsync" => true,
            "ExecuteSqlInterpolated" or "ExecuteSqlInterpolatedAsync" => true,
            "ExecuteDelete" or "ExecuteDeleteAsync" => true,
            "ExecuteUpdate" or "ExecuteUpdateAsync" => true,
            _ => false,
        };

        /// <summary>Returns whether a type is declared in the Entity Framework namespace.</summary>
        /// <param name="type">The containing type of a bound extension method.</param>
        /// <returns><see langword="true"/> when the type lives in <c>Microsoft.EntityFrameworkCore</c>.</returns>
        private static bool IsEntityFrameworkMember(INamedTypeSymbol type) =>
            type.ContainingNamespace is { IsGlobalNamespace: false } ns && ns.ToDisplayString() == EntityFrameworkNamespace;

        /// <summary>Returns whether a call deletes or overwrites a file or directory.</summary>
        /// <param name="name">The callee name.</param>
        /// <param name="containingType">The callee's containing type.</param>
        /// <returns><see langword="true"/> for a file/directory delete or file overwrite.</returns>
        private bool IsFileSystemSink(string name, INamedTypeSymbol containingType)
        {
            if (_file is { } file && SymbolEqualityComparer.Default.Equals(containingType, file))
            {
                return name is "Delete" or "WriteAllText" or "WriteAllTextAsync";
            }

            return _directory is { } directory && SymbolEqualityComparer.Default.Equals(containingType, directory) && name == "Delete";
        }

        /// <summary>Returns whether a call starts an external process.</summary>
        /// <param name="name">The callee name.</param>
        /// <param name="containingType">The callee's containing type.</param>
        /// <returns><see langword="true"/> for a <c>Process.Start</c> call.</returns>
        private bool IsProcessSink(string name, INamedTypeSymbol containingType) =>
            _process is { } process && SymbolEqualityComparer.Default.Equals(containingType, process) && name == "Start";

        /// <summary>Returns whether a call issues a destructive database command or persists changes.</summary>
        /// <param name="name">The callee name.</param>
        /// <param name="containingType">The callee's containing type.</param>
        /// <returns><see langword="true"/> for an ADO.NET non-query or an Entity Framework mutation.</returns>
        private bool IsDatabaseSink(string name, INamedTypeSymbol containingType)
        {
            if (_dbCommand is { } dbCommand && name is "ExecuteNonQuery" or "ExecuteNonQueryAsync")
            {
                return IsOrDerivesFrom(containingType, dbCommand);
            }

            if (_dbContext is not { } dbContext)
            {
                return false;
            }

            if (name is "SaveChanges" or "SaveChangesAsync")
            {
                return IsOrDerivesFrom(containingType, dbContext);
            }

            return IsEntityFrameworkBulkMutation(name) && IsEntityFrameworkMember(containingType);
        }
    }
}
