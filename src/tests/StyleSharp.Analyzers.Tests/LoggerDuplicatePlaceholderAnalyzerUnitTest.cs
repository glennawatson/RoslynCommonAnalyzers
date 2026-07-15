// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLogger = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.LoggerCallAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2442 (a placeholder name repeated within one template).</summary>
public class LoggerDuplicatePlaceholderAnalyzerUnitTest
{
    /// <summary>Verifies a repeated placeholder name is reported on its second use.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RepeatedNameIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int id) => logger.LogInformation("{Id} then {|SST2442:{Id}|}", id, id);
            }
            """));

    /// <summary>Verifies a case-only difference is treated as the same name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CaseOnlyDifferenceIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int id) => logger.LogInformation("{Id} {|SST2442:{id}|}", id, id);
            }
            """));

    /// <summary>Verifies the third and later uses of a name are each reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThirdUseIsAlsoReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int id)
                    => logger.LogInformation("{Id} {|SST2442:{Id}|} {|SST2442:{Id}|}", id, id, id);
            }
            """));

    /// <summary>Verifies distinct placeholder names are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DistinctNamesAreCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int userId, string userName)
                    => logger.LogInformation("{UserId} {UserName}", userId, userName);
            }
            """));

    /// <summary>Verifies a shared token that is not a whole name is not treated as a repeat.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SharedTokenIsNotADuplicateAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int user, string userName)
                    => logger.LogInformation("{User} {UserName}", user, userName);
            }
            """));

    /// <summary>Verifies a repeated positional placeholder is not treated as a duplicate name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RepeatedNumericPlaceholderIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(ILogger logger, int id) => logger.LogInformation("{0} and {0}", id);
            }
            """));
}
