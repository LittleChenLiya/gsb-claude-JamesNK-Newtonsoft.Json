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
    /// Implemented by an <see cref="IContractResolver"/> to observe, per serialization call, the
    /// properties the serializer skipped because they were ignored, failed <c>ShouldSerialize</c>,
    /// or matched their default value.
    /// </summary>
    /// <remarks>
    /// Notifications replay decisions the serializer has already made. Implementations are never
    /// asked to re-evaluate a property getter or a <c>ShouldSerialize</c> method, and exceptions
    /// thrown by getters or <c>ShouldSerialize</c> methods are not intercepted by this interface.
    /// </remarks>
    public interface ISerializationIgnoreTracker
    {
        /// <summary>
        /// Called once at the start of each top-level serialization call so that records from
        /// previous calls can be discarded and each call is recorded independently.
        /// </summary>
        void OnSerializationStarting();

        /// <summary>
        /// Called when the serializer skips a property that would otherwise have been written.
        /// </summary>
        /// <param name="property">The contract property that was skipped.</param>
        /// <param name="reason">Why the property was skipped.</param>
        void RecordIgnoredProperty(JsonProperty property, SerializationIgnoreReason reason);
    }
}
