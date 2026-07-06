// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyTupleName = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1142TupleElementNameAnalyzer,
    StyleSharp.Analyzers.Sst1142TupleElementNameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1142 (refer to tuple elements by name) and its fix.</summary>
public class TupleElementNameAnalyzerUnitTest
{
    /// <summary>Verifies access to a named element via ItemN is reported (SST1142) and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedElementItemAccessRenamedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M()
                                  {
                                      (int Count, int Total) data = (1, 2);
                                      return data.{|SST1142:Item1|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M()
                                       {
                                           (int Count, int Total) data = (1, 2);
                                           return data.Count;
                                       }
                                   }
                                   """;
        var test = new VerifyTupleName.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = FixedSource };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies Fix All renames every positional tuple access in one pass (SST1142).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M()
                                  {
                                      (int Count, int Total) data = (1, 2);
                                      return data.{|SST1142:Item1|} + data.{|SST1142:Item2|};
                                  }

                                  public int N()
                                  {
                                      (int First, int Second) pair = (3, 4);
                                      return pair.{|SST1142:Item1|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M()
                                       {
                                           (int Count, int Total) data = (1, 2);
                                           return data.Count + data.Total;
                                       }

                                       public int N()
                                       {
                                           (int First, int Second) pair = (3, 4);
                                           return pair.First;
                                       }
                                   }
                                   """;
        var test = new VerifyTupleName.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = FixedSource };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies ItemN access on an unnamed tuple is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnnamedTupleItemAccessIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M()
                                  {
                                      (int, int) data = (1, 2);
                                      return data.Item1;
                                  }
                              }
                              """;
        var test = new VerifyTupleName.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = Source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule stays silent below C# 7, where the named-element access the fix emits does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp7Async()
    {
        // The named tuple arrives through a referenced project, so the consuming C# 6 source still
        // compiles its plain '.Item1' field access. At C# 7+ that access would be reported (the fix
        // rewrites it to '.Count'), so under the C# 6 pin the analyzer must stay silent.
        const string LibrarySource = """
                                     public static class TupleLib
                                     {
                                         public static (int Count, int Total) Get() => (1, 2);
                                     }
                                     """;
        const string Source = """
                              public class C
                              {
                                  public int M() => TupleLib.Get().Item1;
                              }
                              """;
        var test = new VerifyTupleName.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source };
        test.TestState.AdditionalProjects["TupleLib"].ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
        test.TestState.AdditionalProjects["TupleLib"].Sources.Add(LibrarySource);
        test.TestState.AdditionalProjectReferences.Add("TupleLib");
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp6));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
