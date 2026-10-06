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
using System.IO;
#if !(NET20 || NET35 || NET40 || PORTABLE40)
using System.Threading.Tasks;
#endif
#if DNXCORE50
using Xunit;
using Test = Xunit.FactAttribute;
using Assert = Newtonsoft.Json.Tests.XUnitAssert;
#else
using NUnit.Framework;
#endif
using Newtonsoft.Json.Linq;

namespace Newtonsoft.Json.Tests.JsonTextReaderTests
{
    [TestFixture]
#if !DNXCORE50
    [Category("JsonTextReaderTests")]
#endif
    public class DepthAccountingTests : TestFixtureBase
    {
        private static JsonTextReader CreateReader(string json, int? maxDepth = 2, bool supportMultipleContent = false)
        {
            JsonTextReader reader = new JsonTextReader(new StringReader(json))
            {
                MaxDepth = maxDepth,
                SupportMultipleContent = supportMultipleContent
            };
            return reader;
        }

        private static List<JsonToken> ReadAll(JsonReader reader)
        {
            List<JsonToken> tokens = new List<JsonToken>();
            while (reader.Read())
            {
                tokens.Add(reader.TokenType);
            }
            return tokens;
        }

        [Test]
        public void Depth2_TwoLevels_ReadsToEndOfFile()
        {
            JsonTextReader reader = CreateReader("{\"a\":[1,2,3],\"b\":{\"c\":true}}");

            List<JsonToken> tokens = ReadAll(reader);

            CollectionAssert.AreEqual(
                new[]
                {
                    JsonToken.StartObject, JsonToken.PropertyName, JsonToken.StartArray,
                    JsonToken.Integer, JsonToken.Integer, JsonToken.Integer, JsonToken.EndArray,
                    JsonToken.PropertyName, JsonToken.StartObject, JsonToken.PropertyName,
                    JsonToken.Boolean, JsonToken.EndObject, JsonToken.EndObject
                },
                tokens);
            Assert.AreEqual(JsonToken.None, reader.TokenType);
        }

        [Test]
        public void Depth2_ThirdLevelContainer_RejectedBeforeOpen()
        {
            JsonTextReader reader = CreateReader("{\"a\":[1,{\"b\":2}]}");

            Assert.IsTrue(reader.Read()); // StartObject (depth 1)
            Assert.IsTrue(reader.Read()); // PropertyName 'a'
            Assert.IsTrue(reader.Read()); // StartArray (depth 2)
            Assert.IsTrue(reader.Read()); // Integer 1

            JsonReaderException ex = ExceptionAssert.Throws<JsonReaderException>(() => reader.Read());
            Assert.IsTrue(ex.Message.Contains("The reader's MaxDepth of 2 has been exceeded."));

            // the exception keeps the path and original position of the reader
            Assert.AreEqual("a[0]", ex.Path);
            Assert.IsTrue(ex.Message.Contains("Path 'a[0]'"));
            Assert.AreEqual(1, ex.LineNumber);
            Assert.IsTrue(ex.LinePosition > 0);

            // the container was rejected before it was opened: reader state is untouched
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual(2, reader.Depth);
            Assert.AreEqual("a[0]", reader.Path);
        }

        [Test]
        public void Depth2_AtLimit_ClosingContainersStillAllowed()
        {
            JsonTextReader reader = CreateReader("[[1,2],[3]]");

            List<JsonToken> tokens = ReadAll(reader);

            CollectionAssert.AreEqual(
                new[]
                {
                    JsonToken.StartArray, JsonToken.StartArray,
                    JsonToken.Integer, JsonToken.Integer, JsonToken.EndArray,
                    JsonToken.StartArray, JsonToken.Integer, JsonToken.EndArray,
                    JsonToken.EndArray
                },
                tokens);
        }

        [Test]
        public void MultiContent_EachRootGetsFreshDepthCount()
        {
            JsonTextReader reader = CreateReader("[[1]] [[2]] [[3]]", supportMultipleContent: true);

            List<JsonToken> tokens = ReadAll(reader);

            // three roots, each two levels deep: every root starts again from zero
            Assert.AreEqual(15, tokens.Count);
            Assert.AreEqual(6, tokens.FindAll(t => t == JsonToken.StartArray).Count);
            Assert.AreEqual(6, tokens.FindAll(t => t == JsonToken.EndArray).Count);
        }

        [Test]
        public void Comments_DoNotCountTowardsDepth()
        {
            JsonTextReader reader = CreateReader("[/* before */[1]/* after */]");

            List<JsonToken> tokens = ReadAll(reader);

            CollectionAssert.AreEqual(
                new[]
                {
                    JsonToken.StartArray, JsonToken.Comment,
                    JsonToken.StartArray, JsonToken.Integer, JsonToken.EndArray,
                    JsonToken.Comment, JsonToken.EndArray
                },
                tokens);
        }

