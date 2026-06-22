// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyModernSyntaxReadability = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ModernSyntaxReadabilityAnalyzer,
    StyleSharp.Analyzers.ModernSyntaxReadabilityCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for modern readability rules that mirror selected IDE language rules (SST2212-SST2217).</summary>
public class ModernSyntaxReadabilityAnalyzerUnitTest
{
    /// <summary>Verifies UTF-8 encoding calls over literals are replaced with UTF-8 string literals.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Utf8EncodingLiteralCandidatesAreFixedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public ReadOnlySpan<byte> Header() => {|SST2212:System.Text.Encoding.UTF8.GetBytes("GET")|};

                                  public byte[] Copy() => {|SST2212:System.Text.Encoding.UTF8.GetBytes("POST")|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public ReadOnlySpan<byte> Header() => "GET"u8;

                                       public byte[] Copy() => "POST"u8.ToArray();
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies byte array literals are not treated as explicit UTF-8 text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ByteArrayTextIsNotUtf8LiteralCandidateAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public byte[] Header() => new byte[] { 71, 69, 84 };
                              }
                              """;
        var test = CreateNet80Test(Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies arbitrary control-byte arrays are not treated as readable UTF-8 text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ControlByteArrayIsNotUtf8TextAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public byte[] Data() => new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                              }
                              """;
        var test = CreateNet80Test(Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a discard designation on a type pattern is removed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnnecessaryDiscardPatternIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(object value) => value is {|SST2213:int _|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(object value) => value is int;
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies tuple element locals can be declared directly with deconstruction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TupleElementLocalDeclarationsAreFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M()
                                  {
                                      {|SST2214:var pair = GetPair();|}
                                      var name = pair.Name;
                                      var age = pair.Age;
                                      return name + age;
                                  }

                                  private static (string Name, int Age) GetPair() => ("Ada", 36);
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M()
                                       {
                                           var (name, age) = GetPair();
                                           return name + age;
                                       }

                                       private static (string Name, int Age) GetPair() => ("Ada", 36);
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a three-statement local swap is rewritten as tuple assignment.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TemporaryLocalSwapIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M()
                                  {
                                      var left = 1;
                                      var right = 2;
                                      {|SST2215:var temp = left;|}
                                      left = right;
                                      right = temp;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M()
                                       {
                                           var left = 1;
                                           var right = 2;
                                           (left, right) = (right, left);
                                       }
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies repeated tuple element names are omitted when the compiler can infer them.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InferredTupleElementNamesAreFixedAsync()
    {
        const string Source = """
                              public sealed class Person
                              {
                                  public int Age { get; set; }
                              }

                              public sealed class C
                              {
                                  public (string name, int Age) M(Person person, string name) => ({|SST2216:name: name|}, {|SST2216:Age: person.Age|});
                              }
                              """;
        const string FixedSource = """
                                   public sealed class Person
                                   {
                                       public int Age { get; set; }
                                   }

                                   public sealed class C
                                   {
                                       public (string name, int Age) M(Person person, string name) => (name, person.Age);
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a simple multiplier hash expression is replaced with <see cref="HashCode.Combine{T1, T2}(T1, T2)"/>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HashCodeCombineCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class Person
                              {
                                  public int Id { get; set; }
                                  public int Age { get; set; }

                                  public override int GetHashCode() => {|SST2217:(Id.GetHashCode() * 397) ^ Age.GetHashCode()|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class Person
                                   {
                                       public int Id { get; set; }
                                       public int Age { get; set; }

                                       public override int GetHashCode() => System.HashCode.Combine(Id, Age);
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies guarded semantic and version-specific shapes stay clean.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RiskyShapesAreCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class Person
                              {
                                  public int Age { get; set; }
                              }

                              public sealed class C
                              {
                                  private int _left;
                                  private int _right;

                                  public byte[] RuntimeEncoding(string value) => System.Text.Encoding.UTF8.GetBytes(value);

                                  public bool VarDiscard(object value) => value is var _;

                                  public string TupleUsedLater()
                                  {
                                      var pair = ("Ada", 36);
                                      var name = pair.Item1;
                                      var age = pair.Item2;
                                      return pair.Item1 + name + age;
                                  }

                                  public void PropertySwap()
                                  {
                                      var temp = _left;
                                      _left = _right;
                                      _right = temp;
                                  }

                                  public (int Years, int other) ExplicitTupleName(Person person) => (Years: person.Age, other: 1);

                                  public int NotHashCode() => (_left.GetHashCode() * 397) ^ _right.GetHashCode();
                              }

                              public sealed class ReferenceHash
                              {
                                  public string Name { get; set; } = "";

                                  public int Age { get; set; }

                                  public override int GetHashCode() => (Name.GetHashCode() * 397) ^ Age.GetHashCode();
                              }
                              """;
        var test = CreateNet80Test(Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies hash-code suggestions are not reported when <see cref="HashCode"/> is unavailable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HashCodeCombineRequiresRuntimeTypeAsync()
    {
        const string Source = """
                              public sealed class Person
                              {
                                  public int Id { get; set; }
                                  public int Age { get; set; }

                                  public override int GetHashCode() => (Id.GetHashCode() * 397) ^ Age.GetHashCode();
                              }
                              """;
        var test = new VerifyModernSyntaxReadability.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source
        };
        AddModernParseOptions(test);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a modern C# verification test using the .NET 8 reference pack.</summary>
    /// <param name="source">The source to analyze.</param>
    /// <param name="fixedSource">The optional fixed source.</param>
    /// <returns>The configured test.</returns>
    private static VerifyModernSyntaxReadability.Test CreateNet80Test(string source, string? fixedSource = null)
    {
        var test = new VerifyModernSyntaxReadability.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source
        };

        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        AddModernParseOptions(test);
        return test;
    }

    /// <summary>Ensures feature-gated syntax rules run against a modern C# parse option.</summary>
    /// <param name="test">The test to configure.</param>
    private static void AddModernParseOptions(VerifyModernSyntaxReadability.Test test)
        => test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var projectParseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, projectParseOptions.WithLanguageVersion(LanguageVersion.CSharp12));
        });
}
