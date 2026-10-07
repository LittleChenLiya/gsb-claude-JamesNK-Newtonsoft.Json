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
using System.ComponentModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Serialization;
#if DNXCORE50
using Xunit;
using Test = Xunit.FactAttribute;
using Assert = Newtonsoft.Json.Tests.XUnitAssert;
#else
using NUnit.Framework;
#endif

namespace Newtonsoft.Json.Tests.Serialization
{
    [TestFixture]
    public class JsonIgnoreExplanationTests : TestFixtureBase
    {
        public class CountingGetterModel
        {
            internal int VisibleGetterCount { get; private set; }
            internal int DefaultValueGetterCount { get; private set; }
            internal int ConditionalGetterCount { get; private set; }
            internal int HiddenGetterCount { get; private set; }
            internal int ShouldSerializeCallCount { get; private set; }

            public string Visible
            {
                get
                {
                    VisibleGetterCount++;
                    return "visible";
                }
            }

            [JsonIgnore]
            public string Hidden
            {
                get
                {
                    HiddenGetterCount++;
                    return "hidden";
                }
            }

            public string Conditional
            {
                get
                {
                    ConditionalGetterCount++;
                    return "conditional";
                }
            }

            public bool ShouldSerializeConditional()
            {
                ShouldSerializeCallCount++;
                return false;
            }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            [DefaultValue(0)]
            public int Count
            {
                get
                {
                    DefaultValueGetterCount++;
                    return 0;
                }
            }
        }

        public class FailingGetterModel
        {
            public string Boom
            {
                get { throw new InvalidOperationException("Getter failed as designed."); }
            }
        }

        private static JsonSerializer CreateSerializer(JsonIgnoreExplanationCollector collector)
        {
            return JsonSerializer.Create(new JsonSerializerSettings
            {
                IgnoreExplanationSink = collector
            });
        }

        private static string Serialize(JsonSerializer serializer, object value)
        {
            StringWriter sw = new StringWriter();
            serializer.Serialize(sw, value);
            return sw.ToString();
        }

        [Test]
        public void ExplainsAllThreeIgnoreReasons()
        {
            JsonIgnoreExplanationCollector collector = new JsonIgnoreExplanationCollector();
            CountingGetterModel model = new CountingGetterModel();

            string json = Serialize(CreateSerializer(collector), model);

            // original JSON output is preserved
            Assert.AreEqual(@"{""Visible"":""visible""}", json);

            // one record set for the single serialize call, with all three reasons
            Assert.AreEqual(1, collector.Calls.Count);
            IList<JsonIgnoreExplanation> explanations = collector.Calls[0].ToList();
            Assert.AreEqual(3, explanations.Count);

            Assert.AreEqual("Hidden", explanations[0].PropertyName);
            Assert.AreEqual(JsonIgnoreReason.Ignored, explanations[0].Reason);
            Assert.AreEqual(typeof(CountingGetterModel), explanations[0].DeclaringType);
            Assert.AreEqual("", explanations[0].Path);

            Assert.AreEqual("Conditional", explanations[1].PropertyName);
            Assert.AreEqual(JsonIgnoreReason.ShouldSerialize, explanations[1].Reason);

            Assert.AreEqual("Count", explanations[2].PropertyName);
            Assert.AreEqual(JsonIgnoreReason.DefaultValue, explanations[2].Reason);

            // single evaluation: the explainer did not invoke anything extra
            Assert.AreEqual(1, model.VisibleGetterCount);
            Assert.AreEqual(1, model.DefaultValueGetterCount);
            Assert.AreEqual(1, model.ShouldSerializeCallCount);
            Assert.AreEqual(0, model.ConditionalGetterCount);
            Assert.AreEqual(0, model.HiddenGetterCount);
        }

        [Test]
        public void EachSerializeCallRecordsIndependently()
        {
            JsonIgnoreExplanationCollector collector = new JsonIgnoreExplanationCollector();
            JsonSerializer serializer = CreateSerializer(collector);

            string json1 = Serialize(serializer, new CountingGetterModel());
            string json2 = Serialize(serializer, new CountingGetterModel());

            Assert.AreEqual(json1, json2);
            Assert.AreEqual(2, collector.Calls.Count);
            Assert.AreEqual(3, collector.Calls[0].Count);
            Assert.AreEqual(3, collector.Calls[1].Count);
            Assert.AreSame(collector.Calls[1], collector.LastCall);

            // records from the two calls are independent objects
            Assert.AreNotSame(collector.Calls[0], collector.Calls[1]);
            Assert.AreEqual(
                collector.Calls[0].Select(e => e.PropertyName + ":" + e.Reason).ToArray(),
                collector.Calls[1].Select(e => e.PropertyName + ":" + e.Reason).ToArray());
        }

        [Test]
        public void SinkDoesNotChangeJsonOutput()
        {
            CountingGetterModel model = new CountingGetterModel();

            string withoutSink = JsonConvert.SerializeObject(model);

            JsonIgnoreExplanationCollector collector = new JsonIgnoreExplanationCollector();
            string withSink = JsonConvert.SerializeObject(model, new JsonSerializerSettings
            {
                IgnoreExplanationSink = collector
            });

            Assert.AreEqual(withoutSink, withSink);
            Assert.AreEqual(1, collector.Calls.Count);
            Assert.AreEqual(3, collector.Calls[0].Count);
        }

        [Test]
        public void NoSinkKeepsSingleEvaluation()
        {
            CountingGetterModel model = new CountingGetterModel();

            string json = JsonConvert.SerializeObject(model);

            Assert.AreEqual(@"{""Visible"":""visible""}", json);
            Assert.AreEqual(1, model.VisibleGetterCount);
            Assert.AreEqual(1, model.DefaultValueGetterCount);
            Assert.AreEqual(1, model.ShouldSerializeCallCount);
        }

        [Test]
        public void FailingGetterCauseIsNotSwallowed()
        {
            JsonIgnoreExplanationCollector collector = new JsonIgnoreExplanationCollector();

            JsonSerializationException ex = ExceptionAssert.Throws<JsonSerializationException>(
                () => Serialize(CreateSerializer(collector), new FailingGetterModel()));

            // the original getter failure is preserved as the cause
            Assert.IsNotNull(ex.InnerException);
            Assert.AreEqual(typeof(InvalidOperationException), ex.InnerException.GetType());
            Assert.AreEqual("Getter failed as designed.", ex.InnerException.Message);

            // the failed call still completed its record set (empty: no ignore rule fired)
            Assert.AreEqual(1, collector.Calls.Count);
            Assert.AreEqual(0, collector.Calls[0].Count);
        }

        [Test]
        public void ExplanationToStringDescribesReason()
        {
            JsonIgnoreExplanationCollector collector = new JsonIgnoreExplanationCollector();
            Serialize(CreateSerializer(collector), new CountingGetterModel());

            Assert.IsTrue(collector.Calls[0][0].ToString().Contains("Hidden"));
            Assert.IsTrue(collector.Calls[0][0].ToString().Contains("Ignored"));
            Assert.IsTrue(collector.Calls[0][1].ToString().Contains("ShouldSerialize"));
            Assert.IsTrue(collector.Calls[0][2].ToString().Contains("DefaultValue"));
        }
    }
}
