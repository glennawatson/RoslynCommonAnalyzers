// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2501SelfComparisonAssertionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2501 (an equality or identity assertion that compares an expression with itself).</summary>
public class SelfComparisonAssertionAnalyzerUnitTest
{
    /// <summary>Minimal xUnit <c>Assert</c> and <c>[Fact]</c> stubs appended to a test source so it compiles in isolation.</summary>
    private const string XUnitStub = """

                                     namespace Xunit
                                     {
                                         using System;
                                         public sealed class FactAttribute : Attribute { }
                                         public static class Assert
                                         {
                                             public static void Equal<T>(T expected, T actual) { }
                                             public static void StrictEqual<T>(T expected, T actual) { }
                                             public static void NotEqual<T>(T expected, T actual) { }
                                             public static void Same(object expected, object actual) { }
                                             public static void NotSame(object expected, object actual) { }
                                         }
                                     }
                                     """;

    /// <summary>Minimal NUnit <c>Assert</c>, <c>Is</c>, and <c>[Test]</c> stubs appended to a test source so it compiles in isolation.</summary>
    private const string NUnitStub = """

                                     namespace NUnit.Framework
                                     {
                                         using System;
                                         public sealed class TestAttribute : Attribute { }
                                         public static class Assert
                                         {
                                             public static void AreEqual(object expected, object actual) { }
                                             public static void AreSame(object expected, object actual) { }
                                             public static void That(object actual, object constraint) { }
                                         }
                                         public static class Is
                                         {
                                             public static object EqualTo(object expected) => new object();
                                             public static object SameAs(object expected) => new object();
                                             public static object GreaterThan(object expected) => new object();
                                         }
                                     }
                                     """;

    /// <summary>Minimal MSTest <c>Assert</c>, <c>[TestClass]</c>, and <c>[TestMethod]</c> stubs appended to a test source so it compiles in isolation.</summary>
    private const string MSTestStub = """

                                      namespace Microsoft.VisualStudio.TestTools.UnitTesting
                                      {
                                          using System;
                                          public sealed class TestClassAttribute : Attribute { }
                                          public sealed class TestMethodAttribute : Attribute { }
                                          public static class Assert
                                          {
                                              public static void AreEqual(object expected, object actual) { }
                                              public static void AreSame(object expected, object actual) { }
                                              public static void AreNotEqual(object expected, object actual) { }
                                              public static void AreNotSame(object expected, object actual) { }
                                          }
                                      }
                                      """;

