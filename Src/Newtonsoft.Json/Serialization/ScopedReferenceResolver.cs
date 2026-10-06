#region License
// Copyright (c) 2007 James Newton-King
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software
// is furnished to do so, subject to the following conditions:
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

namespace Newtonsoft.Json.Serialization
{
    /// <summary>
    /// An explicit, single-use <see cref="IReferenceResolver"/> that scopes <c>$id</c> definitions
    /// and <c>$ref</c> resolution to one deserialize operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="DefaultReferenceResolver"/>, which silently overwrites duplicate
    /// <c>$id</c> values and returns <c>null</c> for unknown references, this resolver applies
    /// an explicit diagnostics policy:
    /// </para>
    /// <list type="bullet">
    /// <item><description>A duplicate <c>$id</c> definition is rejected with a <see cref="JsonSerializationException"/>.</description></item>
    /// <item><description>A <c>$ref</c> that names an unknown (or not-yet-defined, i.e. forward) <c>$id</c> is rejected with a <see cref="JsonSerializationException"/>.</description></item>
    /// </list>
    /// <para>
    /// Create a new instance per deserialize call. Because the mapping table lives only on the
    /// instance, a failed call cannot leak half-registered objects into shared state and the next
    /// call starts with no residual IDs.
    /// </para>
    /// </remarks>
    public sealed class ScopedReferenceResolver : IReferenceResolver
    {
        private readonly Dictionary<string, object> _idToObject = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<object, string> _objectToId = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        private int _referenceCount;

        /// <summary>
        /// Gets the number of <c>$id</c> definitions registered with this resolver.
        /// </summary>
        public int Count => _idToObject.Count;

        /// <summary>
        /// Resolves a <c>$ref</c> value to its previously defined object.
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="reference">The <c>$ref</c> value to resolve.</param>
        /// <returns>The object registered under the reference.</returns>
        /// <exception cref="JsonSerializationException">
        /// Thrown when <paramref name="reference"/> has not been defined by a preceding <c>$id</c>.
        /// </exception>
        public object ResolveReference(object context, string reference)
        {
            if (_idToObject.TryGetValue(reference, out object? value))
            {
                return value;
            }

            throw new JsonSerializationException(
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot resolve reference '{0}'. The reference has not been defined by a preceding '{1}' property in this deserialize operation. Forward references to objects defined later in the JSON are not supported.",
                    reference, "$id"));
        }

        /// <summary>
        /// Gets the reference ID for the specified object, assigning a new sequential ID if the
        /// object has not been referenced before.
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="value">The object to get a reference for.</param>
        /// <returns>The reference ID for the object.</returns>
        public string GetReference(object context, object value)
        {
            if (!_objectToId.TryGetValue(value, out string? reference))
            {
                _referenceCount++;
                reference = _referenceCount.ToString(CultureInfo.InvariantCulture);
                _objectToId[value] = reference;
                _idToObject[reference] = value;
            }

            return reference;
        }

        /// <summary>
        /// Determines whether the specified object has been assigned a reference ID.
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="value">The object to test for a reference.</param>
        /// <returns><c>true</c> if the object has been referenced; otherwise, <c>false</c>.</returns>
        public bool IsReferenced(object context, object value)
        {
            return _objectToId.ContainsKey(value);
        }

        /// <summary>
        /// Registers a <c>$id</c> definition for the specified object.
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="reference">The <c>$id</c> value.</param>
        /// <param name="value">The object being defined.</param>
        /// <exception cref="JsonSerializationException">
        /// Thrown when <paramref name="reference"/> has already been defined in this deserialize operation.
        /// </exception>
        public void AddReference(object context, string reference, object value)
        {
            if (_idToObject.ContainsKey(reference))
            {
                throw new JsonSerializationException(
                    string.Format(CultureInfo.InvariantCulture,
                        "Duplicate reference Id '{0}'. A '{1}' property with this value has already been defined in this deserialize operation.",
                        reference, "$id"));
            }

            _idToObject.Add(reference, value);
            _objectToId[value] = reference;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object? x, object? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
