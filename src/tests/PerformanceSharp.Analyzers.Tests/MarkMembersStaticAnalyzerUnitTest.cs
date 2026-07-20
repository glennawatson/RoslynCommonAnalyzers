// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeStatic = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1414MarkMembersStaticAnalyzer>;
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

    /// <summary>Verifies an internal method that never reads this is reported, but not auto-fixed (its call sites may lie outside this file).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalMethodWithoutInstanceStateIsReportedWithoutFixAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int Use(int a, int b) => Add(a, b);

                                  internal int {|PSH1414:Add|}(int a, int b) => a + b;
                              }
                              """;
        await VerifyReportedNet90Async(Source);
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

    /// <summary>Verifies a protected member is never reported, because a derived type binds to it as an instance member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectedMemberIsNotReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  protected int Add(int a, int b) => a + b;
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

    /// <summary>Verifies an unrelated attribute no longer exempts a member that could plainly be static.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberWithUnrelatedAttributeIsStillReportedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int Use(int a, int b) => Add(a, b);

                                  [Obsolete]
                                  private int {|PSH1414:Add|}(int a, int b) => a + b;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int Use(int a, int b) => Add(a, b);

                                       [Obsolete]
                                       private static int Add(int a, int b) => a + b;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a member carrying a test attribute is not reported, because the runner invokes it on an instance.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TestMethodMemberIsNotReportedAsync()
    {
        const string Source = """
                              namespace Xunit
                              {
                                  public sealed class FactAttribute : System.Attribute { }
                              }

                              public class C
                              {
                                  [Xunit.Fact]
                                  private void Runs() { }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a benchmark method is not reported, because BenchmarkDotNet requires an instance method.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BenchmarkMethodMemberIsNotReportedAsync()
    {
        const string Source = """
                              namespace BenchmarkDotNet.Attributes
                              {
                                  public sealed class BenchmarkAttribute : System.Attribute { }
                              }

                              public class C
                              {
                                  [BenchmarkDotNet.Attributes.Benchmark]
                                  internal int Measure() => 42;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a private helper in a test-fixture type is not reported, because the fixture is a reflection host.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberOfTestFixtureTypeIsNotReportedAsync()
    {
        const string Source = """
                              namespace Microsoft.VisualStudio.TestTools.UnitTesting
                              {
                                  public sealed class TestClassAttribute : System.Attribute { }
                              }

                              namespace NUnit.Framework
                              {
                                  public sealed class TestFixtureAttribute : System.Attribute { }
                              }

                              namespace BenchmarkDotNet.Attributes
                              {
                                  public sealed class MemoryDiagnoserAttribute : System.Attribute { }

                                  public sealed class SimpleJobAttribute : System.Attribute { }
                              }

                              [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
                              public class C
                              {
                                  public int Use(int a, int b) => Add(a, b);

                                  private int Add(int a, int b) => a + b;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a serialization callback is not reported, because it must stay an instance method with the callback signature.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SerializationCallbackMemberIsNotReportedAsync()
    {
        const string Source = """
                              using System.Runtime.Serialization;

                              public class C
                              {
                                  [OnDeserialized]
                                  private void AfterLoad(StreamingContext context) { }
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

    /// <summary>Verifies a member that names a captured primary constructor parameter is not reported.</summary>
    /// <remarks>
    /// Naming the parameter captures it into a synthesized instance field, so the member does depend on
    /// its receiver — and the compiler agrees: adding <c>static</c> here is CS9105, a build error. The
    /// rule must not hand out a diagnostic whose only remedy does not compile.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberUsingCapturedPrimaryConstructorParameterIsNotReportedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C(Action callback)
                              {
                                  private void Fire() => callback();

                                  public void Run() => Fire();
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
        var test = new VerifyStatic.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies, for a diagnostic the fix does not touch.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyReportedNet90Async(string source)
    {
        var test = new AnalyzeStatic.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
