// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyStatic = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1414MarkMembersStaticAnalyzer,
    PerformanceSharp.Analyzers.Psh1414MarkMembersStaticCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1414 (mark members that do not touch instance state as static) and its code fix.</summary>
public class MarkMembersStaticAnalyzerUnitTest
{
    /// <summary>Verifies a private method that never reads this is reported and made static.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateMethodWithoutInstanceStateIsMadeStaticAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int Use(int a, int b) => Add(a, b);

                                  private int {|PSH1414:Add|}(int a, int b) => a + b;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Use(int a, int b) => Add(a, b);

                                       private static int Add(int a, int b) => a + b;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a this-qualified call site is unqualified so it keeps compiling once the member is static.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisQualifiedCallSiteIsUnqualifiedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int Use(int a, int b) => this.Add(a, b);

                                  private int {|PSH1414:Add|}(int a, int b) => a + b;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Use(int a, int b) => Add(a, b);

                                       private static int Add(int a, int b) => a + b;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a private computed property that never reads this is reported and made static.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateComputedPropertyIsMadeStaticAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int Use() => Zero;

                                  private int {|PSH1414:Zero|} => 0;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Use() => Zero;

                                       private static int Zero => 0;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a member that reads an instance field is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberReadingInstanceFieldIsNotReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _seed;

                                  public int Use(int a) => Add(a);

                                  private int Add(int a) => a + _seed;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a member that mentions this explicitly is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberUsingThisIsNotReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public object Use() => Self();

                                  private object Self() => this;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a public member is never reported, because making it static breaks its callers.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMemberIsNotReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int Add(int a, int b) => a + b;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a virtual member is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VirtualMemberIsNotReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  protected virtual int Add(int a, int b) => a + b;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a member carrying an attribute is skipped, since reflection may reach it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AttributedMemberIsNotReportedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int Use(int a, int b) => Add(a, b);

                                  [Obsolete]
                                  private int Add(int a, int b) => a + b;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies an auto-property is never reported, because it is instance state.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AutoPropertyIsNotReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int Value { get; set; }

                                  public int Use() => Value;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies an unqualified recursive call does not count as reading instance state.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecursiveMemberIsStillReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int Use(int n) => Countdown(n);

                                  private int {|PSH1414:Countdown|}(int n) => n <= 0 ? 0 : Countdown(n - 1);
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Use(int n) => Countdown(n);

                                       private static int Countdown(int n) => n <= 0 ? 0 : Countdown(n - 1);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyStatic.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
