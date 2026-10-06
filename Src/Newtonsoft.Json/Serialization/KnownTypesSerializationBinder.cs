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
using Newtonsoft.Json.Utilities;

namespace Newtonsoft.Json.Serialization
{
    /// <summary>
    /// An <see cref="ISerializationBinder"/> that only binds <c>$type</c> names to an explicit
    /// whitelist of known types, preventing <see cref="TypeNameHandling"/> from loading
    /// arbitrary types from JSON.
    /// </summary>
    /// <remarks>
    /// Any <c>$type</c> value that does not resolve to a whitelisted type is rejected with a
    /// <see cref="JsonSerializationException"/> before the runtime attempts to load it, so a
    /// malicious type name can never trigger type loading or static constructors.
    /// </remarks>
    public sealed class KnownTypesSerializationBinder : ISerializationBinder
    {
        private readonly HashSet<Type> _knownTypes = new HashSet<Type>();
        private readonly Dictionary<string, Type> _nameToType = new Dictionary<string, Type>(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a new instance of the <see cref="KnownTypesSerializationBinder"/> class.
        /// </summary>
        /// <param name="knownTypes">The types that <c>$type</c> values are allowed to bind to.</param>
        public KnownTypesSerializationBinder(params Type[] knownTypes)
        {
            if (knownTypes != null)
            {
                foreach (Type type in knownTypes)
                {
                    AddKnownType(type);
                }
            }
        }

        /// <summary>
        /// Adds a type to the whitelist of bindable types.
        /// </summary>
        /// <param name="type">The type to allow.</param>
        public void AddKnownType(Type type)
        {
            ValidationUtils.ArgumentNotNull(type, nameof(type));

            _knownTypes.Add(type);
            // Index every $type spelling the serializer can produce or accept:
            // the simple "Namespace.TypeName, AssemblyName" form used by
            // TypeNameAssemblyFormatHandling.Simple, the full assembly-qualified
            // form, and the bare full type name.
            _nameToType[type.FullName! + ", " + type.Assembly.GetName().Name] = type;
            _nameToType[type.AssemblyQualifiedName!] = type;
            _nameToType[type.FullName!] = type;
        }

        /// <summary>
        /// Controls the binding of a serialized object to a type.
        /// </summary>
        /// <param name="assemblyName">The assembly name from the <c>$type</c> value.</param>
        /// <param name="typeName">The type name from the <c>$type</c> value.</param>
        /// <returns>The whitelisted type the name binds to.</returns>
        /// <exception cref="JsonSerializationException">
        /// Thrown when the <c>$type</c> value does not name a whitelisted type.
        /// </exception>
        public Type BindToType(string? assemblyName, string typeName)
        {
            string qualifiedName = assemblyName != null
                ? typeName + ", " + assemblyName
                : typeName;

            if (_nameToType.TryGetValue(qualifiedName, out Type? type)
                || (assemblyName == null && _nameToType.TryGetValue(typeName, out type)))
            {
                return type;
            }

            throw new JsonSerializationException(
                string.Format(CultureInfo.InvariantCulture,
                    "Type '{0}' is not in the list of known types and will not be loaded. Add the type to KnownTypesSerializationBinder to allow it.",
                    qualifiedName));
        }

        /// <summary>
        /// Controls the binding of a serialized object to a type name.
        /// </summary>
        /// <param name="serializedType">The type being serialized.</param>
        /// <param name="assemblyName">The assembly name to write.</param>
        /// <param name="typeName">The type name to write.</param>
        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            if (!_knownTypes.Contains(serializedType))
            {
                throw new JsonSerializationException(
                    string.Format(CultureInfo.InvariantCulture,
                        "Type '{0}' is not in the list of known types and will not be serialized with a type name. Add the type to KnownTypesSerializationBinder to allow it.",
                        serializedType));
            }

            assemblyName = serializedType.Assembly.FullName;
            typeName = serializedType.FullName;
        }
    }
}
