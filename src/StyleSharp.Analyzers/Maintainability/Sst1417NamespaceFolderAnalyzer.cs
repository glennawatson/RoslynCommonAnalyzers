// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a top-level namespace whose name does not mirror the file's folder path under the
/// project root (SST1417, opt-in). The expected namespace is the root namespace followed by the
/// folders between the project directory and the file, matching the SDK's the rule behaviour. The
/// project directory and root namespace come from the compiler-provided <c>build_property.*</c>
/// options, so the rule does nothing unless those are available; the root namespace can be
/// overridden with <c>stylesharp.namespace_root</c> in <c>.editorconfig</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1417NamespaceFolderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Rule-specific editorconfig key overriding the root namespace (SST1417).</summary>
    internal const string RootNamespaceSpecificKey = "stylesharp.SST1417.namespace_root";

    /// <summary>General editorconfig key overriding the root namespace.</summary>
    internal const string RootNamespaceGeneralKey = "stylesharp.namespace_root";

    /// <summary>The compiler-visible MSBuild property holding the project directory.</summary>
    private const string ProjectDirectoryKey = "build_property.ProjectDir";

    /// <summary>The compiler-visible MSBuild property holding the root namespace.</summary>
    private const string RootNamespaceBuildKey = "build_property.RootNamespace";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.NamespaceMatchesFolder);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.NamespaceDeclaration, SyntaxKind.FileScopedNamespaceDeclaration);
    }

    /// <summary>Reports SST1417 when a top-level namespace does not match the folder structure.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = (BaseNamespaceDeclarationSyntax)context.Node;
        if (node.Parent is not CompilationUnitSyntax)
        {
            return;
        }

        var filePath = node.SyntaxTree.FilePath;
        var global = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (filePath.Length == 0
            || !global.TryGetValue(ProjectDirectoryKey, out var projectDirectory)
            || projectDirectory.Length == 0
            || !TryGetRelativeDirectory(projectDirectory, filePath, out var relativeDirectory))
        {
            return;
        }

        var expected = BuildExpectedNamespace(ReadRootNamespace(context, global), relativeDirectory);
        if (expected is null || string.Equals(node.Name.ToString(), expected, StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NamespaceMatchesFolder, node.Name.GetLocation(), node.Name.ToString(), expected));
    }

    /// <summary>Extracts the folder path of a file relative to the project directory.</summary>
    /// <param name="projectDirectory">The project directory.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="relativeDirectory">The folder portion (forward-slash separated) when the file is under the project.</param>
    /// <returns><see langword="true"/> when the file lies under the project directory.</returns>
    private static bool TryGetRelativeDirectory(string projectDirectory, string filePath, out string relativeDirectory)
    {
        relativeDirectory = string.Empty;
        var normalizedDirectory = projectDirectory.Replace('\\', '/');
        if (!normalizedDirectory.EndsWith("/", StringComparison.Ordinal))
        {
            normalizedDirectory += "/";
        }

        var normalizedPath = filePath.Replace('\\', '/');
        if (!normalizedPath.StartsWith(normalizedDirectory, StringComparison.Ordinal))
        {
            return false;
        }

        var relative = normalizedPath[normalizedDirectory.Length..];
        var lastSlash = relative.LastIndexOf('/');
        relativeDirectory = lastSlash < 0 ? string.Empty : relative[..lastSlash];
        return true;
    }

    /// <summary>Builds the expected namespace from the root namespace and the relative folders.</summary>
    /// <param name="root">The root namespace (may be empty).</param>
    /// <param name="relativeDirectory">The folder path, forward-slash separated.</param>
    /// <returns>The expected namespace, or <see langword="null"/> when it cannot be formed.</returns>
    private static string? BuildExpectedNamespace(string root, string relativeDirectory)
    {
        var builder = new StringBuilder(root);
        if (relativeDirectory.Length > 0)
        {
            var segments = relativeDirectory.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0)
                {
                    continue;
                }

                if (!IsValidIdentifier(segments[i]))
                {
                    return null;
                }

                if (builder.Length > 0)
                {
                    builder.Append('.');
                }

                builder.Append(segments[i]);
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    /// <summary>Reads the root namespace, preferring an editorconfig override over the build property.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="global">The global analyzer config options.</param>
    /// <returns>The configured root namespace, or the empty string.</returns>
    private static string ReadRootNamespace(SyntaxNodeAnalysisContext context, AnalyzerConfigOptions global)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
        if (options.TryGetValue(RootNamespaceSpecificKey, out var specific) && specific.Length > 0)
        {
            return specific;
        }

        if (options.TryGetValue(RootNamespaceGeneralKey, out var general) && general.Length > 0)
        {
            return general;
        }

        if (global.TryGetValue(RootNamespaceBuildKey, out var build))
        {
            return build;
        }

        return string.Empty;
    }

    /// <summary>Returns whether a folder name is a valid C# identifier (and so usable as a namespace part).</summary>
    /// <param name="segment">The folder name.</param>
    /// <returns><see langword="true"/> when the segment is a valid identifier.</returns>
    private static bool IsValidIdentifier(string segment)
    {
        if (!char.IsLetter(segment[0]) && segment[0] != '_')
        {
            return false;
        }

        for (var i = 1; i < segment.Length; i++)
        {
            if (!char.IsLetterOrDigit(segment[i]) && segment[i] != '_')
            {
                return false;
            }
        }

        return true;
    }
}
