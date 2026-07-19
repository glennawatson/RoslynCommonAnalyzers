// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyReversedEquality = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2502ReversedEqualityAssertionAnalyzer,
    StyleSharp.Analyzers.Sst2502ReversedEqualityAssertionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2502 (an equality assertion's expected and actual arguments reversed) and its fix.</summary>
public class ReversedEqualityAssertionAnalyzerUnitTest
{
    /// <summary>Verifies an xUnit assertion with a constant second and a computed first is reported and swapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitReversedEqualityIsFlaggedAndFixedAsync()
    {
        const string Source = """
            using Xunit;

            namespace Xunit
            {
                using System;
                public sealed class FactAttribute : Attribute { }
                public static class Assert { public static void Equal<T>(T expected, T actual) { } }
            }

            public sealed class C
            {
                [Fact]
                public void M(int result)
                {
                    Assert.Equal(result, {|SST2502:42|});
                }
            }
            """;
        const string FixedSource = """
            using Xunit;

            namespace Xunit
            {
                using System;
                public sealed class FactAttribute : Attribute { }
                public static class Assert { public static void Equal<T>(T expected, T actual) { } }
            }

            public sealed class C
            {
                [Fact]
                public void M(int result)
                {
                    Assert.Equal(42, result);
                }
            }
            """;
        await VerifyReversedEquality.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an MSTest assertion with the constant in the actual position is reported and swapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MsTestReversedEqualityIsFlaggedAndFixedAsync()
    {
        const string Source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                using System;
                public sealed class TestMethodAttribute : Attribute { }
                public static class Assert { public static void AreEqual(object expected, object actual) { } }
            }

            public sealed class C
            {
                [TestMethod]
                public void M(int result)
                {
                    Assert.AreEqual(result, {|SST2502:42|});
                }
            }
            """;
        const string FixedSource = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                using System;
                public sealed class TestMethodAttribute : Attribute { }
                public static class Assert { public static void AreEqual(object expected, object actual) { } }
            }

            public sealed class C
            {
                [TestMethod]
                public void M(int result)
                {
                    Assert.AreEqual(42, result);
                }
            }
            """;
        await VerifyReversedEquality.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a NUnit classic assertion with a reversed constant is reported and swapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitClassicReversedEqualityIsFlaggedAndFixedAsync()
    {
        const string Source = """
            using NUnit.Framework;

            namespace NUnit.Framework
            {
                using System;
                public sealed class TestAttribute : Attribute { }
                public static class Assert { public static void AreEqual(object expected, object actual) { } }
            }

            public sealed class C
            {
                [Test]
                public void M(int result)
                {
                    Assert.AreEqual(result, {|SST2502:42|});
                }
            }
            """;
        const string FixedSource = """
            using NUnit.Framework;

            namespace NUnit.Framework
            {
                using System;
                public sealed class TestAttribute : Attribute { }
                public static class Assert { public static void AreEqual(object expected, object actual) { } }
            }

            public sealed class C
            {
                [Test]
                public void M(int result)
                {
                    Assert.AreEqual(42, result);
                }
            }
            """;
        await VerifyReversedEquality.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a <c>nameof</c> constant in the actual position is reported and swapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NameofConstantInActualPositionIsFlaggedAndFixedAsync()
    {
        const string Source = """
            using Xunit;

            namespace Xunit
            {
                using System;
                public sealed class FactAttribute : Attribute { }
                public static class Assert { public static void Equal<T>(T expected, T actual) { } }
            }

            public sealed class C
            {
                [Fact]
                public void M(string name)
                {
                    Assert.Equal(name, {|SST2502:nameof(C)|});
                }
            }
            """;
        const string FixedSource = """
            using Xunit;

            namespace Xunit
            {
                using System;
                public sealed class FactAttribute : Attribute { }
                public static class Assert { public static void Equal<T>(T expected, T actual) { } }
            }

            public sealed class C
            {
                [Fact]
                public void M(string name)
                {
                    Assert.Equal(nameof(C), name);
                }
            }
            """;
        await VerifyReversedEquality.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the rule sees an assertion imported with <c>using static</c>, reported through its simple name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingStaticAssertionIsFlaggedAndFixedAsync()
    {
        const string Source = """
            using static Xunit.Assert;

            namespace Xunit
            {
                public static class Assert { public static void Equal<T>(T expected, T actual) { } }
            }

            public sealed class C
            {
                public void M(int result)
                {
                    Equal(result, {|SST2502:42|});
                }
            }
            """;
        const string FixedSource = """
            using static Xunit.Assert;

            namespace Xunit
            {
                public static class Assert { public static void Equal<T>(T expected, T actual) { } }
            }

            public sealed class C
            {
                public void M(int result)
                {
                    Equal(42, result);
                }
            }
            """;
        await VerifyReversedEquality.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the common, correct shape — a constant expected value first — is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantExpectedFirstIsCleanAsync()
        => await VerifyReversedEquality.VerifyAnalyzerAsync(
            """
            using Xunit;

            namespace Xunit
            {
                using System;
                public sealed class FactAttribute : Attribute { }
                public static class Assert { public static void Equal<T>(T expected, T actual) { } }
            }

            public sealed class C
            {
                [Fact]
                public void M(int result)
                {
                    Assert.Equal(42, result);
                }
            }
            """);

    /// <summary>Verifies an assertion whose arguments are both constant is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BothConstantArgumentsAreCleanAsync()
        => await VerifyReversedEquality.VerifyAnalyzerAsync(
            """
            using Xunit;

            namespace Xunit
            {
                using System;
                public sealed class FactAttribute : Attribute { }
                public static class Assert { public static void Equal<T>(T expected, T actual) { } }
            }

            public sealed class C
            {
                [Fact]
                public void M()
                {
                    Assert.Equal(1, 2);
                }
            }
            """);

    /// <summary>Verifies an assertion whose arguments are both computed is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BothComputedArgumentsAreCleanAsync()
        => await VerifyReversedEquality.VerifyAnalyzerAsync(
            """
            using Xunit;

            namespace Xunit
            {
                using System;
                public sealed class FactAttribute : Attribute { }
                public static class Assert { public static void Equal<T>(T expected, T actual) { } }
            }

            public sealed class C
            {
                [Fact]
                public void M(int first, int second)
                {
                    Assert.Equal(first, second);
                }
            }
            """);

    /// <summary>Verifies the actual-first fluent form is never reported — it is not a reversal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FluentEqualToFormIsCleanAsync()
        => await VerifyReversedEquality.VerifyAnalyzerAsync(
            """
            using NUnit.Framework;

            namespace NUnit.Framework
            {
                using System;
                public sealed class TestAttribute : Attribute { }
                public sealed class EqualConstraint { }
                public static class Assert
                {
                    public static void That(object actual, EqualConstraint constraint) { }
                    public static void AreEqual(object expected, object actual) { }
                }
                public static class Is
                {
                    public static EqualConstraint EqualTo(object expected) => new EqualConstraint();
                }
            }

            public sealed class C
            {
                [Test]
                public void M(int result)
                {
                    Assert.That(result, Is.EqualTo(42));
                }
            }
            """);

    /// <summary>Verifies a named-argument call settles the order at the call site, so nothing is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedArgumentsAreCleanAsync()
        => await VerifyReversedEquality.VerifyAnalyzerAsync(
            """
            using Xunit;

            namespace Xunit
            {
                using System;
                public sealed class FactAttribute : Attribute { }
                public static class Assert { public static void Equal<T>(T expected, T actual) { } }
            }

            public sealed class C
            {
                [Fact]
                public void M(int result)
                {
                    Assert.Equal(expected: result, actual: 42);
                }
            }
            """);

    /// <summary>Verifies a same-named method on a type that is not the framework's assertion host is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedMethodOnOtherTypeIsCleanAsync()
        => await VerifyReversedEquality.VerifyAnalyzerAsync(
            """
            using Xunit;

            namespace Xunit
            {
                using System;
                public sealed class FactAttribute : Attribute { }
                public static class Assert { public static void Equal<T>(T expected, T actual) { } }
            }

            public static class Calc
            {
                public static void Equal(int expected, int actual) { }
            }

            public sealed class C
            {
                [Fact]
                public void M(int result)
                {
                    Calc.Equal(result, 42);
                }
            }
            """);

    /// <summary>Verifies that with no test framework referenced the rule registers nothing and reports nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoTestFrameworkReferencedIsCleanAsync()
        => await VerifyReversedEquality.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private static void Equal(int expected, int actual) { }

                public void M(int result)
                {
                    Equal(result, 42);
                }
            }
            """);
}
