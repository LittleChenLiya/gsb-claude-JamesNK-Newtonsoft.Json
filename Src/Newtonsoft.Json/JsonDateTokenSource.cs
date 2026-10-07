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
    /// A source view of a JSON date token. Captures the original text the date was parsed from
    /// and the explicit time zone offset written in that text, so the input offset is preserved
    /// even when the parsed value has been converted to a <see cref="DateTime"/>.
    /// </summary>
    /// <remarks>
    /// The view is an independent snapshot: the text is copied when the token is read and is not
    /// affected by the reader advancing or its internal buffer being reused.
    /// </remarks>
    public readonly struct JsonDateTokenSource
    {
        private readonly string _text;
        private readonly TimeSpan? _offset;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDateTokenSource"/> struct.
        /// </summary>
        /// <param name="text">The original text the date token was parsed from.</param>
        /// <param name="offset">
        /// The explicit time zone offset written in the source text,
        /// or <c>null</c> when the text contains no zone designator.
        /// </param>
        public JsonDateTokenSource(string text, TimeSpan? offset)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _offset = offset;
        }

        /// <summary>
        /// Gets the original text the date token was parsed from.
        /// </summary>
        public string Text => _text;

        /// <summary>
        /// Gets the explicit time zone offset written in the source text.
        /// A <c>Z</c> designator maps to <see cref="TimeSpan.Zero"/>.
        /// <c>null</c> when the source text contains no zone designator.
        /// </summary>
        public TimeSpan? Offset => _offset;

        /// <summary>
        /// Gets a value indicating whether the source text contains an explicit time zone offset.
        /// </summary>
        public bool HasExplicitOffset => _offset.HasValue;

        /// <summary>
        /// Returns the original text the date token was parsed from.
        /// </summary>
        public override string ToString() => _text;
    }
}
