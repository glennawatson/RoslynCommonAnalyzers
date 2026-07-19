// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyEmptyTestClass = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2504EmptyTestClassAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2504 (a test fixture that declares no test methods).</summary>
public class EmptyTestClassAnalyzerUnitTest
{
    /// <summary>The in-source MSTest attribute stubs each verifier source can declare in isolation.</summary>
    private const string MSTestStubs = """
                                       namespace Microsoft.VisualStudio.TestTools.UnitTesting
                                       {
                                           using System;
                                           public sealed class TestClassAttribute : Attribute { }
                                           public sealed class TestMethodAttribute : Attribute { }
                                           public sealed class DataTestMethodAttribute : Attribute { }
                                       }
                                       """;

    /// <summary>The in-source NUnit attribute stubs each verifier source can declare in isolation.</summary>
    private const string NUnitStubs = """
                                      namespace NUnit.Framework
                                      {
                                          using System;
                                          public sealed class TestFixtureAttribute : Attribute { }
                                          public sealed class TestAttribute : Attribute { }
                                          public sealed class TestCaseAttribute : Attribute { }
                                      }
                                      """;

    /// <summary>Verifies an MSTest test class with no test method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MSTestClassWithNoTestMethodIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|SST2504:Fixture|}
            {
                public void Helper() { }
            }
            """ + "\n" + MSTestStubs);

    /// <summary>Verifies an NUnit fixture with no test method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitFixtureWithNoTestIsFlaggedAsync()
        => await VerifyAsync(
            """
            using NUnit.Framework;

            [TestFixture]
            public class {|SST2504:Fixture|}
            {
                public void Helper() { }
            }
            """ + "\n" + NUnitStubs);

    /// <summary>Verifies an MSTest test class with a test method is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MSTestClassWithTestMethodIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Fixture
            {
                [TestMethod]
                public void Exercises() { }
            }
            """ + "\n" + MSTestStubs);

    /// <summary>Verifies an MSTest test class with a data-driven test is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MSTestClassWithDataTestMethodIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Fixture
            {
                [DataTestMethod]
                public void Exercises() { }
            }
            """ + "\n" + MSTestStubs);

    /// <summary>Verifies an NUnit fixture with a test is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitFixtureWithTestIsCleanAsync()
        => await VerifyAsync(
            """
            using NUnit.Framework;

            [TestFixture]
            public class Fixture
            {
                [Test]
                public void Exercises() { }
            }
            """ + "\n" + NUnitStubs);

    /// <summary>Verifies an NUnit fixture with a test case is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitFixtureWithTestCaseIsCleanAsync()
        => await VerifyAsync(
            """
            using NUnit.Framework;

            [TestFixture]
            public class Fixture
            {
                [TestCase]
                public void Exercises() { }
            }
            """ + "\n" + NUnitStubs);

    /// <summary>Verifies an abstract test class with no tests is a legitimate base and is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractTestClassIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class FixtureBase
            {
                public void Helper() { }
            }
            """ + "\n" + MSTestStubs);

    /// <summary>Verifies a concrete class inheriting test methods from a plain base is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritedTestMethodFromBaseIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class SharedTests
            {
                [TestMethod]
                public void Exercises() { }
            }

            [TestClass]
            public class Fixture : SharedTests
            {
            }
            """ + "\n" + MSTestStubs);

    /// <summary>Verifies an empty test class deriving from an unrelated base is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyTestClassWithNonTestBaseIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class PlainBase
            {
                public void Prepare() { }
            }

            [TestClass]
            public class {|SST2504:Fixture|} : PlainBase
            {
                public void Helper() { }
            }
            """ + "\n" + MSTestStubs);

    /// <summary>Verifies a concrete fixture derived from an abstract test-fixture base is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DerivedFromTestFixtureBaseIsCleanAsync()
        => await VerifyAsync(
            """
            using NUnit.Framework;

            [TestFixture]
            public abstract class FixtureBase
            {
            }

            [TestFixture]
            public class Fixture : FixtureBase
            {
            }
            """ + "\n" + NUnitStubs);

    /// <summary>Verifies an empty test class written with a fully-qualified attribute is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedTestClassAttributeIsFlaggedAsync()
        => await VerifyAsync(
            """
            [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
            public class {|SST2504:Fixture|}
            {
                public void Helper() { }
            }
            """ + "\n" + MSTestStubs);

    /// <summary>Verifies an empty MSTest class is reported when both supported frameworks are referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BothFrameworksReferencedFlagsEmptyClassAsync()
        => await VerifyAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class {|SST2504:Fixture|}
            {
                public void Helper() { }
            }
            """ + "\n" + MSTestStubs + "\n" + NUnitStubs);

    /// <summary>Verifies a class with no test-class attribute is never reported, even when the framework is present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonTestClassIsCleanAsync()
        => await VerifyAsync(
            """
            public class Ordinary
            {
                public void Helper() { }
            }
            """ + "\n" + MSTestStubs);

    /// <summary>Verifies a class carrying an unrelated attribute is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonTestClassWithUnrelatedAttributeIsCleanAsync()
        => await VerifyAsync(
            """
            [System.Serializable]
            public class Ordinary
            {
                public void Helper() { }
            }
            """ + "\n" + MSTestStubs);

    /// <summary>Verifies an attribute merely named like a test-class marker, but from elsewhere, is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LookAlikeTestClassAttributeIsCleanAsync()
        => await VerifyAsync(
            """
            using Custom;

            [TestClass]
            public class Fixture
            {
                public void Helper() { }
            }

            namespace Custom
            {
                using System;
                public sealed class TestClassAttribute : Attribute { }
            }
            """ + "\n" + MSTestStubs);

    /// <summary>Verifies a class with no test framework referenced is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoFrameworkReferencedIsCleanAsync()
        => await VerifyAsync(
            """
            public class Ordinary
            {
                public void Helper() { }
            }
            """);

    /// <summary>Verifies an xUnit-style class (a fact, no test-class attribute) is out of scope and never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XUnitClassIsOutOfScopeAsync()
        => await VerifyAsync(
            """
            using Xunit;

            public class Fixture
            {
                [Fact]
                public void Exercises() { }
            }

            namespace Xunit
            {
                using System;
                public sealed class FactAttribute : Attribute { }
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with any diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyEmptyTestClass.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
