// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Polyfill attributes so that modern C# features (init, required, record struct)
// compile when targeting netstandard2.0.

#pragma warning disable MA0048 // File name must match type name — single polyfill file by design

#if NETSTANDARD2_0 || NETSTANDARD2_1 || !NET5_0_OR_GREATER

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    /// <summary>
    /// Reserved to be used by the compiler for tracking metadata.
    /// This class should not be used by developers in source code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit;

    /// <summary>
    /// Indicates that compiler support for a particular feature is required for the location where this attribute is applied.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;

        public string FeatureName { get; }

        public bool IsOptional { get; init; }
    }

    /// <summary>
    /// Specifies that a type has required members or that a member is required.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute;
}

// ReSharper disable once CheckNamespace
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Specifies that this constructor sets all required members for the current type,
    /// and callers do not need to set any required members themselves.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute;
}

#endif
