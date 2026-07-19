// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2507ExpectedExceptionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2507 (a test method declaring its expected failure with an expected-exception attribute).</summary>
public class ExpectedExceptionAnalyzerUnitTest
{
    /// <summary>Verifies an MSTest expected-exception attribute on a test method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MsTestExpectedExceptionIsReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                [TestMethod]
                [{|SST2507:ExpectedException(typeof(System.InvalidOperationException))|}]
                public void M() => throw new System.InvalidOperationException();
            }

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                using System;
                public sealed class TestMethodAttribute : Attribute { }
                public sealed class ExpectedExceptionAttribute : Attribute { public ExpectedExceptionAttribute(Type exceptionType) { } }
            }
            """);

    /// <summary>Verifies the attribute is reported when written with its explicit <c>Attribute</c> suffix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitAttributeSuffixIsReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                [TestMethod]
                [{|SST2507:ExpectedExceptionAttribute(typeof(System.InvalidOperationException))|}]
                public void M() => throw new System.InvalidOperationException();
            }

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                using System;
                public sealed class TestMethodAttribute : Attribute { }
                public sealed class ExpectedExceptionAttribute : Attribute { public ExpectedExceptionAttribute(Type exceptionType) { } }
            }
            """);

    /// <summary>Verifies a fully-qualified expected-exception attribute is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedExpectedExceptionIsReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                [{|SST2507:Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedException(typeof(System.InvalidOperationException))|}]
                public void M() => throw new System.InvalidOperationException();
            }

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                using System;
                public sealed class TestMethodAttribute : Attribute { }
                public sealed class ExpectedExceptionAttribute : Attribute { public ExpectedExceptionAttribute(Type exceptionType) { } }
            }
            """);

    /// <summary>Verifies NUnit's legacy parameterless expected-exception attribute is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitLegacyExpectedExceptionIsReportedAsync()
        => await VerifyAsync(
            """
            using NUnit.Framework;

            public class C
            {
                [Test]
                [{|SST2507:ExpectedException|}]
                public void M() => throw new System.InvalidOperationException();
            }

            namespace NUnit.Framework
            {
                using System;
                public sealed class TestAttribute : Attribute { }
                public sealed class ExpectedExceptionAttribute : Attribute { }
            }
            """);

    /// <summary>Verifies a test that asserts the specific operation with <c>Assert.Throws</c> is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssertThrowsIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                [TestMethod]
                public void M()
                    => Assert.ThrowsException<System.InvalidOperationException>(() => throw new System.InvalidOperationException());
            }

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                using System;
                public sealed class TestMethodAttribute : Attribute { }
                public sealed class ExpectedExceptionAttribute : Attribute { public ExpectedExceptionAttribute(Type exceptionType) { } }
                public static class Assert
                {
                    public static T ThrowsException<T>(Action action)
                        where T : Exception => default!;
                }
            }
            """);

    /// <summary>Verifies a test method with an ordinary assertion and no expected-exception attribute is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TestWithoutExpectedExceptionIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                [TestMethod]
                public void M()
                {
                }
            }

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                using System;
                public sealed class TestMethodAttribute : Attribute { }
                public sealed class ExpectedExceptionAttribute : Attribute { public ExpectedExceptionAttribute(Type exceptionType) { } }
            }
            """);

    /// <summary>Verifies an expected-exception attribute on a non-test method is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonTestMethodWithExpectedExceptionIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                [ExpectedException(typeof(System.InvalidOperationException))]
                public void M() => throw new System.InvalidOperationException();
            }

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                using System;
                public sealed class TestMethodAttribute : Attribute { }
                public sealed class ExpectedExceptionAttribute : Attribute { public ExpectedExceptionAttribute(Type exceptionType) { } }
            }
            """);

    /// <summary>Verifies an unrelated attribute that merely shares the <c>ExpectedException</c> name is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SharedNameAttributeIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                [TestMethod]
                [My.ExpectedException(typeof(System.InvalidOperationException))]
                public void M() => throw new System.InvalidOperationException();
            }

            namespace My
            {
                using System;
                public sealed class ExpectedExceptionAttribute : Attribute { public ExpectedExceptionAttribute(Type exceptionType) { } }
            }

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                using System;
                public sealed class TestMethodAttribute : Attribute { }
                public sealed class ExpectedExceptionAttribute : Attribute { public ExpectedExceptionAttribute(Type exceptionType) { } }
            }
            """);

    /// <summary>Verifies the rule stays silent when no test-framework marker type resolves.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpectedExceptionWithoutTestMarkerTypeIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                [ExpectedException(typeof(System.InvalidOperationException))]
                public void M() => throw new System.InvalidOperationException();
            }

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                using System;
                public sealed class ExpectedExceptionAttribute : Attribute { public ExpectedExceptionAttribute(Type exceptionType) { } }
            }
            """);

    /// <summary>Verifies the rule stays silent when no test framework is referenced at all.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoTestFrameworkIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M() => throw new System.InvalidOperationException();
            }
            """);

    /// <summary>Runs a source against the .NET 8 reference assemblies with in-source framework stubs.</summary>
    /// <param name="source">The test source, with markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
