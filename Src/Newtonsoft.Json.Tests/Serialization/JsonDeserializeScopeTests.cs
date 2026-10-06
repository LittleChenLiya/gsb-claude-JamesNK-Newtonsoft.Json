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
    public class JsonDeserializeScopeTests : TestFixtureBase
    {
        public class SharedNode
        {
            public string Name { get; set; }
        }

        public class NodeGraph
        {
            public SharedNode Shared { get; set; }
            public SharedNode Left { get; set; }
            public SharedNode Right { get; set; }
        }

        public class LinkedNode
        {
            public string Name { get; set; }
            public LinkedNode Next { get; set; }
        }

        public class MaliciousPayload
        {
            public static bool WasLoaded;

            static MaliciousPayload()
            {
                WasLoaded = true;
            }

            public string Name { get; set; }
        }

        // Three-node example: one shared node defined with $id, two $ref values pointing at it.
        private const string ThreeNodeJson = @"{
  'shared': { '$id': '1', 'name': 'shared' },
  'left':   { '$ref': '1' },
  'right':  { '$ref': '1' }
}";

        [Test]
        public void TwoRefsResolveToSameObject()
        {
            JsonDeserializeScope scope = new JsonDeserializeScope();

            NodeGraph graph = scope.Deserialize<NodeGraph>(ThreeNodeJson);

            Assert.IsNotNull(graph.Shared);
            Assert.AreEqual("shared", graph.Shared.Name);
            Assert.IsTrue(ReferenceEquals(graph.Shared, graph.Left));
            Assert.IsTrue(ReferenceEquals(graph.Shared, graph.Right));
        }

        [Test]
        public void DuplicateIdIsRejected()
        {
            JsonDeserializeScope scope = new JsonDeserializeScope();

            string json = @"{
  'shared': { '$id': '1', 'name': 'a' },
  'left':   { '$id': '1', 'name': 'b' },
  'right':  null
}";

            JsonSerializationException ex = ExceptionAssert.Throws<JsonSerializationException>(() => scope.Deserialize<NodeGraph>(json));
            Assert.IsNotNull(ex.InnerException);
            Assert.IsTrue(ex.InnerException.Message.Contains("Duplicate reference Id '1'"), ex.InnerException.Message);
        }

        [Test]
        public void UnknownForwardReferenceIsRejected()
        {
            JsonDeserializeScope scope = new JsonDeserializeScope();

            // 'left' references an $id that is never defined.
            string json = @"{
  'shared': { '$id': '1', 'name': 'shared' },
  'left':   { '$ref': '99' },
  'right':  { '$ref': '1' }
}";

            JsonSerializationException ex = ExceptionAssert.Throws<JsonSerializationException>(() => scope.Deserialize<NodeGraph>(json));
            Assert.IsTrue(ex.Message.Contains("Cannot resolve reference '99'"), ex.Message);
        }

        [Test]
        public void SecondCallWithSameIdIsIndependent()
        {
            JsonDeserializeScope scope = new JsonDeserializeScope();

            NodeGraph first = scope.Deserialize<NodeGraph>(ThreeNodeJson);
            NodeGraph second = scope.Deserialize<NodeGraph>(ThreeNodeJson);

            // No residual IDs from the first call: the same $id is accepted again
            // and produces independent objects.
            Assert.IsFalse(ReferenceEquals(first.Shared, second.Shared));
            Assert.IsTrue(ReferenceEquals(second.Shared, second.Left));
            Assert.IsTrue(ReferenceEquals(second.Shared, second.Right));
        }

        [Test]
        public void FailedCallLeavesNoResidualIds()
        {
            JsonDeserializeScope scope = new JsonDeserializeScope();

            string duplicateIdJson = @"{
  'shared': { '$id': '1', 'name': 'a' },
  'left':   { '$id': '1', 'name': 'b' },
  'right':  null
}";

            ExceptionAssert.Throws<JsonSerializationException>(() => scope.Deserialize<NodeGraph>(duplicateIdJson));

            // The failed call did not store half-registered objects anywhere:
            // $id '1' can be defined again by the next call.
            NodeGraph graph = scope.Deserialize<NodeGraph>(ThreeNodeJson);
            Assert.AreEqual("shared", graph.Shared.Name);
            Assert.IsTrue(ReferenceEquals(graph.Shared, graph.Left));
        }

        [Test]
        public void MaliciousTypeNameIsNotLoaded()
        {
            MaliciousPayload.WasLoaded = false;

            JsonDeserializeScope scope = new JsonDeserializeScope();

            string maliciousTypeName = typeof(MaliciousPayload).FullName + ", " + typeof(MaliciousPayload).Assembly.GetName().Name;
            string json = @"{
  'shared': { '$type': '" + maliciousTypeName + @"', '$id': '1', 'name': 'shared' },
  'left':   { '$ref': '1' },
  'right':  { '$ref': '1' }
}";

            NodeGraph graph = scope.Deserialize<NodeGraph>(json);

            // TypeNameHandling.None: the $type value is ignored, the declared type is used
            // and the malicious type is never loaded.
            Assert.IsFalse(MaliciousPayload.WasLoaded);
            Assert.AreEqual(typeof(SharedNode), graph.Shared.GetType());
            Assert.AreEqual("shared", graph.Shared.Name);
        }

        [Test]
        public void TypeNameHandlingWithoutBinderIsRejected()
        {
            InvalidOperationException ex = ExceptionAssert.Throws<InvalidOperationException>(() =>
                new JsonDeserializeScope(s => s.TypeNameHandling = TypeNameHandling.All));

            Assert.IsTrue(ex.Message.Contains("SerializationBinder"), ex.Message);
        }

        [Test]
        public void TypeNameHandlingWithKnownTypesBinderLoadsOnlyWhitelistedTypes()
        {
            KnownTypesSerializationBinder binder = new KnownTypesSerializationBinder(typeof(MaliciousPayload));
            JsonDeserializeScope scope = new JsonDeserializeScope(s =>
            {
                s.TypeNameHandling = TypeNameHandling.Objects;
                s.SerializationBinder = binder;
            });

            // Whitelisted type binds.
            string allowedJson = @"{ '$type': '" + typeof(MaliciousPayload).FullName + ", " + typeof(MaliciousPayload).Assembly.GetName().Name + @"', 'name': 'ok' }";
            object allowed = scope.Deserialize<object>(allowedJson);
            Assert.AreEqual(typeof(MaliciousPayload), allowed.GetType());

            // Anything else is rejected by the binder before any type is loaded.
            string deniedJson = @"{ '$type': 'System.IO.FileInfo, System.IO.FileSystem', 'name': 'evil' }";
            JsonSerializationException ex = ExceptionAssert.Throws<JsonSerializationException>(() => scope.Deserialize<object>(deniedJson));
            Assert.IsNotNull(ex.InnerException);
            Assert.IsTrue(ex.InnerException.Message.Contains("not in the list of known types"), ex.InnerException.Message);
        }

        [Test]
        public void ExternalReferenceResolverIsRejected()
        {
            InvalidOperationException ex = ExceptionAssert.Throws<InvalidOperationException>(() =>
                new JsonDeserializeScope(s => s.ReferenceResolver = new ScopedReferenceResolver()));

            Assert.IsTrue(ex.Message.Contains("owns reference resolution"), ex.Message);
        }

        [Test]
        public void ObjectCycleWithinSupportedScopeRoundTrips()
        {
            JsonDeserializeScope scope = new JsonDeserializeScope();

            // Three-node cycle: A -> B -> C -> A. Backward $ref to an already-defined $id.
            string json = @"{
  '$id': '1',
  'name': 'A',
  'next': {
    '$id': '2',
    'name': 'B',
    'next': {
      '$id': '3',
      'name': 'C',
      'next': { '$ref': '1' }
    }
  }
}";

            LinkedNode a = scope.Deserialize<LinkedNode>(json);

            Assert.AreEqual("A", a.Name);
            Assert.AreEqual("B", a.Next.Name);
            Assert.AreEqual("C", a.Next.Next.Name);
            Assert.IsTrue(ReferenceEquals(a, a.Next.Next.Next));
        }

        [Test]
        public void DefaultSerializerBehaviorIsUnchanged()
        {
            // The default serializer keeps its pre-existing behavior: $ref values resolve
            // through the DefaultReferenceResolver. The scoped per-call resolver and its
            // diagnostics are opt-in via JsonDeserializeScope.
            NodeGraph graph = JsonConvert.DeserializeObject<NodeGraph>(ThreeNodeJson);

            Assert.IsNotNull(graph.Shared);
            Assert.AreEqual("shared", graph.Shared.Name);
            Assert.IsTrue(ReferenceEquals(graph.Shared, graph.Left));
            Assert.IsTrue(ReferenceEquals(graph.Shared, graph.Right));

            string duplicateIdJson = @"{
  'shared': { '$id': '1', 'name': 'a' },
  'left':   { '$id': '1', 'name': 'b' },
  'right':  null
}";

            // Pre-existing default behavior for a duplicate $id is also preserved:
            // the DefaultReferenceResolver rejects it with its own error.
            JsonSerializationException ex = ExceptionAssert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<NodeGraph>(duplicateIdJson));
            Assert.IsTrue(ex.Message.Contains("Error reading object reference '1'"), ex.Message);
        }
    }
}
