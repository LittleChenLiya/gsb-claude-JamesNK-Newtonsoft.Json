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
using System.Globalization;
using Newtonsoft.Json.Utilities;

namespace Newtonsoft.Json.Serialization
{
    /// <summary>
    /// Records that a property was not written during serialization, captured at the exact
    /// point the serializer made the decision. Explanations are recorded from the serializer's
    /// own execution results; no getter or ShouldSerialize predicate is invoked to produce them.
    /// </summary>
    public sealed class JsonIgnoreExplanation
    {
        /// <summary>
        /// Gets the resolved name of the property that was not serialized.
        /// </summary>
        public string? PropertyName { get; }

        /// <summary>
        /// Gets the type that declares the property that was not serialized.
        /// </summary>
        public Type? DeclaringType { get; }

        /// <summary>
        /// Gets the reason the property was not serialized.
        /// </summary>
        public JsonIgnoreReason Reason { get; }

        /// <summary>
        /// Gets the path of the object being serialized when the property was skipped.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonIgnoreExplanation"/> class.
        /// </summary>
        public JsonIgnoreExplanation(string? propertyName, Type? declaringType, JsonIgnoreReason reason, string path)
        {
            PropertyName = propertyName;
            DeclaringType = declaringType;
            Reason = reason;
            Path = path;
        }

        /// <summary>
        /// Returns a string that describes the explanation.
        /// </summary>
        public override string ToString()
        {
            return JsonPosition.FormatMessage(null, Path, "Property '{0}' on {1} was not serialized. Reason: {2}.".FormatWith(
                CultureInfo.InvariantCulture, PropertyName, DeclaringType, Reason));
        }
    }
}
