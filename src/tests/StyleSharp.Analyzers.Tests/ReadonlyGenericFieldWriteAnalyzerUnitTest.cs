// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyWrite = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2421ReadonlyGenericFieldWriteAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2421 (a write through a readonly field of an unconstrained type parameter).</summary>
public class ReadonlyGenericFieldWriteAnalyzerUnitTest
{
    /// <summary>The shared point interface used by the fixtures.</summary>
    private const string PointInterface = """
        public interface IPoint
        {
            int X { get; set; }

            void Bump();
        }
        """;

    /// <summary>Verifies a property assignment through a readonly generic field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyAssignmentIsReportedAsync()
        => await VerifyWrite.VerifyAnalyzerAsync(
            PointInterface + """

            public sealed class Holder<T>
                where T : IPoint
            {
                private readonly T _p;

                public Holder(T p) => _p = p;

                public void M() => {|SST2421:_p.X = 5|};
            }
            """);

    /// <summary>Verifies a mutating-method call through a readonly generic field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MutatingCallIsReportedAsync()
        => await VerifyWrite.VerifyAnalyzerAsync(
            PointInterface + """

            public sealed class Holder<T>
                where T : IPoint
            {
                private readonly T _p;

                public Holder(T p) => _p = p;

                public void M() => {|SST2421:_p.Bump()|};
            }
            """);

    /// <summary>Verifies a reference-constrained type parameter is clean: the write lands.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceConstrainedIsCleanAsync()
        => await VerifyWrite.VerifyAnalyzerAsync(
            PointInterface + """

            public sealed class RefHolder<T>
                where T : class, IPoint
            {
                private readonly T _p;

                public RefHolder(T p) => _p = p;

                public void M() => _p.X = 5;
            }
            """);

    /// <summary>Verifies an interface-typed field is clean: it holds a reference, not a struct copy.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceFieldIsCleanAsync()
        => await VerifyWrite.VerifyAnalyzerAsync(
            PointInterface + """

            public sealed class InterfaceHolder
            {
                private readonly IPoint _p;

                public InterfaceHolder(IPoint p) => _p = p;

                public void M() => _p.X = 5;
            }
            """);

    /// <summary>Verifies an object member call is clean: it cannot mutate the value.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectMemberCallIsCleanAsync()
        => await VerifyWrite.VerifyAnalyzerAsync(
            PointInterface + """

            public sealed class Holder<T>
                where T : IPoint
            {
                private readonly T _p;

                public Holder(T p) => _p = p;

                public int M() => _p.GetHashCode();
            }
            """);
}
