// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags the local, inline "zip slip" shape where an archive entry name builds the destination of a
/// file write with no containment check (SES1304). The rule reports a <c>System.IO.Path.Combine(...)</c>
/// call, or a string <c>+</c> concatenation, that takes an archive-entry-name expression -- a
/// <c>ZipArchiveEntry.FullName</c> or a <c>System.Formats.Tar.TarEntry.Name</c> -- <b>directly</b> as an
/// argument/operand and is <b>itself</b> the destination argument of an extraction or file-writing sink in
/// the same expression: <c>entry.ExtractToFile(...)</c>, <c>File.Create</c>/<c>OpenWrite</c>/
/// <c>WriteAllBytes</c>/<c>WriteAllText</c>, or <c>new FileStream(...)</c>. Because the join and the write
/// sit in one expression, there is structurally no room for a <c>Path.GetFullPath</c> + prefix guard, so
/// the shape is provably unguarded; the multi-statement form (entry name copied into a local, then written
/// later) would require data-flow to clear of a guard and is deliberately not reported.
/// </summary>
/// <remarks>
/// The zip <c>ExtractToFile</c>, <c>FileStream</c>, and <c>File.OpenWrite</c> sinks are already reported by
/// the .NET SDK's built-in archive path-traversal analysis, which recognises <c>ZipArchiveEntry</c> as a
/// tainted archive source but not <c>System.Formats.Tar.TarEntry</c>. To avoid duplicate diagnostics this
/// rule reports those three sinks only for Tar entries, and reports the remaining file-writing sinks
/// (<c>File.Create</c>, <c>File.WriteAllBytes</c>, <c>File.WriteAllText</c>) for both entry types. The two
/// archive-entry types are gated independently, so a project that references only one still gets that
/// surface; a project that references neither registers nothing and pays nothing.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1304ArchiveEntryPathTraversalAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the zip archive entry whose <c>FullName</c> is a tainted archive source.</summary>
    private const string ZipArchiveEntryMetadataName = "System.IO.Compression.ZipArchiveEntry";

    /// <summary>The metadata name of the Tar entry whose <c>Name</c> is a tainted archive source.</summary>
    private const string TarEntryMetadataName = "System.Formats.Tar.TarEntry";

    /// <summary>The metadata name of the <c>System.IO.File</c> owner of the guarded file-writing sinks.</summary>
    private const string FileMetadataName = "System.IO.File";

    /// <summary>The metadata name of the <c>System.IO.FileStream</c> sink type.</summary>
    private const string FileStreamMetadataName = "System.IO.FileStream";

    /// <summary>The metadata name of <c>System.IO.Path</c>, whose <c>Combine</c> join is confirmed.</summary>
    private const string PathMetadataName = "System.IO.Path";

    /// <summary>The name of the <c>Path.Combine</c> join method whose result is a candidate destination.</summary>
    private const string CombineMethodName = "Combine";

    /// <summary>The name of the archive <c>ExtractToFile</c> sink method.</summary>
    private const string ExtractToFileMethodName = "ExtractToFile";

    /// <summary>The name of the <c>File.OpenWrite</c> sink method.</summary>
    private const string OpenWriteMethodName = "OpenWrite";

    /// <summary>The name of the <c>File.Create</c> sink method.</summary>
    private const string CreateMethodName = "Create";

    /// <summary>The name of the <c>File.WriteAllBytes</c> sink method.</summary>
    private const string WriteAllBytesMethodName = "WriteAllBytes";

    /// <summary>The name of the <c>File.WriteAllText</c> sink method.</summary>
    private const string WriteAllTextMethodName = "WriteAllText";

    /// <summary>The simple type name of the <c>FileStream</c> object-creation sink.</summary>
    private const string FileStreamTypeName = "FileStream";

    /// <summary>The name of the zip entry's <c>FullName</c> path property.</summary>
    private const string FullNamePropertyName = "FullName";

    /// <summary>The name of the Tar entry's <c>Name</c> path property.</summary>
    private const string NamePropertyName = "Name";

    /// <summary>The <c>path:</c> destination-parameter name honoured when a call passes its path by name.</summary>
    private const string PathParameterName = "path";

    /// <summary>The <c>destinationFileName:</c> destination-parameter name honoured on <c>ExtractToFile</c>.</summary>
    private const string DestinationFileNameParameterName = "destinationFileName";

    /// <summary>How an in-box archive path-traversal analysis treats a sink for a zip entry.</summary>
    private enum SinkKind
    {
        /// <summary>Not a recognised write sink.</summary>
        None,

        /// <summary>A sink already covered for zip entries elsewhere; reported here for Tar entries only.</summary>
        ZipCovered,

        /// <summary>A file-writing sink reported for both zip and Tar entries.</summary>
        AlwaysReport,
    }

    /// <summary>Which archive-entry type an entry-name access binds to.</summary>
    private enum EntryKind
    {
        /// <summary>Not an archive-entry name.</summary>
        None,

        /// <summary>A <c>ZipArchiveEntry.FullName</c>.</summary>
        Zip,

        /// <summary>A <c>System.Formats.Tar.TarEntry.Name</c>.</summary>
        Tar,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.ArchiveEntryPathTraversal);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var types = ArchivePathTypes.Resolve(start.Compilation);
            if (types is null)
            {
                return;
            }

            var resolved = types.Value;
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, resolved), SyntaxKind.InvocationExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeObjectCreation(nodeContext, resolved), SyntaxKind.ObjectCreationExpression);
        });
    }

    /// <summary>Reports SES1304 for an extraction/file-writing method whose destination joins an archive entry name.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The archive/path types resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, ArchivePathTypes types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.<sink>(...)' call carrying at least one argument whose destination
        // argument syntactically joins a '.FullName'/'.Name' member access via Path.Combine or '+'.
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        var sinkKind = ClassifySink(methodName);
        if (sinkKind == SinkKind.None
            || invocation.ArgumentList.Arguments.Count == 0
            || GetDestinationArgument(invocation.ArgumentList) is not { } destination
            || !TryGetArchiveEntryCombine(destination, out var combined, out var entryAccess))
        {
            return;
        }

        var entryKind = ClassifyEntry(context.SemanticModel, entryAccess, types, context.CancellationToken);
        if (!ShouldReport(entryKind, sinkKind)
            || !CombineBinds(context.SemanticModel, combined, types, context.CancellationToken)
            || !InvocationSinkBinds(context.SemanticModel, invocation, methodName, types, context.CancellationToken))
        {
            return;
        }

        Report(context, combined, entryKind);
    }

    /// <summary>Reports SES1304 for a <c>new FileStream(...)</c> whose destination joins an archive entry name.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The archive/path types resolved for the compilation.</param>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, ArchivePathTypes types)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        // Syntactic prefilter: 'new FileStream(<dest>, ...)' whose destination joins a '.FullName'/'.Name' access.
        if (creation.ArgumentList is not { } argumentList
            || argumentList.Arguments.Count == 0
            || !string.Equals(GetSimpleTypeName(creation.Type), FileStreamTypeName, StringComparison.Ordinal)
            || GetDestinationArgument(argumentList) is not { } destination
            || !TryGetArchiveEntryCombine(destination, out var combined, out var entryAccess))
        {
            return;
        }

        // FileStream is one of the zip-covered sinks, so only a Tar entry is reported here.
        if (ClassifyEntry(context.SemanticModel, entryAccess, types, context.CancellationToken) != EntryKind.Tar
            || !CombineBinds(context.SemanticModel, combined, types, context.CancellationToken)
            || !FileStreamSinkBinds(context.SemanticModel, creation, types, context.CancellationToken))
        {
            return;
        }

        Report(context, combined, EntryKind.Tar);
    }

    /// <summary>Confirms an object creation binds to a <c>System.IO.FileStream</c> constructor.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="creation">The object-creation sink.</param>
    /// <param name="types">The archive/path types resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the creation binds to a <c>FileStream</c> constructor.</returns>
    private static bool FileStreamSinkBinds(SemanticModel model, ObjectCreationExpressionSyntax creation, ArchivePathTypes types, CancellationToken cancellationToken)
        => types.FileStream is { } fileStreamType
            && model.GetSymbolInfo(creation, cancellationToken).Symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            && SymbolEqualityComparer.Default.Equals(constructor.ContainingType, fileStreamType);

    /// <summary>Reports the diagnostic on the combined destination expression.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="combined">The Path.Combine/concatenation expression that joins the entry name.</param>
    /// <param name="entryKind">The archive-entry kind driving the message argument.</param>
    private static void Report(SyntaxNodeAnalysisContext context, ExpressionSyntax combined, EntryKind entryKind)
        => context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.ArchiveEntryPathTraversal,
            combined.SyntaxTree,
            combined.Span,
            entryKind == EntryKind.Zip ? "ZipArchiveEntry" : "TarEntry"));

    /// <summary>Classifies an invoked member name as an extraction/file-writing sink.</summary>
    /// <param name="methodName">The invoked member's identifier text.</param>
    /// <returns>The sink kind, or <see cref="SinkKind.None"/> when the name is not a guarded sink.</returns>
    private static SinkKind ClassifySink(string methodName)
        => methodName switch
        {
            ExtractToFileMethodName or OpenWriteMethodName => SinkKind.ZipCovered,
            CreateMethodName or WriteAllBytesMethodName or WriteAllTextMethodName => SinkKind.AlwaysReport,
            _ => SinkKind.None,
        };

    /// <summary>Returns whether a bound entry/sink pair should be reported (avoiding the zip surface already covered elsewhere).</summary>
    /// <param name="entryKind">The archive-entry kind.</param>
    /// <param name="sinkKind">The sink kind.</param>
    /// <returns><see langword="true"/> when the pair is in this rule's complement surface.</returns>
    private static bool ShouldReport(EntryKind entryKind, SinkKind sinkKind)
        => sinkKind == SinkKind.ZipCovered
            ? entryKind == EntryKind.Tar
            : entryKind is EntryKind.Zip or EntryKind.Tar;

    /// <summary>Returns the destination argument, honouring an explicit <c>path:</c>/<c>destinationFileName:</c> name.</summary>
    /// <param name="argumentList">The invocation or object-creation argument list.</param>
    /// <returns>The destination expression, or <see langword="null"/> when it cannot be identified positionally.</returns>
    private static ExpressionSyntax? GetDestinationArgument(ArgumentListSyntax argumentList)
    {
        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { Name.Identifier.ValueText: PathParameterName or DestinationFileNameParameterName })
            {
                return arguments[i].Expression;
            }
        }

        // The destination path is the first parameter of every guarded sink, so a leading positional argument is it.
        return arguments[0].NameColon is null ? arguments[0].Expression : null;
    }

    /// <summary>Syntactically detects a destination that joins a <c>.FullName</c>/<c>.Name</c> access via Path.Combine or <c>+</c>.</summary>
    /// <param name="destination">The destination argument expression.</param>
    /// <param name="combined">The Path.Combine invocation or concatenation expression, when matched.</param>
    /// <param name="entryAccess">The <c>.FullName</c>/<c>.Name</c> member access joined into it, when matched.</param>
    /// <returns><see langword="true"/> when the destination is a candidate join.</returns>
    private static bool TryGetArchiveEntryCombine(ExpressionSyntax destination, out ExpressionSyntax combined, out MemberAccessExpressionSyntax entryAccess)
    {
        combined = destination;
        entryAccess = null!;

        if (destination is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: CombineMethodName } } combineInvocation)
        {
            var arguments = combineInvocation.ArgumentList.Arguments;
            for (var i = 0; i < arguments.Count; i++)
            {
                if (IsEntryNameAccess(arguments[i].Expression, out entryAccess))
                {
                    return true;
                }
            }

            return false;
        }

        return destination.IsKind(SyntaxKind.AddExpression)
            && destination is BinaryExpressionSyntax concatenation
            && (IsEntryNameAccess(concatenation.Right, out entryAccess) || IsEntryNameAccess(concatenation.Left, out entryAccess));
    }

    /// <summary>Returns whether an expression is a bare <c>.FullName</c>/<c>.Name</c> member access.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="entryAccess">The member access, when matched.</param>
    /// <returns><see langword="true"/> for a <c>.FullName</c>/<c>.Name</c> member access.</returns>
    private static bool IsEntryNameAccess(ExpressionSyntax expression, out MemberAccessExpressionSyntax entryAccess)
    {
        if (expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: FullNamePropertyName or NamePropertyName } memberAccess)
        {
            entryAccess = memberAccess;
            return true;
        }

        entryAccess = null!;
        return false;
    }

    /// <summary>Binds an entry-name access to <c>ZipArchiveEntry.FullName</c> or <c>TarEntry.Name</c>/<c>FullName</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="entryAccess">The entry-name member access.</param>
    /// <param name="types">The archive/path types resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The archive-entry kind, or <see cref="EntryKind.None"/> when it is not an archive-entry name.</returns>
    private static EntryKind ClassifyEntry(SemanticModel model, MemberAccessExpressionSyntax entryAccess, ArchivePathTypes types, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(entryAccess, cancellationToken).Symbol is not IPropertySymbol property)
        {
            return EntryKind.None;
        }

        var container = property.ContainingType;
        if (types.ZipArchiveEntry is { } zip
            && string.Equals(property.Name, FullNamePropertyName, StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(container, zip))
        {
            return EntryKind.Zip;
        }

        if (types.TarEntry is { } tar
            && SymbolEqualityComparer.Default.Equals(container, tar))
        {
            return EntryKind.Tar;
        }

        return EntryKind.None;
    }

    /// <summary>Confirms a Path.Combine destination binds to <c>System.IO.Path.Combine</c> (a concatenation needs no binding).</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="combined">The Path.Combine invocation or concatenation expression.</param>
    /// <param name="types">The archive/path types resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the join is a bound Path.Combine or a string concatenation.</returns>
    private static bool CombineBinds(SemanticModel model, ExpressionSyntax combined, ArchivePathTypes types, CancellationToken cancellationToken)
    {
        if (combined is not InvocationExpressionSyntax combineInvocation)
        {
            return true;
        }

        return types.Path is { } path
            && model.GetSymbolInfo(combineInvocation, cancellationToken).Symbol is IMethodSymbol { Name: CombineMethodName } method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, path);
    }

    /// <summary>Confirms an invocation sink binds to <c>System.IO.File</c> or the Tar <c>ExtractToFile</c> owner.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The sink invocation.</param>
    /// <param name="methodName">The invoked member's identifier text.</param>
    /// <param name="types">The archive/path types resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the sink binds to a recognised file-writing owner.</returns>
    private static bool InvocationSinkBinds(SemanticModel model, InvocationExpressionSyntax invocation, string methodName, ArchivePathTypes types, CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
            && (string.Equals(methodName, ExtractToFileMethodName, StringComparison.Ordinal)

                // ExtractToFile only reaches here for a Tar entry (the zip surface is filtered by
                // ShouldReport); it is an instance method on TarEntry, so its container is the resolved type.
                ? types.TarEntry is { } tar && SymbolEqualityComparer.Default.Equals(method.ContainingType, tar)
                : types.File is { } file && SymbolEqualityComparer.Default.Equals(method.ContainingType, file));

    /// <summary>Returns the simple (unqualified) type name of a type syntax used in an object creation.</summary>
    /// <param name="type">The object-creation type syntax.</param>
    /// <returns>The simple type name, or <see langword="null"/> when it cannot be read cheaply.</returns>
    private static string? GetSimpleTypeName(TypeSyntax type)
        => type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            _ => null,
        };

    /// <summary>The archive/path types resolved once per compilation, gated on at least one archive-entry type.</summary>
    private readonly record struct ArchivePathTypes(
        INamedTypeSymbol? ZipArchiveEntry,
        INamedTypeSymbol? TarEntry,
        INamedTypeSymbol? File,
        INamedTypeSymbol? FileStream,
        INamedTypeSymbol? Path)
    {
        /// <summary>Resolves the archive/path types, returning <see langword="null"/> when neither entry type is present.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The resolved types, or <see langword="null"/> when the rule cannot apply.</returns>
        public static ArchivePathTypes? Resolve(Compilation compilation)
        {
            var zip = compilation.GetTypeByMetadataName(ZipArchiveEntryMetadataName);
            var tar = compilation.GetTypeByMetadataName(TarEntryMetadataName);
            if (zip is null && tar is null)
            {
                return null;
            }

            return new ArchivePathTypes(
                zip,
                tar,
                compilation.GetTypeByMetadataName(FileMetadataName),
                compilation.GetTypeByMetadataName(FileStreamMetadataName),
                compilation.GetTypeByMetadataName(PathMetadataName));
        }
    }
}
