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

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Utilities;

namespace Newtonsoft.Json.Serialization
{
    /// <summary>
    /// A <see cref="DefaultContractResolver"/> that also acts as an interpretation view over the
    /// properties skipped during the most recent serialization call.
    /// </summary>
    /// <remarks>
    /// Assign an instance to <see cref="JsonSerializerSettings.ContractResolver"/> (or
    /// <see cref="JsonSerializer.ContractResolver"/>) and serialize as usual. The JSON output is
    /// unchanged; after the call, <see cref="IgnoredProperties"/> explains, for each skipped
    /// property, whether it was dropped because of <c>JsonIgnore</c>, <c>ShouldSerialize</c>, or
    /// <see cref="DefaultValueHandling.Ignore"/>. Records are built from the decisions the
    /// serializer actually made - getters and <c>ShouldSerialize</c> methods are evaluated exactly
    /// once by the serializer itself - and are reset at the start of every serialization call so
    /// each call is recorded independently.
    /// </remarks>
    /// <example>
    /// <code>
    /// var resolver = new IgnoreTrackingContractResolver();
    /// string json = JsonConvert.SerializeObject(model, new JsonSerializerSettings { ContractResolver = resolver });
    /// foreach (SerializationIgnoreRecord record in resolver.IgnoredProperties)
    /// {
    ///     Console.WriteLine(record);
    /// }
    /// </code>
    /// </example>
    [RequiresUnreferencedCode(MiscellaneousUtils.TrimWarning)]
    [RequiresDynamicCode(MiscellaneousUtils.AotWarning)]
    public class IgnoreTrackingContractResolver : DefaultContractResolver, ISerializationIgnoreTracker
    {
        private readonly List<SerializationIgnoreRecord> _ignoredProperties = new List<SerializationIgnoreRecord>();

        /// <summary>
        /// Gets the properties skipped by the most recent serialization call, in the order the
        /// serializer made those decisions. The list is reset at the start of each call.
        /// </summary>
        public IList<SerializationIgnoreRecord> IgnoredProperties => _ignoredProperties.AsReadOnly();

        /// <summary>
        /// Removes all recorded skips without waiting for the next serialization call.
        /// </summary>
        public void Clear()
        {
            _ignoredProperties.Clear();
        }

        void ISerializationIgnoreTracker.OnSerializationStarting()
        {
            _ignoredProperties.Clear();
        }

        void ISerializationIgnoreTracker.RecordIgnoredProperty(JsonProperty property, SerializationIgnoreReason reason)
        {
            _ignoredProperties.Add(new SerializationIgnoreRecord(
                property.PropertyName,
                property.UnderlyingName,
                property.DeclaringType,
                reason));
        }
    }
}
