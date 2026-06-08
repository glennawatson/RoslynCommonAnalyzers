// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Provides shared helpers used when configuring C# analyzer and code fix tests.
/// </summary>
internal static class CSharpVerifierHelper
{
    /// <summary>
    /// Gets the nullable warnings.
    /// By default, the compiler reports diagnostics for nullable reference types at
    /// <see cref="DiagnosticSeverity.Warning"/>, and the analyzer test framework defaults to only validating
    /// diagnostics at <see cref="DiagnosticSeverity.Error"/>. This map contains all compiler diagnostic IDs
    /// related to nullability mapped to <see cref="ReportDiagnostic.Error"/>, which is then used to enable all
    /// of these warnings for default validation during analyzer and code fix tests.
    /// </summary>
    internal static ImmutableDictionary<string, ReportDiagnostic> NullableWarnings { get; } = GetNullableWarningsFromCompiler();

    /// <summary>
    /// Configures a test solution: promotes nullable warnings to errors and pins the formatter's newline to
    /// <c>\n</c>. The latter matters because the harness reformats the fixed document through
    /// <see cref="Formatter"/>, which rewrites the elastic end-of-line trivia our code fixes emit to the
    /// workspace newline — left at its platform default that would be CRLF on Windows and LF elsewhere, making
    /// fix output checkout-dependent. Pinning it keeps applied fixes byte-identical on every platform.
    /// </summary>
    /// <param name="solution">The solution under construction.</param>
    /// <param name="projectId">The identifier of the project under test.</param>
    /// <returns>The configured solution.</returns>
    internal static Solution ConfigureSolution(Solution solution, ProjectId projectId)
    {
        var compilationOptions = solution.GetProject(projectId)!.CompilationOptions!;
        compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
            compilationOptions.SpecificDiagnosticOptions.SetItems(NullableWarnings));
        solution = solution.WithProjectCompilationOptions(projectId, compilationOptions);

        return solution.WithOptions(solution.Options.WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, "\n"));
    }

    /// <summary>
    /// Normalizes every CRLF and lone CR in <paramref name="value"/> to a single <c>\n</c>, so a snippet behaves
    /// the same whichever line endings git checked the test file out with. Allocation-free fast path: when the
    /// string has no carriage return it is returned unchanged.
    /// </summary>
    /// <param name="value">The text to normalize.</param>
    /// <returns>The text with <c>\n</c> line endings, or the original instance when already normalized.</returns>
    internal static string NormalizeLineEndings(string value)
    {
        if (value.IndexOf('\r') < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        var index = 0;
        while (index < value.Length)
        {
            var current = value[index];
            if (current != '\r')
            {
                builder.Append(current);
                index++;
                continue;
            }

            builder.Append('\n');

            // Treat CRLF as one break by consuming the LF that follows a CR.
            index += index + 1 < value.Length && value[index + 1] == '\n' ? 2 : 1;
        }

        return builder.ToString();
    }

    /// <summary>Rewrites each source file in <paramref name="sources"/> to use <c>\n</c> line endings in place.</summary>
    /// <param name="sources">The source files to normalize; left untouched when already normalized.</param>
    internal static void NormalizeLineEndings(SourceFileCollection sources)
    {
        for (var i = 0; i < sources.Count; i++)
        {
            var (filename, content) = sources[i];
            var text = content.ToString();
            var normalized = NormalizeLineEndings(text);
            if (!ReferenceEquals(text, normalized))
            {
                sources[i] = (filename, SourceText.From(normalized, content.Encoding, content.ChecksumAlgorithm));
            }
        }
    }

    /// <summary>
    /// Builds the map of nullable-related compiler diagnostic identifiers promoted to <see cref="ReportDiagnostic.Error"/>.
    /// </summary>
    /// <returns>A dictionary mapping nullable diagnostic identifiers to <see cref="ReportDiagnostic.Error"/>.</returns>
    private static ImmutableDictionary<string, ReportDiagnostic> GetNullableWarningsFromCompiler()
    {
        string[] args = ["/warnaserror:nullable"];
        var commandLineArguments = CSharpCommandLineParser.Default.Parse(args, baseDirectory: Environment.CurrentDirectory, sdkDirectory: Environment.CurrentDirectory);
        var nullableWarnings = commandLineArguments.CompilationOptions.SpecificDiagnosticOptions;

        // Workaround for https://github.com/dotnet/roslyn/issues/41610
        return nullableWarnings
            .SetItem("CS8632", ReportDiagnostic.Error)
            .SetItem("CS8669", ReportDiagnostic.Error);
    }
}
