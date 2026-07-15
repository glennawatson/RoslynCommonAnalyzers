// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeRefactoringVerifier<StyleSharp.Analyzers.Sst2444GeneratedRegexRefactoringProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for the SST2444 source-generated regular-expression refactoring. The generated partial method
/// is completed by the framework's own source generator at build time; the test host does not run that
/// generator, so the converted declaration is expected to carry the "needs an implementation part" compiler
/// diagnostic that the generator would otherwise satisfy.
/// </summary>
public class GeneratedRegexRefactoringUnitTest
{
    /// <summary>Verifies a valid single-literal construction is converted to a generated regular expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidConstructionIsConvertedAsync()
    {
        const string Source = """
                              using System.Text.RegularExpressions;

                              public class C
                              {
                                  public Regex Build() => [|new Regex("[a-z]+")|];
                              }
                              """;
        const string FixedSource = """
                                   using System.Text.RegularExpressions;

                                   public partial class C
                                   {
                                       public Regex Build() => PatternRegex();

                                       [GeneratedRegex("[a-z]+")]
                                       private static partial Regex {|CS8795:PatternRegex|}();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an already-partial type keeps its single partial modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlreadyPartialTypeKeepsOneModifierAsync()
    {
        const string Source = """
                              using System.Text.RegularExpressions;

                              public partial class C
                              {
                                  public Regex Build() => [|new Regex("[a-z]+")|];
                              }
                              """;
        const string FixedSource = """
                                   using System.Text.RegularExpressions;

                                   public partial class C
                                   {
                                       public Regex Build() => PatternRegex();

                                       [GeneratedRegex("[a-z]+")]
                                       private static partial Regex {|CS8795:PatternRegex|}();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an invalid pattern is not offered the refactoring.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidPatternIsNotOfferedAsync()
        => await VerifyNoRefactoringAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => [|new Regex("[a-z")|];
            }
            """);

    /// <summary>Verifies a construction with an options argument is not offered the refactoring.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructionWithOptionsIsNotOfferedAsync()
        => await VerifyNoRefactoringAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => [|new Regex("[a-z]+", RegexOptions.Compiled)|];
            }
            """);

    /// <summary>Verifies a generic host type is not offered the refactoring.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericHostTypeIsNotOfferedAsync()
        => await VerifyNoRefactoringAsync(
            """
            using System.Text.RegularExpressions;

            public class C<T>
            {
                public Regex Build() => [|new Regex("[a-z]+")|];
            }
            """);

    /// <summary>Verifies a nested host type is not offered the refactoring.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedHostTypeIsNotOfferedAsync()
        => await VerifyNoRefactoringAsync(
            """
            using System.Text.RegularExpressions;

            public class Outer
            {
                public class C
                {
                    public Regex Build() => [|new Regex("[a-z]+")|];
                }
            }
            """);

    /// <summary>Runs a refactoring verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with the selection span.</param>
    /// <param name="fixedSource">The expected source after the refactoring.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that no refactoring is offered at the selection.</summary>
    /// <param name="source">The source with the selection span.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNoRefactoringAsync(string source)
        => await VerifyAsync(
            source,
            source.Replace("[|", string.Empty, StringComparison.Ordinal).Replace("|]", string.Empty, StringComparison.Ordinal));
}
