// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAnalyzer = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.OverrideParameterContractAnalyzer>;
using VerifyParamsFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.OverrideParameterContractAnalyzer,
    StyleSharp.Analyzers.Sst2426OverrideChangesParamsCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2424 (override changes a parameter default) and SST2426 (override changes params).</summary>
public class OverrideParameterContractAnalyzerUnitTest
{
    /// <summary>A params modifier the override drops relative to the base.</summary>
    private const string DroppedParamsSource = """
        public class Base
        {
            public virtual void Log(string first, params int[] rest)
            {
            }
        }

        public class Derived : Base
        {
            public override void Log(string first, int[] {|SST2426:rest|})
            {
            }
        }
        """;

    /// <summary>The dropped-params case after the fix restores params.</summary>
    private const string DroppedParamsFixed = """
        public class Base
        {
            public virtual void Log(string first, params int[] rest)
            {
            }
        }

        public class Derived : Base
        {
            public override void Log(string first, params int[] rest)
            {
            }
        }
        """;

    /// <summary>A params modifier the override adds relative to the base.</summary>
    private const string AddedParamsSource = """
        public class Base
        {
            public virtual void Log(string first, int[] rest)
            {
            }
        }

        public class Derived : Base
        {
            public override void Log(string first, params int[] {|SST2426:rest|})
            {
            }
        }
        """;

    /// <summary>The added-params case after the fix removes params.</summary>
    private const string AddedParamsFixed = """
        public class Base
        {
            public virtual void Log(string first, int[] rest)
            {
            }
        }

        public class Derived : Base
        {
            public override void Log(string first, int[] rest)
            {
            }
        }
        """;

    /// <summary>Verifies an override that changes a parameter's default is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ChangedDefaultIsReportedAsync()
        => await VerifyAnalyzer.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual int Go(int a, int b = 1) => b;
            }

            public class Derived : Base
            {
                public override int Go(int a, int {|SST2424:b|} = 2) => b;
            }
            """);

    /// <summary>Verifies an override that drops the base's default is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DroppedDefaultIsReportedAsync()
        => await VerifyAnalyzer.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual int Go(int a, int b = 1) => b;
            }

            public class Derived : Base
            {
                public override int Go(int a, int {|SST2424:b|}) => b;
            }
            """);

    /// <summary>Verifies an override that repeats the base's default is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RepeatedDefaultIsCleanAsync()
        => await VerifyAnalyzer.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual int Go(int a, int b = 1) => b;
            }

            public class Derived : Base
            {
                public override int Go(int a, int b = 1) => b;
            }
            """);

    /// <summary>Verifies an override that keeps the base's params modifier is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MatchingParamsIsCleanAsync()
        => await VerifyAnalyzer.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Log(string first, params int[] rest)
                {
                }
            }

            public class Derived : Base
            {
                public override void Log(string first, params int[] rest)
                {
                }
            }
            """);

    /// <summary>Verifies dropping the base's params modifier is reported and restored.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DroppedParamsIsReportedAndFixedAsync()
        => await VerifyParamsFix.VerifyCodeFixAsync(DroppedParamsSource, DroppedParamsFixed);

    /// <summary>Verifies adding a params modifier the base lacks is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddedParamsIsReportedAndFixedAsync()
        => await VerifyParamsFix.VerifyCodeFixAsync(AddedParamsSource, AddedParamsFixed);
}