        [Test]
        public void EndOfFile_IsDistinctFromDepthExceeded()
        {
            // clean EOF inside open containers: reading simply ends, no depth error
            JsonTextReader eofReader = CreateReader("[[");
            Assert.IsTrue(eofReader.Read());
            Assert.IsTrue(eofReader.Read());
            Assert.IsFalse(eofReader.Read());
            Assert.AreEqual(JsonToken.StartArray, eofReader.TokenType);

            // same input one level deeper: depth error, not EOF
            JsonTextReader depthReader = CreateReader("[[[");
            Assert.IsTrue(depthReader.Read());
            Assert.IsTrue(depthReader.Read());
            JsonReaderException ex = ExceptionAssert.Throws<JsonReaderException>(() => depthReader.Read());
            Assert.IsTrue(ex.Message.Contains("The reader's MaxDepth of 2 has been exceeded."));

            // unterminated string at EOF: an unexpected-end error, not a depth error
            JsonTextReader stringReader = CreateReader("[\"abc");
            Assert.IsTrue(stringReader.Read());
            JsonReaderException eofEx = ExceptionAssert.Throws<JsonReaderException>(() => stringReader.Read());
            Assert.IsTrue(eofEx.Message.Contains("Unterminated string"));
            Assert.IsFalse(eofEx.Message.Contains("MaxDepth"));
        }

        [Test]
        public void DefaultReader_MaxDepthUnchanged()
        {
            // the default reader still allows 64 levels and rejects the 65th
            string deep64 = new string('[', 64) + new string(']', 64);
            JsonTextReader reader = new JsonTextReader(new StringReader(deep64));
            Assert.AreEqual(128, ReadAll(reader).Count);

            string deep65 = new string('[', 65) + new string(']', 65);
            JsonTextReader overReader = new JsonTextReader(new StringReader(deep65));
            JsonReaderException ex = ExceptionAssert.Throws<JsonReaderException>(() => ReadAll(overReader));
            Assert.IsTrue(ex.Message.Contains("The reader's MaxDepth of 64 has been exceeded."));
        }

        [Test]
        public void JTokenLoad_EnforcesDepthDuringRead()
        {
            JsonTextReader reader = CreateReader("{\"a\":[1,{\"b\":2}]}");

            JsonReaderException ex = ExceptionAssert.Throws<JsonReaderException>(() => JToken.Load(reader));
            Assert.IsTrue(ex.Message.Contains("The reader's MaxDepth of 2 has been exceeded."));
        }

        [Test]
        public void ExampleFile_DepthTwoValid_ReadsToEndOfFile()
        {
            string path = ResolvePath(Path.Combine("JsonTextReaderTests", "Data", "DepthTwoValid.json"));
            using (StreamReader file = File.OpenText(path))
            {
                JsonTextReader reader = new JsonTextReader(file) { MaxDepth = 2 };
                List<JsonToken> tokens = ReadAll(reader);
                Assert.AreEqual(13, tokens.Count);
                Assert.AreEqual(JsonToken.None, reader.TokenType);
            }
        }

        [Test]
        public void ExampleFile_DepthThreeRejected_ThrowsAtThirdLevel()
        {
            string path = ResolvePath(Path.Combine("JsonTextReaderTests", "Data", "DepthThreeRejected.json"));
            using (StreamReader file = File.OpenText(path))
            {
                JsonTextReader reader = new JsonTextReader(file) { MaxDepth = 2 };
                JsonReaderException ex = ExceptionAssert.Throws<JsonReaderException>(() => ReadAll(reader));
                Assert.IsTrue(ex.Message.Contains("The reader's MaxDepth of 2 has been exceeded."));
                Assert.AreEqual("a[0]", ex.Path);
            }
        }

        [Test]
        public void ExampleFile_MultiRootWithComments_EachRootResetsAndCommentsIgnored()
        {
            string path = ResolvePath(Path.Combine("JsonTextReaderTests", "Data", "MultiRootWithComments.json"));
            using (StreamReader file = File.OpenText(path))
            {
                JsonTextReader reader = new JsonTextReader(file)
                {
                    MaxDepth = 2,
                    SupportMultipleContent = true
                };

                List<JsonToken> tokens = ReadAll(reader);

                // two roots (each two container levels) plus three comments read to a clean EOF
                Assert.AreEqual(3, tokens.FindAll(t => t == JsonToken.Comment).Count);
                Assert.AreEqual(2, tokens.FindAll(t => t == JsonToken.StartObject).Count);
                Assert.AreEqual(2, tokens.FindAll(t => t == JsonToken.StartArray).Count);
                Assert.AreEqual(JsonToken.None, reader.TokenType);
            }
        }

#if !(NET20 || NET35 || NET40 || PORTABLE40)
        [Test]
        public async Task Depth2_ThirdLevelContainer_RejectedBeforeOpen_Async()
        {
            JsonTextReader reader = CreateReader("{\"a\":[1,{\"b\":2}]}");

            Assert.IsTrue(await reader.ReadAsync()); // StartObject
            Assert.IsTrue(await reader.ReadAsync()); // PropertyName 'a'
            Assert.IsTrue(await reader.ReadAsync()); // StartArray
            Assert.IsTrue(await reader.ReadAsync()); // Integer 1

            try
            {
                await reader.ReadAsync();
                Assert.Fail("JsonReaderException expected.");
            }
            catch (JsonReaderException ex)
            {
                Assert.IsTrue(ex.Message.Contains("The reader's MaxDepth of 2 has been exceeded."));
                Assert.AreEqual("a[0]", ex.Path);
            }

            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual(2, reader.Depth);
        }
#endif
    }
}
