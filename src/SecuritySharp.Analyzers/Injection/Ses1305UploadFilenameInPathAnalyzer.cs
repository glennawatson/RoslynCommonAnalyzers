// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a <c>Microsoft.AspNetCore.Http.IFormFile.FileName</c> read used to build a filesystem path
/// (SES1305). An uploaded file's name is fully attacker-controlled and may contain <c>..</c> segments
/// or a rooted path, so combining it into a storage path enables path traversal. The rule reports the
/// <c>.FileName</c> expression when it is a direct argument to <c>System.IO.Path.Combine</c>, an
/// operand of a <c>+</c> string concatenation that also contains a path-separator literal, or a direct
/// argument to a file-creating call (<c>System.IO.File.Create</c>/<c>OpenWrite</c>/<c>WriteAllBytes</c>/
/// <c>Copy</c> or <c>new System.IO.FileStream(...)</c>). The <c>.FileName</c> access is bound and its
/// containing type must be <c>IFormFile</c> itself, so a same-named property on another type is ignored.
/// This is a purely local, syntactic shape (no data-flow); a value sanitized with
/// <c>Path.GetFileName(file.FileName)</c> is not flagged because <c>.FileName</c> is then a direct
/// argument to <c>GetFileName</c>, not to the path sink. The <c>IFormFile</c> marker is probed once per
/// compilation; a project without ASP.NET Core registers nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1305UploadFilenameInPathAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the attacker-controlled upload property this rule tracks.</summary>
    private const string FileNamePropertyName = "FileName";

    /// <summary>The <c>System.IO.Path.Combine</c> method name treated as a path sink.</summary>
    private const string CombineMethodName = "Combine";

    /// <summary>The <c>System.IO.File.Create</c> method name treated as a path sink.</summary>
    private const string CreateMethodName = "Create";

    /// <summary>The <c>System.IO.File.OpenWrite</c> method name treated as a path sink.</summary>
    private const string OpenWriteMethodName = "OpenWrite";

    /// <summary>The <c>System.IO.File.WriteAllBytes</c> method name treated as a path sink.</summary>
    private const string WriteAllBytesMethodName = "WriteAllBytes";

    /// <summary>The <c>System.IO.File.Copy</c> method name treated as a path sink.</summary>
    private const string CopyMethodName = "Copy";

    /// <summary>The simple type name of the <c>System.IO.FileStream</c> constructor sink.</summary>
    private const string FileStreamTypeName = "FileStream";

    /// <summary>The metadata name of the upload marker whose presence gates the rule.</summary>
    private const string FormFileMetadataName = "Microsoft.AspNetCore.Http.IFormFile";

    /// <summary>The metadata name of <c>System.IO.Path</c>.</summary>
    private const string PathMetadataName = "System.IO.Path";

    /// <summary>The metadata name of <c>System.IO.File</c>.</summary>
    private const string FileMetadataName = "System.IO.File";

    /// <summary>The metadata name of <c>System.IO.FileStream</c>.</summary>
    private const string FileStreamMetadataName = "System.IO.FileStream";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.UploadFilenameInPath);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var sinkTypes = GetSinkTypes(start.Compilation);
            if (sinkTypes is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMemberAccess(nodeContext, sinkTypes), SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    /// <summary>Reports SES1305 for an <c>IFormFile.FileName</c> read that flows straight into a path sink.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="sinkTypes">The gated marker and path-sink types resolved for the compilation.</param>
    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context, SinkTypes sinkTypes)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Syntactic prefilter: a '.FileName' member access.
        if (memberAccess.Name.Identifier.ValueText != FileNamePropertyName)
        {
            return;
        }

        // Syntactic sink classification, cheapest first: a direct argument to a name-matching call, or an
        // operand of a path-forming '+' concatenation. Neither touches the semantic model.
        var sinkCall = GetSyntacticSinkCall(memberAccess);
        var isPathConcat = sinkCall is null && IsPathFormingConcatOperand(memberAccess);
        if (sinkCall is null && !isPathConcat)
        {
            return;
        }

        // Bind '.FileName' and confirm the read is 'IFormFile.FileName' (the high-signal, rare condition).
        if (context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol is not IPropertySymbol { Name: FileNamePropertyName } property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, sinkTypes.FormFile))
        {
            return;
        }

        // For the call sinks, bind the enclosing invocation/construction to confirm the framework member.
        if (sinkCall is not null && !IsConfirmedSinkCall(context.SemanticModel, sinkCall, sinkTypes, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.UploadFilenameInPath,
            memberAccess.SyntaxTree,
            memberAccess.Span,
            memberAccess.ToString()));
    }

    /// <summary>Returns the enclosing call when <c>.FileName</c> is a direct argument to a name-matching sink.</summary>
    /// <param name="memberAccess">The <c>.FileName</c> member access.</param>
    /// <returns>The invocation or object-creation sink node, or <see langword="null"/> when the shape does not match.</returns>
    private static SyntaxNode? GetSyntacticSinkCall(MemberAccessExpressionSyntax memberAccess)
    {
        // The access must be the direct expression of an argument: Sink(..., file.FileName, ...).
        if (memberAccess.Parent is not ArgumentSyntax argument
            || argument.Parent is not ArgumentListSyntax argumentList)
        {
            return null;
        }

        return argumentList.Parent switch
        {
            InvocationExpressionSyntax invocation when IsPathSinkMethodName(invocation.Expression) => invocation,
            ObjectCreationExpressionSyntax creation when GetTypeName(creation.Type) == FileStreamTypeName => creation,
            _ => null,
        };
    }

    /// <summary>Returns whether an invocation's callee is syntactically named like a path sink method.</summary>
    /// <param name="callee">The invocation's callee expression.</param>
    /// <returns><see langword="true"/> when the simple method name matches a path sink.</returns>
    private static bool IsPathSinkMethodName(ExpressionSyntax callee)
        => GetInvokedName(callee) is CombineMethodName or CreateMethodName or OpenWriteMethodName or WriteAllBytesMethodName or CopyMethodName;

    /// <summary>Returns the simple method name an invocation targets, ignoring the receiver.</summary>
    /// <param name="callee">The invocation's callee expression.</param>
    /// <returns>The simple method name, or <see langword="null"/> when it cannot be read syntactically.</returns>
    private static string? GetInvokedName(ExpressionSyntax callee)
        => callee switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns the right-most simple identifier of a type name.</summary>
    /// <param name="type">The constructed type syntax.</param>
    /// <returns>The simple type name, or <see langword="null"/> when it cannot be read syntactically.</returns>
    private static string? GetTypeName(TypeSyntax type)
        => type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns whether a syntactic sink call binds to a framework path-building member.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="call">The invocation or object-creation sink node.</param>
    /// <param name="sinkTypes">The gated path-sink types resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the call is a gated <c>Path</c>/<c>File</c>/<c>FileStream</c> sink.</returns>
    private static bool IsConfirmedSinkCall(SemanticModel model, SyntaxNode call, SinkTypes sinkTypes, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(call, cancellationToken).Symbol is not IMethodSymbol method)
        {
            return false;
        }

        var containingType = method.ContainingType;

        // 'Path.Combine(..., file.FileName, ...)'.
        if (method.Name == CombineMethodName && SymbolEqualityComparer.Default.Equals(containingType, sinkTypes.Path))
        {
            return true;
        }

        // 'File.Create/OpenWrite/WriteAllBytes/Copy(file.FileName, ...)'.
        if (SymbolEqualityComparer.Default.Equals(containingType, sinkTypes.File)
            && method.Name is CreateMethodName or OpenWriteMethodName or WriteAllBytesMethodName or CopyMethodName)
        {
            return true;
        }

        // 'new FileStream(file.FileName, ...)'.
        return method.MethodKind == MethodKind.Constructor && SymbolEqualityComparer.Default.Equals(containingType, sinkTypes.FileStream);
    }

    /// <summary>Returns whether <c>.FileName</c> is an operand of a <c>+</c> chain that builds a path.</summary>
    /// <param name="memberAccess">The <c>.FileName</c> member access.</param>
    /// <returns><see langword="true"/> when the enclosing additive chain contains a path-separator string literal.</returns>
    private static bool IsPathFormingConcatOperand(MemberAccessExpressionSyntax memberAccess)
    {
        // The access (past any wrapping parentheses) must be an operand of a string '+'.
        if (OutermostParenthesized(memberAccess).Parent is not BinaryExpressionSyntax binary || !binary.IsKind(SyntaxKind.AddExpression))
        {
            return false;
        }

        // Climb to the root of the '+' chain, then require a path-separator literal somewhere in it so a
        // non-path concatenation (a log message, a display string) is not flagged.
        var root = binary;
        while (OutermostParenthesized(root).Parent is BinaryExpressionSyntax outer && outer.IsKind(SyntaxKind.AddExpression))
        {
            root = outer;
        }

        return AdditiveChainHasSeparatorLiteral(root);
    }

    /// <summary>Returns the outer-most parenthesized expression wrapping a node, or the node itself.</summary>
    /// <param name="expression">The inner expression.</param>
    /// <returns>The outer-most wrapping expression whose parent should be inspected.</returns>
    private static ExpressionSyntax OutermostParenthesized(ExpressionSyntax expression)
    {
        var current = expression;
        while (current.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized;
        }

        return current;
    }

    /// <summary>Returns whether an additive/parenthesized subtree contains a path-separator string literal.</summary>
    /// <param name="expression">The additive-chain node to scan.</param>
    /// <returns><see langword="true"/> when a string literal operand contains a directory separator.</returns>
    private static bool AdditiveChainHasSeparatorLiteral(ExpressionSyntax expression)
        => expression switch
        {
            ParenthesizedExpressionSyntax parenthesized => AdditiveChainHasSeparatorLiteral(parenthesized.Expression),
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) =>
                AdditiveChainHasSeparatorLiteral(binary.Left) || AdditiveChainHasSeparatorLiteral(binary.Right),
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                ContainsPathSeparator(literal.Token.ValueText),
            _ => false,
        };

    /// <summary>Returns whether a string contains a forward or backslash directory separator.</summary>
    /// <param name="value">The string-literal text.</param>
    /// <returns><see langword="true"/> when a path separator is present.</returns>
    private static bool ContainsPathSeparator(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (character is '/' or '\\')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves the upload marker and path-sink types present in the compilation.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>The resolved sink types, or <see langword="null"/> when the <c>IFormFile</c> marker is absent.</returns>
    private static SinkTypes? GetSinkTypes(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName(FormFileMetadataName) is not { } formFile)
        {
            return null;
        }

        return new SinkTypes(
            formFile,
            compilation.GetTypeByMetadataName(PathMetadataName),
            compilation.GetTypeByMetadataName(FileMetadataName),
            compilation.GetTypeByMetadataName(FileStreamMetadataName));
    }

    /// <summary>The marker and path-sink types resolved once per compilation.</summary>
    private sealed class SinkTypes
    {
        /// <summary>Initializes a new instance of the <see cref="SinkTypes"/> class.</summary>
        /// <param name="formFile">The resolved <c>IFormFile</c> marker type.</param>
        /// <param name="path">The resolved <c>System.IO.Path</c> type, if present.</param>
        /// <param name="file">The resolved <c>System.IO.File</c> type, if present.</param>
        /// <param name="fileStream">The resolved <c>System.IO.FileStream</c> type, if present.</param>
        public SinkTypes(INamedTypeSymbol formFile, INamedTypeSymbol? path, INamedTypeSymbol? file, INamedTypeSymbol? fileStream)
        {
            FormFile = formFile;
            Path = path;
            File = file;
            FileStream = fileStream;
        }

        /// <summary>Gets the resolved <c>IFormFile</c> marker type.</summary>
        public INamedTypeSymbol FormFile { get; }

        /// <summary>Gets the resolved <c>System.IO.Path</c> type, or <see langword="null"/> when absent.</summary>
        public INamedTypeSymbol? Path { get; }

        /// <summary>Gets the resolved <c>System.IO.File</c> type, or <see langword="null"/> when absent.</summary>
        public INamedTypeSymbol? File { get; }

        /// <summary>Gets the resolved <c>System.IO.FileStream</c> type, or <see langword="null"/> when absent.</summary>
        public INamedTypeSymbol? FileStream { get; }
    }
}
