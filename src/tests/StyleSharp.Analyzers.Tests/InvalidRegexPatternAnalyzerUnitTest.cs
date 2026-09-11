// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2444InvalidRegexPatternAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2444 (an invalid regular-expression pattern).</summary>
public class InvalidRegexPatternAnalyzerUnitTest
{
    /// <summary>Verifies an unterminated character class in a construction is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnterminatedSetInConstructionIsFlaggedAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex({|SST2444:"[a-z"|});
            }
            """);

    /// <summary>Verifies an unclosed group in a static query is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnclosedGroupInStaticQueryIsFlaggedAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool Match(string s) => Regex.IsMatch(s, {|SST2444:"(unclosed"|});
            }
            """);

    /// <summary>Verifies a reversed quantifier is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReversedQuantifierIsFlaggedAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex({|SST2444:"a{2,1}"|});
            }
            """);

    /// <summary>Verifies a backreference to a group that is never defined is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UndefinedBackreferenceIsFlaggedAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public string Clean(string s) => Regex.Replace(s, {|SST2444:@"\1x"|}, "y");
            }
            """);

    /// <summary>Verifies the non-backtracking option is kept, so a backreference under it is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BackreferenceUnderNonBacktrackingIsFlaggedAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex({|SST2444:@"(a)\1"|}, RegexOptions.NonBacktracking);
            }
            """);

    /// <summary>Verifies a valid pattern is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ValidPatternIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex("[a-z]+");
            }
            """);

    /// <summary>Verifies a valid backreference in the default engine is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ValidBackreferenceIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex(@"(a)\1");
            }
            """);

    /// <summary>Verifies the compile option is stripped, so a valid compiled pattern is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CompiledOptionIsStrippedAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex("[a-z]+", RegexOptions.Compiled);
            }
            """);

    /// <summary>Verifies whitespace ignored under the pattern-whitespace option keeps a spaced pattern valid.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IgnorePatternWhitespaceKeepsSpacedPatternValidAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => new Regex("a b c", RegexOptions.IgnorePatternWhitespace);
            }
            """);

    /// <summary>Verifies a non-constant pattern is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonConstantPatternIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build(string pattern) => new Regex(pattern);
            }
            """);

    /// <summary>Verifies a same-named method on another type is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameNamedMethodOnOtherTypeIsCleanAsync() =>
        VerifyAsync(
            """
            public static class Helper
            {
                public static bool IsMatch(string s, string pattern) => false;
            }

            public class C
            {
                public bool Match(string s) => Helper.IsMatch(s, "(unclosed");
            }
            """);

    /// <summary>Verifies an instance query with no pattern argument is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InstanceQueryWithoutPatternIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public bool Match(Regex regex, string s) => regex.IsMatch(s);
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with any diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }
}
