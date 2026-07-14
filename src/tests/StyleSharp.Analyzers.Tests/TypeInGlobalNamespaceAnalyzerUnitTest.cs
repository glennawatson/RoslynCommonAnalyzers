// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using VerifyGlobalNamespace = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2312TypeInGlobalNamespaceAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2312 (types should be declared in a named namespace).</summary>
public class TypeInGlobalNamespaceAnalyzerUnitTest
{
    /// <summary>Verifies every kind of type declared outside a namespace is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeOutsideAnyNamespaceIsReportedAsync()
    {
        const string Source = """
                              public class {|SST2312:Widget|};

                              public struct {|SST2312:Point|};

                              public interface {|SST2312:IWidget|};

                              public enum {|SST2312:Level|}
                              {
                                  Low,
                              }

                              public record {|SST2312:Session|}(int Id);

                              public record struct {|SST2312:Pair|}(int Left, int Right);

                              internal class {|SST2312:Helper|};

                              public static class {|SST2312:Helpers|}
                              {
                                  public class Nested;
                              }
                              """;

        // Records need IsExternalInit, which the default reference assemblies do not carry.
        var test = new VerifyGlobalNamespace.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies each part of a partial type is reported, because each is a declaration to move.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryPartialPartIsReportedAsync()
        => await VerifyGlobalNamespace.VerifyAnalyzerAsync(
            """
            public partial class {|SST2312:Widget|}
            {
                public int Id;
            }

            public partial class {|SST2312:Widget|}
            {
                public int Size;
            }
            """);

    /// <summary>Verifies a delegate is left alone, which keeps the firing set identical to the the rule it replaces.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GlobalDelegateIsCleanAsync()
        => await VerifyGlobalNamespace.VerifyAnalyzerAsync("public delegate void Handler(int value);");

    /// <summary>Verifies a type that already has a namespace is left alone, however that namespace is written.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Converting a block namespace to a file-scoped one is SST2237's business; this rule only asks that a
    /// namespace exist at all, so it must stay silent whichever form is used.
    /// </remarks>
    [Test]
    public async Task NamespacedTypeIsCleanAsync()
        => await VerifyGlobalNamespace.VerifyAnalyzerAsync(
            """
            namespace Vendor.Widgets
            {
                public class Widget
                {
                    public class Nested;
                }

                public delegate void Handler();
            }
            """);

    /// <summary>Verifies a file-scoped namespace is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileScopedNamespaceIsCleanAsync()
        => await VerifyGlobalNamespace.VerifyAnalyzerAsync(
            """
            namespace Vendor.Widgets;

            public class Widget;
            """);

    /// <summary>Verifies the <c>Program</c> that top-level statements generate is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The explicit <c>partial class Program</c> a project writes alongside its top-level statements — the shape
    /// integration tests use to reach the entry point — cannot be moved into a namespace: it would silently stop
    /// being the same type as the generated one rather than fail to compile. The rule leaves it where it is.
    /// </remarks>
    [Test]
    public async Task TopLevelStatementsProgramIsCleanAsync()
    {
        var test = new VerifyGlobalNamespace.Test
        {
            TestState =
            {
                OutputKind = OutputKind.ConsoleApplication,
                Sources =
                {
                    ("Main.cs", "System.Console.WriteLine(\"running\");"),
                    ("Program.cs", "public partial class Program;"),
                },
            },
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies generated code is not asked to move.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneratedCodeIsCleanAsync()
    {
        var test = new VerifyGlobalNamespace.Test
        {
            TestState = { Sources = { ("Widget.g.cs", "// <auto-generated/>\npublic class Widget;") } },
        };

        await test.RunAsync(CancellationToken.None);
    }
}
