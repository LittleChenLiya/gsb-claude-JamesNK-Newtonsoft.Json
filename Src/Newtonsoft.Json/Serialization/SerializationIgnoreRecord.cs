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

namespace Newtonsoft.Json.Serialization
{
    /// <summary>
    /// Describes a single property that was not written to the JSON output during serialization.
    /// Records are captured from the decisions the serializer actually made; creating them does
    /// not invoke property getters or <c>ShouldSerialize</c> methods again.
    /// </summary>
    public sealed class SerializationIgnoreRecord
    {
        /// <summary>
        /// Gets the JSON property name that was skipped.
        /// </summary>
        public string? PropertyName { get; }

        /// <summary>
        /// Gets the underlying member name of the skipped property.
        /// </summary>
        public string? UnderlyingName { get; }

        /// <summary>
        /// Gets the type that declares the skipped property.
        /// </summary>
        public Type? DeclaringType { get; }

        /// <summary>
        /// Gets the reason the property was not serialized.
        /// </summary>
        public SerializationIgnoreReason Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializationIgnoreRecord"/> class.
        /// </summary>
        public SerializationIgnoreRecord(string? propertyName, string? underlyingName, Type? declaringType, SerializationIgnoreReason reason)
        {
            PropertyName = propertyName;
            UnderlyingName = underlyingName;
            DeclaringType = declaringType;
            Reason = reason;
        }

        /// <summary>
        /// Returns a string that represents the current record.
        /// </summary>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1} was not serialized ({2})",
                DeclaringType?.Name ?? "<unknown>",
                PropertyName ?? UnderlyingName ?? "<unknown>",
                Reason);
        }
    }
}
