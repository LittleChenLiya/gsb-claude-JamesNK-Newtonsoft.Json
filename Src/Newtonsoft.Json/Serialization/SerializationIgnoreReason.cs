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

namespace Newtonsoft.Json.Serialization
{
    /// <summary>
    /// Explains why a property was not written to the JSON output during serialization.
    /// </summary>
    public enum SerializationIgnoreReason
    {
        /// <summary>
        /// The property is marked as ignored, e.g. with <see cref="JsonIgnoreAttribute"/>.
        /// </summary>
        Ignored = 0,

        /// <summary>
        /// The property's <c>ShouldSerialize</c> method returned <c>false</c>.
        /// </summary>
        ShouldSerialize = 1,

        /// <summary>
        /// The property value matched its default value and <see cref="DefaultValueHandling.Ignore"/> applied.
        /// </summary>
        DefaultValue = 2
    }
}
