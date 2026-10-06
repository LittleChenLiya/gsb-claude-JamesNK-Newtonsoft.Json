#region License
// Copyright (c) 2007 James Newton-King
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Utilities;

namespace Newtonsoft.Json.Serialization
{
    /// <summary>
    /// Resolves <c>$id</c>/<c>$ref</c> object references with an explicit scope that belongs to the
    /// current Serialize/Deserialize call.
    /// <para>
    /// Unlike <see cref="DefaultReferenceResolver"/>, which keeps its mappings on a serializer-wide
    /// context, this resolver stores every <c>$id</c> definition, <c>$ref</c> binding and generated
    /// reference on the per-call internal serializer context. As a result:
    /// <list type="bullet">
    /// <item><description>A shared object is resolved to the same instance; it is never copied into two objects.</description></item>
    /// <item><description>A failed deserialization never leaves half-populated objects in the resolver,
    /// because the failed call scope is discarded together with its internal serializer.</description></item>
    /// <item><description>The next call starts with an empty scope, so Ids from a previous call never leak
    /// into a later one, even when the same <see cref="ScopedReferenceResolver"/> instance is reused.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Diagnostics follow an explicit policy: a duplicate <c>$id</c> definition inside one call and a
    /// <c>$ref</c> that cannot be resolved (including forward references to an <c>$id</c> defined later
    /// in the document, which are not supported) both raise a <see cref="JsonSerializationException"/>
    /// with a descriptive message instead of being silently overwritten or resolved to <c>null</c>.
    /// </para>
    /// </summary>
    public sealed class ScopedReferenceResolver : IReferenceResolver
    {
        private sealed class ReferenceScope
        {
            public readonly Dictionary<string, object> ObjectsById = new Dictionary<string, object>(StringComparer.Ordinal);
            public readonly Dictionary<object, string> IdsByObject = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
            public int ReferenceCount;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            private ReferenceEqualityComparer()
            {
            }

            bool IEqualityComparer<object>.Equals(object? x, object? y)
            {
                return ReferenceEquals(x, y);
            }

            int IEqualityComparer<object>.GetHashCode(object obj)
            {
                // put objects in a bucket based on their reference
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }

        private static ReferenceScope GetScope(object context)
        {
            if (!(context is JsonSerializerInternalBase internalSerializer))
            {
                if (context is JsonSerializerProxy proxy)
                {
                    internalSerializer = proxy.GetInternalSerializer();
                }
                else
                {
                    throw new JsonException("The ScopedReferenceResolver can only be used internally.");
                }
            }

            if (!(internalSerializer.ScopedReferenceResolverState is ReferenceScope scope))
            {
                scope = new ReferenceScope();
                internalSerializer.ScopedReferenceResolverState = scope;
            }

            return scope;
        }

        /// <summary>
        /// Resolves a reference to its object within the scope of the current call.
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="reference">The reference to resolve.</param>
        /// <returns>The object that was resolved from the reference.</returns>
        /// <exception cref="JsonSerializationException">
        /// The reference has not been defined in the current scope. Forward references to an
        /// <c>$id</c> defined later in the document are not supported.
        /// </exception>
        public object ResolveReference(object context, string reference)
        {
            if (!GetScope(context).ObjectsById.TryGetValue(reference, out object? value))
            {
                throw new JsonSerializationException(
                    "Could not resolve JSON reference '{0}'. The reference has not been defined in the current deserialization scope. Forward references to an Id defined later in the document are not supported.".FormatWith(CultureInfo.InvariantCulture, reference));
            }

            return value;
        }

        /// <summary>
        /// Gets the reference for the specified object within the scope of the current call.
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="value">The object to get a reference for.</param>
        /// <returns>The reference to the object.</returns>
        public string GetReference(object context, object value)
        {
            ReferenceScope scope = GetScope(context);

            if (!scope.IdsByObject.TryGetValue(value, out string? reference))
            {
                scope.ReferenceCount++;
                reference = scope.ReferenceCount.ToString(CultureInfo.InvariantCulture);
                scope.IdsByObject[value] = reference;
                scope.ObjectsById[reference] = value;
            }

            return reference;
        }

        /// <summary>
        /// Determines whether the specified object is referenced within the scope of the current call.
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="value">The object to test for a reference.</param>
        /// <returns>
        /// 	<c>true</c> if the specified object is referenced; otherwise, <c>false</c>.
        /// </returns>
        public bool IsReferenced(object context, object value)
        {
            return GetScope(context).IdsByObject.ContainsKey(value);
        }

        /// <summary>
        /// Adds a reference to the specified object within the scope of the current call.
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="reference">The reference.</param>
        /// <param name="value">The object to reference.</param>
        /// <exception cref="JsonSerializationException">
        /// The same <c>$id</c> has already been defined in the current scope.
        /// </exception>
        public void AddReference(object context, string reference, object value)
        {
            ReferenceScope scope = GetScope(context);

            if (scope.ObjectsById.ContainsKey(reference))
            {
                throw new JsonSerializationException(
                    "Duplicate object Id '{0}'. The same Id has already been defined in the current deserialization scope.".FormatWith(CultureInfo.InvariantCulture, reference));
            }

            scope.ObjectsById.Add(reference, value);
            scope.IdsByObject[value] = reference;
        }
    }
}
