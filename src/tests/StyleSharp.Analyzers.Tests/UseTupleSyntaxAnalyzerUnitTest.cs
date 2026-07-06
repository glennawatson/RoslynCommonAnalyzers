// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyTuple = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1141UseTupleSyntaxAnalyzer,
    StyleSharp.Analyzers.Sst1141UseTupleSyntaxCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1141 (use tuple syntax instead of ValueTuple&lt;...&gt;) and its fix.</summary>
public class UseTupleSyntaxAnalyzerUnitTest
{
    /// <summary>Verifies an explicit ValueTuple type is reported (SST1141) and rewritten to tuple syntax.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueTupleTypeRewrittenAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public {|SST1141:ValueTuple<int, string>|} M() => default;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public (int, string) M() => default;
                                   }
                                   """;
        var test = new VerifyTuple.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = FixedSource };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies Fix All rewrites every explicit ValueTuple type in a single document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public {|SST1141:ValueTuple<int, string>|} A() => default;

                                  public {|SST1141:ValueTuple<string, int>|} B() => default;

                                  public {|SST1141:ValueTuple<int, int>|} D() => default;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public (int, string) A() => default;

                                       public (string, int) B() => default;

                                       public (int, int) D() => default;
                                   }
                                   """;
        var test = new VerifyTuple.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = FixedSource };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies Fix All composes nested explicit ValueTuple type rewrites in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesNestedValueTupleTypesAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public {|SST1141:ValueTuple<{|SST1141:ValueTuple<int, string>|}, {|SST1141:ValueTuple<bool, bool>|}>|} M() => default;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public ((int, string), (bool, bool)) M() => default;
                                   }
                                   """;
        const string BatchFixedSource = """
                                        using System;

                                        public class C
                                        {
                                            public ((int, string), (bool, bool)) M() => default;
                                        }
                                        """;
        var test = new VerifyTuple.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource,
            BatchFixedCode = BatchFixedSource
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies tuple syntax and a single-argument ValueTuple are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TupleSyntaxAndSingleArgAreCleanAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public (int, string) M() => default;

                                  public ValueTuple<int> Single() => default;
                              }
                              """;
        var test = new VerifyTuple.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = Source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule stays silent below C# 7, where the tuple type syntax the fix emits does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp7Async()
    {
        // 'ValueTuple<int, string>' is ordinary generic usage that compiles at C# 6; only the fix's
        // '(int, string)' form needs C# 7, so the analyzer must not report it under the C# 6 pin.
        const string Source = """
                              using System;

                              public class C
                              {
                                  public ValueTuple<int, string> M() => default(ValueTuple<int, string>);
                              }
                              """;
        var test = new VerifyTuple.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp6));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
