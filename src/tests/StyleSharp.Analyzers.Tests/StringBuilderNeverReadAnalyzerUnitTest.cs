// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyStringBuilder = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2408StringBuilderNeverReadAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2408 (a StringBuilder that is filled and never read).</summary>
public class StringBuilderNeverReadAnalyzerUnitTest
{
    /// <summary>Verifies a builder that is appended to and never read is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnreadBuilderIsReportedAsync()
        => await VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public void M(string[] lines)
                {
                    var {|SST2408:builder|} = new StringBuilder();
                    foreach (var line in lines)
                    {
                        builder.AppendLine(line);
                    }
                }
            }
            """);

    /// <summary>Verifies a chain of discarded appends is still no read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ChainedAppendsAreStillUnreadAsync()
        => await VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public void M(string name)
                {
                    StringBuilder {|SST2408:builder|} = new StringBuilder();
                    builder.Append("name: ").Append(name).AppendLine();
                    builder.Clear();
                }
            }
            """);

    /// <summary>Verifies a builder whose contents are collected is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BuilderThatIsReadIsCleanAsync()
        => await VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public string M(string[] lines)
                {
                    var builder = new StringBuilder();
                    foreach (var line in lines)
                    {
                        builder.AppendLine(line);
                    }

                    return builder.ToString();
                }
            }
            """);

    /// <summary>Verifies handing the builder to something else counts as a read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BuilderPassedOnIsCleanAsync()
        => await VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public void M(string name)
                {
                    var builder = new StringBuilder();
                    builder.Append(name);
                    Write(builder);
                }

                private static void Write(StringBuilder builder)
                {
                }
            }
            """);

    /// <summary>Verifies reading a property of the builder counts as a read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BuilderLengthReadIsCleanAsync()
        => await VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System;
            using System.Text;

            public sealed class C
            {
                public void M(string name)
                {
                    var builder = new StringBuilder();
                    builder.Append(name);
                    Console.WriteLine(builder.Length);
                }
            }
            """);

    /// <summary>Verifies a builder that is returned is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnedBuilderIsCleanAsync()
        => await VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public StringBuilder M(string name)
                {
                    var builder = new StringBuilder();
                    builder.Append(name);
                    return builder;
                }
            }
            """);

    /// <summary>Verifies a builder that is never appended to is not this rule's business.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BuilderWithNoAppendIsCleanAsync()
        => await VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public void M()
                {
                    var builder = new StringBuilder();
                    builder.Clear();
                }
            }
            """);

    /// <summary>Verifies a field is left alone: any other member of the type may read it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BuilderFieldIsCleanAsync()
        => await VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                private readonly StringBuilder _builder = new();

                public void Add(string line) => _builder.AppendLine(line);
            }
            """);

    /// <summary>Verifies a type of the project's own with the same name is not measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedProjectTypeIsCleanAsync()
        => await VerifyStringBuilder.VerifyAnalyzerAsync(
            """
            namespace Custom
            {
                public sealed class StringBuilder
                {
                    public void Append(string value)
                    {
                    }
                }

                public sealed class C
                {
                    public void M(string name)
                    {
                        var builder = new StringBuilder();
                        builder.Append(name);
                    }
                }
            }
            """);
}
