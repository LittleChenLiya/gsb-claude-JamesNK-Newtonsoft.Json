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
    public class SerializationIgnoreTrackingTests : TestFixtureBase
    {
        public class CountingIgnoreModel
        {
            private int _ignoredGetCount;
            private int _hiddenGetCount;
            private int _hiddenShouldSerializeCount;
            private int _defaultGetCount;
            private int _nullGetCount;
            private int _writtenGetCount;

            public int IgnoredGetCount() { return _ignoredGetCount; }
            public int HiddenGetCount() { return _hiddenGetCount; }
            public int HiddenShouldSerializeCount() { return _hiddenShouldSerializeCount; }
            public int DefaultGetCount() { return _defaultGetCount; }
            public int NullGetCount() { return _nullGetCount; }
            public int WrittenGetCount() { return _writtenGetCount; }








            [JsonIgnore]
            public string IgnoredProp
            {
                get { _ignoredGetCount++; return "ignored"; }
            }

            public string HiddenProp
            {
                get { _hiddenGetCount++; return "hidden"; }
            }

            public bool ShouldSerializeHiddenProp()
            {
                _hiddenShouldSerializeCount++;
                return false;
            }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int DefaultProp
            {
                get { _defaultGetCount++; return 0; }
            }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string NullProp
            {
                get { _nullGetCount++; return null; }
            }

            public string WrittenProp
            {
                get { _writtenGetCount++; return "written"; }
            }
        }

        public class SecondCountingModel
        {
            private int _versionGetCount;
            private int _nameGetCount;
            private int _nameShouldSerializeCount;
            private int _statusGetCount;

            public int VersionGetCount() { return _versionGetCount; }
            public int NameGetCount() { return _nameGetCount; }
            public int NameShouldSerializeCount() { return _nameShouldSerializeCount; }
            public int StatusGetCount() { return _statusGetCount; }






            [JsonIgnore]
            public int Version
            {
                get { _versionGetCount++; return 3; }
            }

            public string Name
            {
                get { _nameGetCount++; return "second"; }
            }

            public bool ShouldSerializeName()
            {
                _nameShouldSerializeCount++;
                return false;
            }

            [DefaultValue("n/a")]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string Status
            {
                get { _statusGetCount++; return "n/a"; }
            }
        }

        public class ThrowingGetterModel
        {
            public string Ok
            {
                get { return "ok"; }
            }

            public string Boom
            {
                get { throw new InvalidOperationException("getter failed", new ApplicationException("root cause")); }
            }
        }

        private static IList<Exception> Flatten(Exception ex)
        {
            List<Exception> chain = new List<Exception>();
            for (Exception current = ex; current != null; current = current.InnerException)
            {
                chain.Add(current);
            }
            return chain;
        }

        [Test]
        public void IgnoredPropertiesAreRecordedWithReasons()
        {
            IgnoreTrackingContractResolver resolver = new IgnoreTrackingContractResolver();
            CountingIgnoreModel model = new CountingIgnoreModel();

            string json = JsonConvert.SerializeObject(model, new JsonSerializerSettings { ContractResolver = resolver });

            // JSON output only contains the property that was actually written
            Assert.AreEqual(@"{""WrittenProp"":""written""}", json);

            // three ignore reasons recorded from the real serialization run
            Assert.AreEqual(3, resolver.IgnoredProperties.Count);
            Dictionary<string, SerializationIgnoreReason> byName = resolver.IgnoredProperties
                .ToDictionary(r => r.PropertyName, r => r.Reason);
            Assert.AreEqual(SerializationIgnoreReason.Ignored, byName["IgnoredProp"]);
            Assert.AreEqual(SerializationIgnoreReason.ShouldSerialize, byName["HiddenProp"]);
            Assert.AreEqual(SerializationIgnoreReason.DefaultValue, byName["DefaultProp"]);

            // null-value skips are not one of the three tracked reasons
            Assert.IsFalse(byName.ContainsKey("NullProp"));

            // records carry the interpretation view details
            SerializationIgnoreRecord ignored = resolver.IgnoredProperties.Single(r => r.PropertyName == "IgnoredProp");
            Assert.AreEqual("IgnoredProp", ignored.UnderlyingName);
            Assert.AreEqual(typeof(CountingIgnoreModel), ignored.DeclaringType);

            // single evaluation: nothing was re-invoked to build the records
            Assert.AreEqual(0, model.IgnoredGetCount());
            Assert.AreEqual(0, model.HiddenGetCount());
            Assert.AreEqual(1, model.HiddenShouldSerializeCount());
            Assert.AreEqual(1, model.DefaultGetCount());
            Assert.AreEqual(1, model.NullGetCount());
            Assert.AreEqual(1, model.WrittenGetCount());
        }

        [Test]
        public void JsonOutputIsUnchangedByTracking()
        {
            CountingIgnoreModel tracked = new CountingIgnoreModel();
            CountingIgnoreModel plain = new CountingIgnoreModel();

            string trackedJson = JsonConvert.SerializeObject(tracked,
                new JsonSerializerSettings { ContractResolver = new IgnoreTrackingContractResolver() });
            string plainJson = JsonConvert.SerializeObject(plain,
                new JsonSerializerSettings { ContractResolver = new DefaultContractResolver() });

            Assert.AreEqual(plainJson, trackedJson);
        }

        [Test]
        public void RecordsAreIndependentPerSerializationCall()
        {
            IgnoreTrackingContractResolver resolver = new IgnoreTrackingContractResolver();
            JsonSerializerSettings settings = new JsonSerializerSettings { ContractResolver = resolver };

            CountingIgnoreModel first = new CountingIgnoreModel();
            JsonConvert.SerializeObject(first, settings);
            Assert.AreEqual(3, resolver.IgnoredProperties.Count);
            Assert.IsTrue(resolver.IgnoredProperties.All(r => r.DeclaringType == typeof(CountingIgnoreModel)));

            SecondCountingModel second = new SecondCountingModel();
            string json = JsonConvert.SerializeObject(second, settings);

            // every property of the second model was skipped
            Assert.AreEqual("{}", json);

            // the second call replaced the records of the first call
            Assert.AreEqual(3, resolver.IgnoredProperties.Count);
            Assert.IsTrue(resolver.IgnoredProperties.All(r => r.DeclaringType == typeof(SecondCountingModel)));
            Assert.IsTrue(resolver.IgnoredProperties.Any(r => r.Reason == SerializationIgnoreReason.Ignored));
            Assert.IsTrue(resolver.IgnoredProperties.Any(r => r.Reason == SerializationIgnoreReason.ShouldSerialize));
            Assert.IsTrue(resolver.IgnoredProperties.Any(r => r.Reason == SerializationIgnoreReason.DefaultValue));

            // single evaluation on the second model as well
            Assert.AreEqual(0, second.VersionGetCount());
            Assert.AreEqual(0, second.NameGetCount());
            Assert.AreEqual(1, second.NameShouldSerializeCount());
            Assert.AreEqual(1, second.StatusGetCount());
        }

        [Test]
        public void FailingGetterCauseIsNotSwallowed()
        {
            IgnoreTrackingContractResolver resolver = new IgnoreTrackingContractResolver();

            JsonSerializationException ex = ExceptionAssert.Throws<JsonSerializationException>(() =>
                JsonConvert.SerializeObject(new ThrowingGetterModel(), new JsonSerializerSettings { ContractResolver = resolver }));

            // the original failure and its cause survive anywhere in the exception chain
            IList<Exception> chain = Flatten(ex);
            Assert.IsTrue(chain.Any(e => e is InvalidOperationException && e.Message == "getter failed"),
                "InvalidOperationException was swallowed. Chain: " + string.Join(" -> ", chain.Select(e => e.GetType().Name)));
            Assert.IsTrue(chain.Any(e => e is ApplicationException && e.Message == "root cause"),
                "Root cause was swallowed. Chain: " + string.Join(" -> ", chain.Select(e => e.GetType().Name)));

            // the failed property was not misreported as ignored
            Assert.IsFalse(resolver.IgnoredProperties.Any(r => r.PropertyName == "Boom"));
        }

        [Test]
        public void IgnoreReportCanBeWrittenToTempFolder()
        {
            IgnoreTrackingContractResolver resolver = new IgnoreTrackingContractResolver();
            CountingIgnoreModel model = new CountingIgnoreModel();

            string json = JsonConvert.SerializeObject(model, new JsonSerializerSettings { ContractResolver = resolver });

            string dir = Path.Combine(Path.GetTempPath(), "IgnoreTracking_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string reportPath = Path.Combine(dir, "ignore-report.txt");
                List<string> lines = new List<string> { "JSON: " + json };
                lines.AddRange(resolver.IgnoredProperties.Select(r => r.ToString()));
                File.WriteAllLines(reportPath, lines.ToArray());

                Console.WriteLine("Ignore report written to: " + reportPath);
                foreach (string line in lines)
                {
                    Console.WriteLine(line);
                }

                Assert.IsTrue(File.Exists(reportPath));
                string[] written = File.ReadAllLines(reportPath);
                Assert.AreEqual(4, written.Length);
                Assert.IsTrue(written[0].Contains(@"{""WrittenProp"":""written""}"));
                Assert.IsTrue(written.Any(l => l.Contains("IgnoredProp") && l.Contains("Ignored")));
                Assert.IsTrue(written.Any(l => l.Contains("HiddenProp") && l.Contains("ShouldSerialize")));
                Assert.IsTrue(written.Any(l => l.Contains("DefaultProp") && l.Contains("DefaultValue")));
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }
    }
}
