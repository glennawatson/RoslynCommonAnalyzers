// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyModernSyntaxStyle = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ModernSyntaxStyleAnalyzer,
    StyleSharp.Analyzers.ModernSyntaxStyleCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for conservative modern syntax style analysis (SST2202-SST2204).</summary>
public class ModernSyntaxStyleAnalyzerUnitTest
{
    /// <summary>Verifies repeated object creation types are removed when the target type is explicit.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TargetTypedNewCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class Person
                              {
                              }

                              public sealed class C
                              {
                                  public Person M()
                                  {
                                      Person person = new {|SST2202:Person|}();
                                      return person;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class Person
                                   {
                                   }

                                   public sealed class C
                                   {
                                       public Person M()
                                       {
                                           Person person = new();
                                           return person;
                                       }
                                   }
                                   """;
        var test = new VerifyModernSyntaxStyle.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies length subtraction on an array can use a from-end index.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IndexFromEndCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int[] values) => values[{|SST2203:values.Length - 1|}];
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int[] values) => values[^1];
                                   }
                                   """;
        var test = new VerifyModernSyntaxStyle.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies string substring calls can use a range expression.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SubstringCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string Tail(string text, int start) => {|SST2204:text.Substring|}(start);

                                  public string Slice(string text, int start, int length) => {|SST2204:text.Substring|}(start, length);
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string Tail(string text, int start) => text[start..];

                                       public string Slice(string text, int start, int length) => text[start..(start + length)];
                                   }
                                   """;
        var test = new VerifyModernSyntaxStyle.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies ambiguous target types and non-local receivers stay clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonCandidatesAreCleanAsync()
    {
        const string Source = """
                              public sealed class Person
                              {
                              }

                              public sealed class C
                              {
                                  private readonly int[] _values = [1, 2, 3];

                                  public Person Create()
                                  {
                                      var person = new Person();
                                      return person;
                                  }

                                  public int Last() => _values[_values.Length - 1];

                                  public string Slice(string text, int start) => text.Substring(start + 1);
                              }
                              """;
        var test = new VerifyModernSyntaxStyle.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies generated files are not analyzed even when diagnostic reporting is optimized.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GeneratedFilesStayCleanAsync()
    {
        const string Source = """
                              public sealed class Person
                              {
                              }

                              public sealed class C
                              {
                                  public Person M()
                                  {
                                      Person person = new Person();
                                      return person;
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxStyle.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };
        test.TestState.Sources.Add(("ModernSyntaxStyleBench.g.cs", Source));

        await test.RunAsync(CancellationToken.None);
    }
}
