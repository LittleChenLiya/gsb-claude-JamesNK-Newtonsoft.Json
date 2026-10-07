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

namespace Newtonsoft.Json
{
    /// <summary>
    /// Represents a view over the source text of a JSON date token, exposing the time zone offset
    /// exactly as it was written in the input rather than only the converted date value.
    /// </summary>
    public sealed class JsonDateTokenSource
    {
        /// <summary>
        /// Gets the source text of the date token as it appeared in the JSON input.
        /// </summary>
        /// <value>The source text of the date token.</value>
        public string Text { get; }

        /// <summary>
        /// Gets a value indicating whether the source text contained an explicit time zone offset,
        /// either <c>Z</c> or a numeric offset such as <c>+08:00</c>.
        /// </summary>
        /// <value><c>true</c> if the source text contained an explicit time zone offset; otherwise, <c>false</c>.</value>
        public bool HasExplicitOffset => Offset != null;

        /// <summary>
        /// Gets the explicit time zone offset read from the source text.
        /// <c>Z</c> is reported as <see cref="TimeSpan.Zero"/>.
        /// The value is <c>null</c> when the source text did not specify an offset.
        /// </summary>
        /// <value>The explicit time zone offset from the source text, or <c>null</c> if none was specified.</value>
        public TimeSpan? Offset { get; }

        internal JsonDateTokenSource(string text, TimeSpan? offset)
        {
            Text = text;
            Offset = offset;
        }

        /// <summary>
        /// Returns the source text of the date token.
        /// </summary>
        /// <returns>The source text of the date token.</returns>
        public override string ToString()
        {
            return Text;
        }
    }
}
