// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1411SealNonDerivedTypeAnalyzer,
    PerformanceSharp.Analyzers.Psh1411SealNonDerivedTypeCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1411SealNonDerivedTypeAnalyzer"/> (PSH1411 seal non-derived types).</summary>
public class SealNonDerivedTypeAnalyzerUnitTest
{
    /// <summary>The editorconfig line that turns on reporting of externally visible classes.</summary>
    private const string IncludePublicSetting = "performancesharp.PSH1411.include_public = true";

    /// <summary>Verifies an internal class nothing derives from is reported and sealed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalNonDerivedClassIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class {|PSH1411:Widget|}
                              {
                              }
                              """;
        const string FixedSource = """
                                   internal sealed class Widget
                                   {
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a class with no accessibility modifier gains sealed as its first modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModifierlessClassIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              class {|PSH1411:Widget|}
                              {
                              }
                              """;
        const string FixedSource = """
                                   sealed class Widget
                                   {
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a private nested class is reported and sealed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateNestedClassIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public sealed class Outer
                              {
                                  private class {|PSH1411:Hidden|}
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class Outer
                                   {
                                       private sealed class Hidden
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a file-local class is reported and sealed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileLocalClassIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              file class {|PSH1411:Widget|}
                              {
                              }
                              """;
        const string FixedSource = """
                                   file sealed class Widget
                                   {
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a base class is left alone while the leaf that derives from it is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DerivedFromClassIsCleanAndItsLeafIsFlaggedAsync()
    {
        const string Source = """
                              internal class Widget
                              {
                              }

                              internal class {|PSH1411:Gadget|} : Widget
                              {
                              }
                              """;
        const string FixedSource = """
                                   internal class Widget
                                   {
                                   }

                                   internal sealed class Gadget : Widget
                                   {
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a class derived from through a constructed generic base is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericBaseIsCleanAsync()
        => await VerifyAsync(
            """
            internal class Widget<T>
            {
            }

            internal sealed class Gadget : Widget<int>
            {
            }
            """);

    /// <summary>Verifies a class named by a generic constraint is not reported — a sealed class is not a valid constraint.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstraintTargetIsCleanAsync()
        => await VerifyAsync(
            """
            internal class Widget
            {
            }

            internal static class Helper
            {
                public static void Run<T>(T value)
                    where T : Widget
                {
                }
            }
            """);

    /// <summary>Verifies a class constrained by a local function's type parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFunctionConstraintTargetIsCleanAsync()
        => await VerifyAsync(
            """
            internal class Widget
            {
            }

            internal sealed class Runner
            {
                public void M()
                {
                    Run(new Widget());

                    static void Run<T>(T value)
                        where T : Widget
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies an internal class is not reported when the assembly exposes its internals to a friend.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalClassWithInternalsVisibleToIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Runtime.CompilerServices;

            [assembly: InternalsVisibleTo("Friend")]

            internal class Widget
            {
            }
            """);

    /// <summary>Verifies a private nested class is still reported when an InternalsVisibleTo is present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A friend assembly gains internal access, never private access, so a private class stays unreachable.</remarks>
    [Test]
    public async Task PrivateNestedClassWithInternalsVisibleToIsFlaggedAsync()
    {
        const string Source = """
                              using System.Runtime.CompilerServices;

                              [assembly: InternalsVisibleTo("Friend")]

                              public sealed class Outer
                              {
                                  private class {|PSH1411:Hidden|}
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Runtime.CompilerServices;

                                   [assembly: InternalsVisibleTo("Friend")]

                                   public sealed class Outer
                                   {
                                       private sealed class Hidden
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an already sealed class is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SealedClassIsCleanAsync()
        => await VerifyAsync(
            """
            internal sealed class Widget
            {
            }
            """);

    /// <summary>Verifies an abstract class is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractClassIsCleanAsync()
        => await VerifyAsync(
            """
            internal abstract class Widget
            {
            }
            """);

    /// <summary>Verifies a static class is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticClassIsCleanAsync()
        => await VerifyAsync(
            """
            internal static class Widget
            {
            }
            """);

    /// <summary>Verifies a record is not reported — SST1800 owns record sealing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordIsCleanAsync()
        => await VerifyAsync(
            """
            internal record Widget(int Value);
            """);

    /// <summary>Verifies a struct and an interface are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonClassTypesAreCleanAsync()
        => await VerifyAsync(
            """
            internal struct Point
            {
            }

            internal interface IWidget
            {
            }
            """);

    /// <summary>Verifies a class declaring a virtual member is not reported — a sealed class may not have one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassWithVirtualMemberIsCleanAsync()
        => await VerifyAsync(
            """
            internal class Widget
            {
                public virtual void Reset()
                {
                }
            }
            """);

    /// <summary>Verifies a class declaring a new protected member is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassWithProtectedMemberIsCleanAsync()
        => await VerifyAsync(
            """
            internal class Widget
            {
                protected void Reset()
                {
                }
            }
            """);

    /// <summary>Verifies a public class is not reported by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicClassIsCleanByDefaultAsync()
        => await VerifyAsync(
            """
            public class Widget
            {
            }
            """);

    /// <summary>Verifies a public class nested in an internal one is reported — it is not reachable from outside.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicClassInsideInternalClassIsFlaggedAsync()
    {
        const string Source = """
                              internal sealed class Outer
                              {
                                  public class {|PSH1411:Inner|}
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal sealed class Outer
                                   {
                                       public sealed class Inner
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a public class is reported once include_public is turned on.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicClassIsFlaggedWhenIncludePublicIsSetAsync()
        => await VerifyWithConfigAsync(
            """
            public class {|PSH1411:Widget|}
            {
            }
            """,
            IncludePublicSetting);

    /// <summary>Verifies a derived public class is still not reported with include_public on.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DerivedPublicClassIsCleanWithIncludePublicAsync()
        => await VerifyWithConfigAsync(
            """
            public class Widget
            {
            }

            public sealed class Gadget : Widget
            {
            }
            """,
            IncludePublicSetting);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer verification with one editorconfig setting applied.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="setting">The editorconfig line to apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithConfigAsync(string source, string setting)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", $"""
            root = true
            [*.cs]
            {setting}

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
