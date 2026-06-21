// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The supported instance-member qualification styles.</summary>
internal enum InstanceMemberQualification
{
    /// <summary>Instance members are read without a <c>this.</c> prefix.</summary>
    OmitThis,

    /// <summary>Instance members are read through a <c>this.</c> prefix.</summary>
    RequireThis
}
