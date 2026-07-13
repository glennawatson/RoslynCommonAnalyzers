// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConstant = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1493MethodReturnsConstantAnalyzer,
    StyleSharp.Analyzers.Sst1493MethodReturnsConstantCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1493 (methods should not return a constant) and its fix.</summary>
public class Sst1493MethodReturnsConstantAnalyzerUnitTest
{
    /// <summary>Verifies an expression-bodied constant method becomes a get-only property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedConstantBecomesAPropertyAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int {|SST1493:Limit|}() => 5;

                                  public int Twice() => Limit() * 2;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Limit => 5;

                                       public int Twice() => Limit * 2;
                                   }
                                   """;
        await VerifyConstant.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a block body whose only statement returns a constant becomes an expression-bodied property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockBodiedConstantBecomesAPropertyAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string {|SST1493:Name|}()
                                  {
                                      return "fixed";
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string Name => "fixed";
                                   }
                                   """;
        await VerifyConstant.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a method returning a named constant is reported, and its call sites are rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedConstantIsReportedAndCallSitesAreRewrittenAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private const int Max = 10;

                                  public int {|SST1493:Ceiling|}() => Max;

                                  public bool IsOver(int value) => value > this.Ceiling();
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private const int Max = 10;

                                       public int Ceiling => Max;

                                       public bool IsOver(int value) => value > this.Ceiling;
                                   }
                                   """;
        await VerifyConstant.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a static method returning a constant is reported, and its qualified call sites are rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticConstantMethodIsReportedAsync()
    {
        const string Source = """
                              public static class Limits
                              {
                                  public static int {|SST1493:Retries|}() => 3;
                              }

                              public sealed class C
                              {
                                  public int Budget() => Limits.Retries() + 1;
                              }
                              """;
        const string FixedSource = """
                                   public static class Limits
                                   {
                                       public static int Retries => 3;
                                   }

                                   public sealed class C
                                   {
                                       public int Budget() => Limits.Retries + 1;
                                   }
                                   """;
        await VerifyConstant.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a boolean constant is reported like any other value.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BooleanConstantIsReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool {|SST1493:IsEnabled|}() => true;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool IsEnabled => true;
                                   }
                                   """;
        await VerifyConstant.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an override is left alone; answering with a constant is what an override is for.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverrideIsCleanAsync()
        => await VerifyConstant.VerifyAnalyzerAsync(
            """
            public abstract class Base
            {
                public abstract int Weight();

                public virtual bool IsReadOnly() => false;
            }

            public sealed class Derived : Base
            {
                public override int Weight() => 1;

                public override bool IsReadOnly() => true;
            }
            """);

    /// <summary>Verifies a virtual method is left alone; a derived type may answer differently.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VirtualMethodIsCleanAsync()
        => await VerifyConstant.VerifyAnalyzerAsync(
            """
            public class Service
            {
                public virtual int Weight() => 1;
            }
            """);

    /// <summary>Verifies an interface implementation is left alone, implicit or explicit.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceImplementationIsCleanAsync()
        => await VerifyConstant.VerifyAnalyzerAsync(
            """
            public interface IWeighted
            {
                int Weight();

                int Height();
            }

            public sealed class Feather : IWeighted
            {
                public int Weight() => 1;

                int IWeighted.Height() => 2;
            }
            """);

    /// <summary>Verifies an interface's own member is left alone, body or no body.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceMemberIsCleanAsync()
        => await VerifyConstant.VerifyAnalyzerAsync(
            """
            public interface IWeighted
            {
                int Weight();
            }
            """);

    /// <summary>Verifies a method carrying any attribute is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The attribute may be the whole reason the member is a method, and this rule cannot know what reads it.</remarks>
    [Test]
    public async Task AttributedMethodIsCleanAsync()
        => await VerifyConstant.VerifyAnalyzerAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class CaseAttribute : Attribute
            {
            }

            public sealed class C
            {
                [Case]
                public int Weight() => 1;
            }
            """);

    /// <summary>Verifies a partial method is left alone; its shape belongs to the declaration it follows.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialMethodIsCleanAsync()
        => await VerifyConstant.VerifyAnalyzerAsync(
            """
            public partial class C
            {
                public partial int Weight();

                public partial int Weight() => 1;
            }
            """);

    /// <summary>Verifies returning null or default is a "nothing to give you" answer, not a constant.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullAndDefaultGuardShapesAreCleanAsync()
        => await VerifyConstant.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string Find() => null;

                public int Missing() => default;

                public int Empty()
                {
                    return default(int);
                }
            }
            """);

    /// <summary>Verifies a method that takes parameters or type parameters is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Neither could become a property, so the rule's advice would not be followable.</remarks>
    [Test]
    public async Task ParameterizedAndGenericMethodsAreCleanAsync()
        => await VerifyConstant.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Ignore(int value) => 1;

                public int Count<T>() => 1;
            }
            """);

    /// <summary>Verifies a method that computes something is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComputedResultIsCleanAsync()
        => await VerifyConstant.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _value;

                public int Value() => _value;

                public string Text() => string.Empty;

                public int Sum()
                {
                    var total = 0;
                    return total + 1;
                }
            }
            """);

    /// <summary>Verifies a local function is not a member and is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFunctionIsCleanAsync()
        => await VerifyConstant.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Run()
                {
                    int Weight() => 1;
                    return Weight();
                }
            }
            """);

    /// <summary>Verifies the report stands but no fix is offered when the method is used as a method group.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A property cannot be handed to a delegate, so the rewrite would not compile and is not offered.</remarks>
    [Test]
    public async Task MethodGroupUseIsReportedWithNoFixAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public int {|SST1493:Weight|}() => 1;

                                  public Func<int> Get() => Weight;
                              }
                              """;
        await VerifyConstant.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies no fix is offered when the value is discarded by a call written as a statement.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A property read is not a statement, so that call site could not survive the rewrite.</remarks>
    [Test]
    public async Task StatementCallIsReportedWithNoFixAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int {|SST1493:Weight|}() => 1;

                                  public void Touch()
                                  {
                                      Weight();
                                  }
                              }
                              """;
        await VerifyConstant.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies no fix is offered when an overload already carries the name a property would take.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverloadedMethodIsReportedWithNoFixAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int {|SST1493:Weight|}() => 1;

                                  public int Weight(int scale) => scale;
                              }
                              """;
        await VerifyConstant.VerifyCodeFixAsync(Source, Source);
    }
}
