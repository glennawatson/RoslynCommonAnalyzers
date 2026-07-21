// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyExplicitObjectCreationType = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2254ExplicitObjectCreationTypeAnalyzer,
    StyleSharp.Analyzers.Sst2254ExplicitObjectCreationTypeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for SST2254 (name the created type instead of a target-typed <c>new</c>). The rule is
/// disabled by default, so every test enables it through an <c>.editorconfig</c> severity entry.
/// </summary>
public class ExplicitObjectCreationTypeAnalyzerUnitTest
{
    /// <summary>Verifies a typed local declaration names the created type at the creation site.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypedLocalDeclarationIsNamedExplicitlyAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M()
                                  {
                                      C value = {|SST2254:new|}();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M()
                                       {
                                           C value = new C();
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an explicitly-typed field initializer names the created type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldInitializerIsNamedExplicitlyAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly C _value = {|SST2254:new|}();
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private readonly C _value = new C();
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an explicitly-typed property initializer names the created type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyInitializerIsNamedExplicitlyAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public C Value { get; } = {|SST2254:new|}();
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public C Value { get; } = new C();
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a return in a method with an explicit return type names the created type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnIsNamedExplicitlyAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public C Make()
                                  {
                                      return {|SST2254:new|}();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public C Make()
                                       {
                                           return new C();
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an argument bound to a typed parameter names the created type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentIsNamedExplicitlyAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void Accept(C value)
                                  {
                                  }

                                  public void M() => Accept({|SST2254:new|}());
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void Accept(C value)
                                       {
                                       }

                                       public void M() => Accept(new C());
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies constructor arguments and an object initializer are kept when the type is named.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentsAndInitializerArePreservedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public C(int count)
                                  {
                                  }

                                  public int Size { get; set; }

                                  public C Make() => {|SST2254:new|}(4) { Size = 2 };
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public C(int count)
                                       {
                                       }

                                       public int Size { get; set; }

                                       public C Make() => new C(4) { Size = 2 };
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a generic created type is named with its minimally-qualified spelling.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericCreatedTypeIsNamedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public List<int> Make() => {|SST2254:new|}() { 1, 2 };
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public List<int> Make() => new List<int>() { 1, 2 };
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a nullable value target names the underlying value type it actually constructs.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableValueTargetNamesUnderlyingTypeAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int? Value { get; } = {|SST2254:new|}();
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int? Value { get; } = new int();
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies leading trivia around the creation survives the rewrite.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SurroundingTriviaIsPreservedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public C Make() =>
                                      // build a fresh one
                                      {|SST2254:new|}();
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public C Make() =>
                                           // build a fresh one
                                           new C();
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a tuple target is left alone because a tuple has no plain type-name spelling here.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TupleTargetIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public (int, int) Make() => new();
                              }
                              """;
        var test = CreateTest(Source);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unresolved target type is left alone rather than reported with an unusable fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ErrorTargetTypeIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly Missing _value = new();
                              }
                              """;
        var test = CreateTest(Source);
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification with the disabled rule enabled.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string fixedSource)
    {
        var test = CreateTest(source);
        test.FixedCode = fixedSource;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a .NET 8 verifier test with SST2254 enabled.</summary>
    /// <param name="source">The markup source.</param>
    /// <returns>The configured test.</returns>
    private static VerifyExplicitObjectCreationType.Test CreateTest(string source)
    {
        var test = new VerifyExplicitObjectCreationType.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        Enable(test);
        return test;
    }

    /// <summary>Enables the disabled-by-default SST2254 diagnostic for a verifier test.</summary>
    /// <param name="test">The verifier test.</param>
    private static void Enable(VerifyExplicitObjectCreationType.Test test)
    {
        const string Config = """
                              root = true

                              [*.cs]
                              dotnet_diagnostic.SST2254.severity = warning
                              """;
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
    }
}
