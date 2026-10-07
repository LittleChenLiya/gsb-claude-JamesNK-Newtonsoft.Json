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
using Newtonsoft.Json.Utilities;

namespace Newtonsoft.Json.Serialization
{
    /// <summary>
    /// Collects <see cref="JsonIgnoreExplanation"/> records produced during serialization.
    /// Each top-level serialize call gets its own independent record set, exposed through
    /// <see cref="Calls"/>; nested serialize calls made by converters contribute to the
    /// enclosing call's record set.
    /// </summary>
    public sealed class JsonIgnoreExplanationCollector : IJsonIgnoreExplanationSink
    {
        private readonly List<IList<JsonIgnoreExplanation>> _calls = new List<IList<JsonIgnoreExplanation>>();
        private List<JsonIgnoreExplanation>? _current;
        private int _depth;

        /// <summary>
        /// Gets the recorded explanations, one entry per top-level serialize call, in call order.
        /// </summary>
        public IList<IList<JsonIgnoreExplanation>> Calls => _calls;

        /// <summary>
        /// Gets the explanations recorded by the most recent serialize call,
        /// or an empty list if no call has completed.
        /// </summary>
        public IList<JsonIgnoreExplanation> LastCall =>
            _calls.Count > 0 ? _calls[_calls.Count - 1] : CollectionUtils.ArrayEmpty<JsonIgnoreExplanation>();

        /// <summary>
        /// Removes all recorded explanations.
        /// </summary>
        public void Clear()
        {
            _calls.Clear();
            _current = null;
            _depth = 0;
        }

        internal void BeginCall()
        {
            _depth++;
            if (_depth == 1)
            {
                _current = new List<JsonIgnoreExplanation>();
            }
        }

        internal void EndCall()
        {
            _depth--;
            if (_depth == 0 && _current != null)
            {
                _calls.Add(_current);
                _current = null;
            }
        }

        /// <summary>
        /// Records an explanation for a property that was not serialized.
        /// </summary>
        /// <param name="explanation">The explanation of why the property was not serialized.</param>
        public void OnPropertyIgnored(JsonIgnoreExplanation explanation)
        {
            if (_current != null)
            {
                _current.Add(explanation);
            }
            else
            {
                // recorded outside of a BeginCall/EndCall pair; keep it as its own call record
                _calls.Add(new List<JsonIgnoreExplanation> { explanation });
            }
        }
    }
}
