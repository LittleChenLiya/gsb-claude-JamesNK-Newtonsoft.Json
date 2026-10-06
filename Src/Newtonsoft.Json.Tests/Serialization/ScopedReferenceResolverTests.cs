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
    public class ScopedReferenceResolverTests : TestFixtureBase
    {
        public class SharedChild
        {
            public int Value { get; set; }
        }

        public class SharedHolder
        {
            public SharedChild First { get; set; }
            public SharedChild Second { get; set; }
        }

        public class Node
        {
            public string Name { get; set; }
            public Node Next { get; set; }
        }

        private const string SharedJson = @"{
  '$id': '1',
  'First': { '$id': '2', 'Value': 5 },
  'Second': { '$ref': '2' }
}";

        private const string DuplicateIdJson = @"{
  '$id': '1',
  'First': { '$id': '2', 'Value': 5 },
  'Second': { '$id': '2', 'Value': 6 }
}";

        private static JsonSerializerSettings CreateSettings(ScopedReferenceResolver resolver = null)
        {
            if (resolver == null)
            {
                resolver = new ScopedReferenceResolver();
            }

            return new JsonSerializerSettings { ReferenceResolverProvider = () => resolver };
        }

        private static void AssertMessageContains(string expected, string actual)
        {
            Assert.IsTrue(actual != null && actual.Contains(expected), "Expected message to contain: " + expected + " but was: " + actual);
        }

        [Test]
        public void TwoRefsResolveToSameSharedObject()
        {
            SharedHolder holder = JsonConvert.DeserializeObject<SharedHolder>(SharedJson, CreateSettings());

            Assert.AreEqual(5, holder.First.Value);
            Assert.IsTrue(ReferenceEquals(holder.First, holder.Second));
        }

        [Test]
        public void DuplicateIdIsRejected()
        {
            JsonSerializationException ex = ExceptionAssert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<SharedHolder>(DuplicateIdJson, CreateSettings()));

            AssertMessageContains("Error reading object reference '2'.", ex.Message);
            Assert.IsNotNull(ex.InnerException);
            AssertMessageContains("Duplicate object Id '2'. The same Id has already been defined in the current deserialization scope.", ex.InnerException.Message);
        }

        [Test]
        public void SameIdsInSecondDeserializeAreIndependentObjects()
        {
            // reuse one resolver instance across calls: Ids must not leak between calls
            JsonSerializerSettings settings = CreateSettings(new ScopedReferenceResolver());

            SharedHolder first = JsonConvert.DeserializeObject<SharedHolder>(SharedJson, settings);
            SharedHolder second = JsonConvert.DeserializeObject<SharedHolder>(SharedJson, settings);

            Assert.IsTrue(ReferenceEquals(first.First, first.Second));
            Assert.IsTrue(ReferenceEquals(second.First, second.Second));
            Assert.IsFalse(ReferenceEquals(first, second));
            Assert.IsFalse(ReferenceEquals(first.First, second.First));
            Assert.AreEqual(5, second.First.Value);
        }

        [Test]
        public void FailedDeserializeLeavesNoResidualState()
        {
            JsonSerializerSettings settings = CreateSettings(new ScopedReferenceResolver());

            // first call fails halfway: the half-populated objects must not be kept by the resolver
            ExceptionAssert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<SharedHolder>(DuplicateIdJson, settings));

            // Id '2' from the failed call must not be resolvable anymore
            JsonSerializationException ex = ExceptionAssert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<SharedHolder>(
                    @"{'$id':'1','First':{'$ref':'2'},'Second':null}", settings));
            AssertMessageContains("Could not resolve JSON reference '2'.", ex.Message);

            // and a valid payload still deserializes cleanly with the same resolver instance
            SharedHolder holder = JsonConvert.DeserializeObject<SharedHolder>(SharedJson, settings);
            Assert.IsTrue(ReferenceEquals(holder.First, holder.Second));
            Assert.AreEqual(5, holder.First.Value);
        }

        [Test]
        public void UnknownForwardReferenceIsDiagnosed()
        {
            // forward references to an $id defined later in the document are not supported
            string json = @"{
  '$id': '1',
  'First': { '$ref': '2' },
  'Second': { '$id': '2', 'Value': 6 }
}";

            JsonSerializationException ex = ExceptionAssert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<SharedHolder>(json, CreateSettings()));

            AssertMessageContains("Could not resolve JSON reference '2'. The reference has not been defined in the current deserialization scope. Forward references to an Id defined later in the document are not supported.", ex.Message);
        }

        [Test]
        public void MaliciousTypeNameIsNotLoaded()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new AllowedTypesSerializationBinder(typeof(SharedChild))
            };

            string maliciousJson = @"{'$type':'System.IO.FileInfo, System.IO.FileSystem','fileName':'c:\\evil.txt'}";

            JsonSerializationException ex = ExceptionAssert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<object>(maliciousJson, settings));
            AssertMessageContains("Error resolving type specified in JSON 'System.IO.FileInfo, System.IO.FileSystem'.", ex.Message);
            Assert.IsNotNull(ex.InnerException);
            AssertMessageContains("Type 'System.IO.FileInfo, System.IO.FileSystem' is not in the list of allowed types and will not be loaded.", ex.InnerException.Message);

            // an allow-listed type still binds and deserializes
            string allowedJson = @"{'$type':'" + typeof(SharedChild).FullName + @", Newtonsoft.Json.Tests','Value':5}";
            object value = JsonConvert.DeserializeObject<object>(allowedJson, settings);

            Assert.IsInstanceOf(typeof(SharedChild), value);
            Assert.AreEqual(5, ((SharedChild)value).Value);
        }

        [Test]
        public void ThreeNodeCycleRoundTrips()
        {
            // three-node cycle: A -> B -> C -> A
            Node a = new Node { Name = "A" };
            Node b = new Node { Name = "B" };
            Node c = new Node { Name = "C" };
            a.Next = b;
            b.Next = c;
            c.Next = a;

            ScopedReferenceResolver resolver = new ScopedReferenceResolver();
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                ReferenceResolverProvider = () => resolver
            };

            string json = JsonConvert.SerializeObject(a, Formatting.Indented, settings);

            StringAssert.AreEqual(@"{
  ""$id"": ""1"",
  ""Name"": ""A"",
  ""Next"": {
    ""$id"": ""2"",
    ""Name"": ""B"",
    ""Next"": {
      ""$id"": ""3"",
      ""Name"": ""C"",
      ""Next"": {
        ""$ref"": ""1""
      }
    }
  }
}", json);

            Node a2 = JsonConvert.DeserializeObject<Node>(json, settings);

            Assert.AreEqual("A", a2.Name);
            Assert.AreEqual("B", a2.Next.Name);
            Assert.AreEqual("C", a2.Next.Next.Name);
            // the cycle closes on the same instance, not a copy
            Assert.IsTrue(ReferenceEquals(a2, a2.Next.Next.Next));
        }

        [Test]
        public void DefaultSerializerBehaviorIsUnchanged()
        {
            // the default serializer keeps its existing behavior: a duplicate $id fails with the
            // generic default-resolver error, and valid shared references still resolve as before
            JsonSerializationException ex = ExceptionAssert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<SharedHolder>(DuplicateIdJson));
            AssertMessageContains("Error reading object reference '2'.", ex.Message);
            AssertMessageContains("A different value already has the Id '2'.", ex.InnerException.Message);

            SharedHolder holder = JsonConvert.DeserializeObject<SharedHolder>(SharedJson);
            Assert.IsTrue(ReferenceEquals(holder.First, holder.Second));
            Assert.AreEqual(5, holder.First.Value);
        }
    }
}
