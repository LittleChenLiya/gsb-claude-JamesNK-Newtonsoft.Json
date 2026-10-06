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
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Utilities;

namespace Newtonsoft.Json
{
    /// <summary>
    /// An explicit deserialization context in which <c>$id</c> definitions, <c>$ref</c>
    /// resolution and <c>$type</c> binding are scoped to a single <see cref="Deserialize{T}"/>
    /// call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each call to <see cref="Deserialize{T}"/> runs with a fresh
    /// <see cref="ScopedReferenceResolver"/>, so:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Two <c>$ref</c> values naming the same <c>$id</c> resolve to the same shared object instance (no duplicate copies).</description></item>
    /// <item><description>A duplicate <c>$id</c> definition is rejected with a <see cref="JsonSerializationException"/>.</description></item>
    /// <item><description>A <c>$ref</c> naming an unknown or not-yet-defined <c>$id</c> is rejected with a <see cref="JsonSerializationException"/>.</description></item>
    /// <item><description>A failed call leaves nothing behind: half-registered objects are discarded with the per-call resolver and the next call starts with no residual IDs.</description></item>
    /// </list>
    /// <para>
    /// Reference loops are supported within the scope of
    /// <see cref="PreserveReferencesHandling.Objects"/>: object-to-object cycles
    /// (for example a node's <c>Next</c> pointing back to an earlier node) round-trip correctly.
    /// Cycles that pass through collection items or constructor arguments are outside the
    /// supported scope and are diagnosed as errors.
    /// </para>
    /// <para>
    /// Type-name binding defaults to <see cref="TypeNameHandling.None"/>, so <c>$type</c>
    /// values in the JSON are ignored and no arbitrary types are loaded. If
    /// <see cref="TypeNameHandling"/> is enabled, an explicit
    /// <see cref="JsonSerializerSettings.SerializationBinder"/> (for example
    /// <see cref="KnownTypesSerializationBinder"/>) must be supplied, otherwise the scope
    /// throws <see cref="InvalidOperationException"/>.
    /// </para>
    /// <para>
    /// The default <see cref="JsonSerializer"/> and <see cref="JsonConvert"/> behavior is
    /// unchanged; this type is an opt-in wrapper.
    /// </para>
    /// </remarks>
    public sealed class JsonDeserializeScope
    {
        private readonly JsonSerializerSettings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDeserializeScope"/> class with
        /// default settings: object reference preservation, no type-name handling.
        /// </summary>
        public JsonDeserializeScope()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDeserializeScope"/> class,
        /// allowing additional settings to be configured.
        /// </summary>
        /// <param name="configure">
        /// An optional callback to adjust the <see cref="JsonSerializerSettings"/> used by
        /// every call on this scope. <see cref="JsonSerializerSettings.ReferenceResolver"/>
        /// and <see cref="JsonSerializerSettings.ReferenceResolverProvider"/> are owned by
        /// the scope and must not be set.
        /// </param>
        public JsonDeserializeScope(Action<JsonSerializerSettings>? configure)
        {
            _settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                TypeNameHandling = TypeNameHandling.None
            };

            configure?.Invoke(_settings);

#pragma warning disable CS0618 // Type or member is obsolete
            if (_settings.ReferenceResolver != null || _settings.ReferenceResolverProvider != null)
#pragma warning restore CS0618
            {
                throw new InvalidOperationException(
                    "JsonDeserializeScope owns reference resolution. Do not set ReferenceResolver or ReferenceResolverProvider; the scope creates a fresh ScopedReferenceResolver for each Deserialize call.");
            }

            if (_settings.TypeNameHandling != TypeNameHandling.None && _settings.SerializationBinder == null)
            {
                throw new InvalidOperationException(
                    "TypeNameHandling requires an explicit SerializationBinder (for example KnownTypesSerializationBinder) so that arbitrary types cannot be loaded from JSON '$type' values.");
            }
        }

        /// <summary>
        /// Deserializes the JSON in a single scoped operation.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="json">The JSON to deserialize.</param>
        /// <returns>The deserialized object.</returns>
        /// <exception cref="JsonSerializationException">
        /// Thrown when the JSON contains a duplicate <c>$id</c>, an unknown or forward
        /// <c>$ref</c>, or a <c>$type</c> rejected by the configured binder.
        /// </exception>
        [RequiresUnreferencedCode(MiscellaneousUtils.TrimWarning)]
        [RequiresDynamicCode(MiscellaneousUtils.AotWarning)]
        public T? Deserialize<T>(string json)
        {
            ValidationUtils.ArgumentNotNull(json, nameof(json));

            JsonSerializer serializer = JsonSerializer.Create(_settings);

            // A fresh resolver per call: $id/$ref state belongs to this deserialize only.
            // If the call fails, the resolver (and any half-registered objects) is discarded
            // with it; nothing is stored in external state and the next call sees no residual IDs.
            serializer.ReferenceResolver = new ScopedReferenceResolver();

            using (JsonTextReader reader = new JsonTextReader(new System.IO.StringReader(json)))
            {
                return serializer.Deserialize<T>(reader);
            }
        }
    }
}
