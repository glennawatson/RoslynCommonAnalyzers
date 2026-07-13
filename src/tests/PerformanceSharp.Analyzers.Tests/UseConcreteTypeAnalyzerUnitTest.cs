// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyConcrete = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1415UseConcreteTypeAnalyzer,
    PerformanceSharp.Analyzers.Psh1415UseConcreteTypeCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1415 (hold the concrete type when the concrete type is what you have) and its code fix.</summary>
public class UseConcreteTypeAnalyzerUnitTest
{
    /// <summary>Verifies an interface-typed local holding one concrete type is reported and narrowed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceLocalIsNarrowedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int M()
                                  {
                                      {|PSH1415:IList<int>|} items = new List<int>();
                                      items.Add(1);
                                      return items.Count;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public int M()
                                       {
                                           List<int> items = new List<int>();
                                           items.Add(1);
                                           return items.Count;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an interface-typed private field holding one concrete type is reported and narrowed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceFieldIsNarrowedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  private {|PSH1415:IList<int>|} _items = new List<int>();

                                  public int Count => _items.Count;
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       private List<int> _items = new List<int>();

                                       public int Count => _items.Count;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a local reassigned from a different implementation is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalReassignedToOtherTypeIsNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int M(bool flag)
                                  {
                                      IList<int> items = new List<int>();
                                      if (flag)
                                      {
                                          items = new int[3];
                                      }

                                      return items.Count;
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a local reassigned from a factory is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalReassignedFromFactoryIsNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int M()
                                  {
                                      IList<int> items = new List<int>();
                                      items = Create();
                                      return items.Count;
                                  }

                                  private static IList<int> Create() => new List<int>();
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a local passed by reference is not reported, because the argument type must match exactly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalPassedByRefIsNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int M()
                                  {
                                      IList<int> items = new List<int>();
                                      Replace(ref items);
                                      return items.Count;
                                  }

                                  private static void Replace(ref IList<int> target) => target = new List<int>();
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a concrete type with an explicit implementation is not reported, because narrowing would hide it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitImplementationIsNotReportedAsync()
    {
        const string Source = """
                              public interface IGreeter
                              {
                                  string Greet();
                              }

                              public class Greeter : IGreeter
                              {
                                  string IGreeter.Greet() => "hello";
                              }

                              public class C
                              {
                                  public string M()
                                  {
                                      IGreeter greeter = new Greeter();
                                      return greeter.Greet();
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a public field is never reported, because its type is part of the API.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicFieldIsNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public IList<int> Items = new List<int>();
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a local that is already a concrete type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcreteLocalIsNotReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int M()
                                  {
                                      List<int> items = new List<int>();
                                      return items.Count;
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyConcrete.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
