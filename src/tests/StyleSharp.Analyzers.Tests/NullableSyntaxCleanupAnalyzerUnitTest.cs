// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyNullableSyntaxCleanup = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.NullableSyntaxCleanupAnalyzer,
    StyleSharp.Analyzers.NullableSyntaxCleanupCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for nullable syntax cleanup rules (SST2209-SST2211).</summary>
public class NullableSyntaxCleanupAnalyzerUnitTest
{
    /// <summary>Verifies a null-forgiving operator on a non-null value is removed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnneededNullForgivingOperatorIsFixedAsync()
    {
        const string Source = """
                              #nullable enable

                              public sealed class C
                              {
                                  public int M()
                                  {
                                      var value = 1;
                                      return value{|SST2209:!|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable

                                   public sealed class C
                                   {
                                       public int M()
                                       {
                                           var value = 1;
                                           return value;
                                       }
                                   }
                                   """;
        var test = new VerifyNullableSyntaxCleanup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a repeated nullable directive is removed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedNullableDirectiveIsFixedAsync()
    {
        const string Source = """
                              #nullable enable
                              {|SST2210:#nullable enable|}

                              public sealed class C
                              {
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable

                                   public sealed class C
                                   {
                                   }
                                   """;
        var test = new VerifyNullableSyntaxCleanup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an initial nullable restore directive is removed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InitialNullableRestoreIsFixedAsync()
    {
        const string Source = """
                              {|SST2211:#nullable restore|}

                              public sealed class C
                              {
                              }
                              """;
        const string FixedSource = """

                                   public sealed class C
                                   {
                                   }
                                   """;
        var test = new VerifyNullableSyntaxCleanup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies necessary nullable syntax stays clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NecessaryNullableSyntaxIsCleanAsync()
    {
        const string Source = """
                              #nullable enable

                              public sealed class C
                              {
                                  public string M(string? value) => value!;
                              }
                              """;
        var test = new VerifyNullableSyntaxCleanup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the suppression on a call that may return null is kept.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullForgivingOnMaybeNullCallIsCleanAsync()
    {
        const string Source = """
                              #nullable enable

                              using System;

                              public sealed class C
                              {
                                  public object Make(Type type) => Activator.CreateInstance(type)!;

                                  public string Name(Type type) => type.FullName!;
                              }
                              """;
        var test = new VerifyNullableSyntaxCleanup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the suppression on a local that flow analysis knows may be null is kept.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The local is declared non-nullable, so its declaration says nothing; only the assignment makes the
    /// suppression load-bearing, and removing it here would leave the file with a nullable warning.
    /// </remarks>
    [Test]
    public async Task NullForgivingOnFlowMaybeNullLocalIsCleanAsync()
    {
        const string Source = """
                              #nullable enable

                              public sealed class C
                              {
                                  public int M(string? source)
                                  {
                                      string value = source!;
                                      return Length(value!);
                                  }

                                  private static int Length(string text) => text.Length;
                              }
                              """;
        var test = new VerifyNullableSyntaxCleanup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a suppression on a freshly created object is removed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullForgivingOnObjectCreationIsFixedAsync()
    {
        const string Source = """
                              #nullable enable

                              public sealed class C
                              {
                                  public object M() => new object(){|SST2209:!|};
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable

                                   public sealed class C
                                   {
                                       public object M() => new object();
                                   }
                                   """;
        var test = new VerifyNullableSyntaxCleanup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a suppression on a string literal is removed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullForgivingOnStringLiteralIsFixedAsync()
    {
        const string Source = """
                              #nullable enable

                              public sealed class C
                              {
                                  public string M() => "text"{|SST2209:!|};
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable

                                   public sealed class C
                                   {
                                       public string M() => "text";
                                   }
                                   """;
        var test = new VerifyNullableSyntaxCleanup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a suppression on the enclosing instance is removed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullForgivingOnThisIsFixedAsync()
    {
        const string Source = """
                              #nullable enable

                              public sealed class C
                              {
                                  public object M() => this{|SST2209:!|};
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable

                                   public sealed class C
                                   {
                                       public object M() => this;
                                   }
                                   """;
        var test = new VerifyNullableSyntaxCleanup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies null-forgiving syntax stays when null is assigned into a non-nullable array slot.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullForgivingNullLiteralAssignedToArraySlotIsCleanAsync()
    {
        const string Source = """
                              #nullable enable

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      string[] values = new string[1];
                                      values[0] = null!;
                                  }
                              }
                              """;
        var test = new VerifyNullableSyntaxCleanup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
