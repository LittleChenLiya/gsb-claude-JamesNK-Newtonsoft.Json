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
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Utilities;

namespace Newtonsoft.Json.Serialization
{
    /// <summary>
    /// An <see cref="ISerializationBinder"/> that only binds <c>$type</c> names to an explicit
    /// allow-list of <see cref="Type"/> instances.
    /// <para>
    /// Unlike <see cref="DefaultSerializationBinder"/>, this binder never calls
    /// <c>Type.GetType</c>/<c>Assembly.Load</c> for names coming from the JSON payload: a type name
    /// that is not on the allow-list is rejected with a <see cref="JsonSerializationException"/>
    /// without loading anything, so a malicious <c>$type</c> value cannot trigger arbitrary type
    /// loading when <see cref="JsonSerializer.TypeNameHandling"/> is enabled.
    /// </para>
    /// </summary>
    public class AllowedTypesSerializationBinder : ISerializationBinder
    {
        private readonly List<Type> _allowedTypes;

        /// <summary>
        /// Initializes a new instance of the <see cref="AllowedTypesSerializationBinder"/> class
        /// with the specified allow-list of types.
        /// </summary>
        /// <param name="allowedTypes">The types that <c>$type</c> names are allowed to bind to.</param>
        public AllowedTypesSerializationBinder(IEnumerable<Type> allowedTypes)
        {
            ValidationUtils.ArgumentNotNull(allowedTypes, nameof(allowedTypes));

            _allowedTypes = new List<Type>(allowedTypes);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AllowedTypesSerializationBinder"/> class
        /// with the specified allow-list of types.
        /// </summary>
        /// <param name="allowedTypes">The types that <c>$type</c> names are allowed to bind to.</param>
        public AllowedTypesSerializationBinder(params Type[] allowedTypes)
            : this((IEnumerable<Type>)allowedTypes)
        {
        }

        /// <summary>
        /// Binds a serialized type name to a <see cref="Type"/>, but only if the type is on the allow-list.
        /// No assemblies are loaded and no types are resolved by name while binding.
        /// </summary>
        /// <param name="assemblyName">Specifies the assembly name of the serialized object, or <c>null</c>.</param>
        /// <param name="typeName">Specifies the type name of the serialized object.</param>
        /// <returns>The allowed type the name binds to.</returns>
        /// <exception cref="JsonSerializationException">The type name is not on the allow-list.</exception>
        public Type BindToType(string? assemblyName, string typeName)
        {
            for (int i = 0; i < _allowedTypes.Count; i++)
            {
                Type allowedType = _allowedTypes[i];

                if (string.Equals(allowedType.FullName, typeName, StringComparison.Ordinal)
                    && (assemblyName == null || MatchesAssemblyName(allowedType, assemblyName)))
                {
                    return allowedType;
                }
            }

            throw new JsonSerializationException(
                "Type '{0}' is not in the list of allowed types and will not be loaded.".FormatWith(CultureInfo.InvariantCulture,
                    assemblyName != null ? typeName + ", " + assemblyName : typeName));
        }

        private static bool MatchesAssemblyName(Type allowedType, string assemblyName)
        {
            // accept either the simple assembly name or the full display name
            string allowedAssemblyName = allowedType.Assembly.FullName!;
            return string.Equals(allowedAssemblyName, assemblyName, StringComparison.Ordinal)
                || string.Equals(new global::System.Reflection.AssemblyName(allowedAssemblyName).Name, assemblyName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Binds a type to the name that will be written to <c>$type</c>.
        /// </summary>
        /// <param name="serializedType">The type being serialized.</param>
        /// <param name="assemblyName">The assembly name written to the payload.</param>
        /// <param name="typeName">The type name written to the payload.</param>
        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            assemblyName = serializedType.Assembly.FullName;
            typeName = serializedType.FullName;
        }
    }
}