    /// <summary>Verifies an xUnit <c>Assert.Equal</c> comparing a local with itself is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XUnitEqualSameVariableIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void M()
                {
                    var x = 1;
                    {|SST2501:Assert.Equal(x, x)|};
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies an xUnit <c>Assert.Same</c> comparing a reference with itself is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XUnitSameSameVariableIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void M()
                {
                    var a = new object();
                    {|SST2501:Assert.Same(a, a)|};
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies an xUnit <c>Assert.NotEqual</c> comparing a local with itself is reported (it can never pass).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XUnitNotEqualSameVariableIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void M()
                {
                    var x = 1;
                    {|SST2501:Assert.NotEqual(x, x)|};
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies an MSTest <c>Assert.AreEqual</c> comparing a constant with itself is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MSTestAreEqualSameConstantIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Tests
            {
                [TestMethod]
                public void M()
                {
                    {|SST2501:Assert.AreEqual(5, 5)|};
                }
            }
            """ + MSTestStub);

    /// <summary>Verifies an MSTest <c>Assert.AreNotSame</c> comparing a reference with itself is reported (it can never pass).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MSTestAreNotSameSameVariableIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Tests
            {
                [TestMethod]
                public void M()
                {
                    var a = new object();
                    {|SST2501:Assert.AreNotSame(a, a)|};
                }
            }
            """ + MSTestStub);

    /// <summary>Verifies an NUnit <c>Assert.That(x, Is.EqualTo(x))</c> comparing the actual with itself is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitThatEqualToSelfIsFlaggedAsync()
        => await VerifyAsync(
            """
            using NUnit.Framework;

            public class Tests
            {
                [Test]
                public void M()
                {
                    var x = 1;
                    {|SST2501:Assert.That(x, Is.EqualTo(x))|};
                }
            }
            """ + NUnitStub);

    /// <summary>Verifies a self-comparison through a member-access operand is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberAccessSelfIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Box { public int Value; }

            public class Tests
            {
                [Fact]
                public void M()
                {
                    var box = new Box();
                    {|SST2501:Assert.Equal(box.Value, box.Value)|};
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies an assertion comparing two different operands is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentOperandsAreCleanAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void M()
                {
                    var x = 1;
                    var y = 2;
                    Assert.Equal(x, y);
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies a self-comparison whose operands are method calls is never reported, since two calls need not agree.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodCallOperandsAreCleanAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Tests
            {
                private int Next() => 1;

                [Fact]
                public void M()
                {
                    Assert.Equal(Next(), Next());
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies an <c>Assert.That</c> whose constraint compares a different value is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitThatEqualToDifferentIsCleanAsync()
        => await VerifyAsync(
            """
            using NUnit.Framework;

            public class Tests
            {
                [Test]
                public void M()
                {
                    var x = 1;
                    var y = 2;
                    Assert.That(x, Is.EqualTo(y));
                }
            }
            """ + NUnitStub);

    /// <summary>Verifies a same-named method that is not a framework assertion is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonAssertionCallIsCleanAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public static class Check
            {
                public static void Equal(int expected, int actual) { }
            }

            public class Tests
            {
                [Fact]
                public void M()
                {
                    var x = 1;
                    Check.Equal(x, x);
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies that with no test framework referenced, a same-named <c>Assert</c> of the project's own is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoFrameworkReferenceIsCleanAsync()
        => await VerifyAsync(
            """
            public static class Assert
            {
                public static void Equal(int expected, int actual) { }
            }

            public class Tests
            {
                public void M()
                {
                    var x = 1;
                    Assert.Equal(x, x);
                }
            }
            """);

    /// <summary>Verifies a self-comparison reached through a <c>using static</c> import (a bare method name) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingStaticEqualSelfIsFlaggedAsync()
        => await VerifyAsync(
            """
            using static Xunit.Assert;

            public class Tests
            {
                public void M()
                {
                    var x = 1;
                    {|SST2501:Equal(x, x)|};
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies an <c>Assert.That</c> whose constraint is not <c>EqualTo</c>/<c>SameAs</c> is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitThatNonEqualityConstraintIsCleanAsync()
        => await VerifyAsync(
            """
            using NUnit.Framework;

            public class Tests
            {
                [Test]
                public void M()
                {
                    var x = 1;
                    Assert.That(x, Is.GreaterThan(x));
                }
            }
            """ + NUnitStub);

    /// <summary>Verifies a self-comparison whose first argument is named is left alone, since named arguments may reorder operands.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedFirstArgumentIsCleanAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void M()
                {
                    var x = 1;
                    Assert.Equal(expected: x, actual: x);
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies a self-comparison whose second argument is named is left alone, since named arguments may reorder operands.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedSecondArgumentIsCleanAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void M()
                {
                    var x = 1;
                    Assert.Equal(x, actual: x);
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies two separate object creations are never reported, since each allocates a distinct instance.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparateCreationsAreCleanAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Box { }

            public class Tests
            {
                [Fact]
                public void M()
                {
                    Assert.Same(new Box(), new Box());
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies an <c>Assert.That</c> whose second argument is not a constraint call is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitThatBareSecondArgumentIsCleanAsync()
        => await VerifyAsync(
            """
            using NUnit.Framework;

            public class Tests
            {
                [Test]
                public void M()
                {
                    var x = 1;
                    Assert.That(x, x);
                }
            }
            """ + NUnitStub);

    /// <summary>Verifies a self-comparison whose operands increment a variable is never reported, since each read has a side effect.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IncrementOperandsAreCleanAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void M()
                {
                    var x = 1;
                    Assert.Equal(x++, x++);
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies an invocation whose callee is not a simple name (a delegate reached through an indexer) is ignored.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateThroughIndexerIsCleanAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void M()
                {
                    var handlers = new System.Func<int>[1];
                    var v = handlers[0]();
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies an <c>Assert.That</c> whose constraint argument is named is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitThatNamedConstraintArgumentIsCleanAsync()
        => await VerifyAsync(
            """
            using NUnit.Framework;

            public class Tests
            {
                [Test]
                public void M()
                {
                    var x = 1;
                    Assert.That(x, Is.EqualTo(expected: x));
                }
            }
            """ + NUnitStub);

    /// <summary>Verifies a self-comparison whose operands nest a method call is never reported, even when the outer shape is stable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedCallInOperandIsCleanAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Tests
            {
                private int Next() => 1;

                [Fact]
                public void M()
                {
                    var a = 1;
                    Assert.Equal(a + Next(), a + Next());
                }
            }
            """ + XUnitStub);

    /// <summary>Verifies a self-comparison whose operands assign a variable is never reported, since each read has a side effect.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignmentOperandsAreCleanAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void M()
                {
                    var x = 0;
                    Assert.Equal(x = 1, x = 1);
                }
            }
            """ + XUnitStub);

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies; embedded markup carries the expectations.</summary>
    /// <param name="source">The source with any diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
