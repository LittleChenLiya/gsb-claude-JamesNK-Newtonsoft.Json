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
using Newtonsoft.Json;

namespace Newtonsoft.Json.Tests.JsonTextReaderTests
{
    [TestFixture]
#if !DNXCORE50
    [Category("JsonTextReaderTests")]
#endif
    public class ContainerDepthTests : TestFixtureBase
    {
        private static string ReadExample(string fileName)
        {
            return File.ReadAllText(ResolvePath("JsonTextReaderTests/Data/" + fileName));
        }

        [Test]
        public void ContainerDepth_TwoLevelsCompleteWithinLimit()
        {
            // example: DepthTwoLevels.json - two legal nesting levels read to completion
            string json = ReadExample("DepthTwoLevels.json");

            JsonTextReader reader = new JsonTextReader(new StringReader(json))
            {
                MaxDepth = 2
            };

            Assert.AreEqual(0, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);
            Assert.AreEqual(1, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual(1, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual(1, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);
            Assert.AreEqual(2, reader.ContainerDepth);

            // at the limit: values and closing tokens are still read normally
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual(2, reader.ContainerDepth);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);
            Assert.AreEqual(1, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndObject, reader.TokenType);
            Assert.AreEqual(0, reader.ContainerDepth);

            Assert.IsFalse(reader.Read());
            Assert.AreEqual(0, reader.ContainerDepth);
        }

        [Test]
        public void ContainerDepth_ThirdLevelRejectedBeforeOpening()
        {
            // example: DepthThreeLevels.json - the third nesting level is rejected
            // while streaming, before the container token is produced (no final JToken involved)
            string json = ReadExample("DepthThreeLevels.json");

            JsonTextReader reader = new JsonTextReader(new StringReader(json))
            {
                MaxDepth = 2
            };

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);
            Assert.AreEqual(1, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("outer", reader.Value);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);
            Assert.AreEqual(2, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("inner", reader.Value);

            // the third level is rejected; the exception keeps the path and the original position
            JsonReaderException ex = ExceptionAssert.Throws<JsonReaderException>(
                () => reader.Read(),
                "The reader's MaxDepth of 2 has been exceeded. Path 'outer.inner', line 3, position 14.");

            Assert.AreEqual("outer.inner", ex.Path);
            Assert.AreEqual(3, ex.LineNumber);
            Assert.AreEqual(14, ex.LinePosition);
            Assert.IsTrue(ex.Message.StartsWith("The reader's MaxDepth of 2 has been exceeded.", StringComparison.Ordinal));
        }

        [Test]
        public void ContainerDepth_MultipleContentResetsPerRoot()
        {
            // example: DepthMultipleContent.json - two root values with comments around them;
            // each root starts with a fresh depth count instead of inheriting the old one
            string json = ReadExample("DepthMultipleContent.json");

            JsonTextReader reader = new JsonTextReader(new StringReader(json))
            {
                MaxDepth = 2,
                SupportMultipleContent = true
            };

            int commentCount = 0;
            int maxContainerDepth = 0;
            int rootCount = 0;
            bool previousWasRootEnd = true;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.Comment)
                {
                    commentCount++;
                    continue;
                }

                if (reader.TokenType == JsonToken.StartObject || reader.TokenType == JsonToken.StartArray)
                {
                    if (previousWasRootEnd)
                    {
                        rootCount++;
                        // a new root never inherits the previous root's depth count
                        Assert.AreEqual(1, reader.ContainerDepth);
                    }

                    previousWasRootEnd = false;
                }

                if (reader.ContainerDepth == 0)
                {
                    previousWasRootEnd = true;
                }

                maxContainerDepth = Math.Max(maxContainerDepth, reader.ContainerDepth);
            }

            Assert.AreEqual(2, rootCount);
            Assert.AreEqual(3, commentCount);
            Assert.AreEqual(2, maxContainerDepth);
            Assert.AreEqual(0, reader.ContainerDepth);
        }

        [Test]
        public void ContainerDepth_EofIsDistinctFromDepthLimit()
        {
            // truncated JSON at the limit: EOF is reported by Read() returning false, not by a MaxDepth error
            JsonTextReader truncatedReader = new JsonTextReader(new StringReader("[[1]"))
            {
                MaxDepth = 2
            };

            Assert.IsTrue(truncatedReader.Read());
            Assert.IsTrue(truncatedReader.Read());
            Assert.AreEqual(2, truncatedReader.ContainerDepth);
            Assert.IsTrue(truncatedReader.Read());
            Assert.IsTrue(truncatedReader.Read());
            Assert.AreEqual(1, truncatedReader.ContainerDepth);
            Assert.IsFalse(truncatedReader.Read());

            // one more level than the limit: a MaxDepth error is reported, not EOF
            JsonTextReader tooDeepReader = new JsonTextReader(new StringReader("[[[1]]]"))
            {
                MaxDepth = 2
            };

            Assert.IsTrue(tooDeepReader.Read());
            Assert.IsTrue(tooDeepReader.Read());

            JsonReaderException depthEx = ExceptionAssert.Throws<JsonReaderException>(() => tooDeepReader.Read());
            Assert.IsTrue(depthEx.Message.StartsWith("The reader's MaxDepth of 2 has been exceeded.", StringComparison.Ordinal));

            // unexpected end inside a value: an EOF error is reported, not a MaxDepth error
            JsonTextReader unexpectedEndReader = new JsonTextReader(new StringReader("{\"a\":n"))
            {
                MaxDepth = 2
            };

            Assert.IsTrue(unexpectedEndReader.Read());
            Assert.IsTrue(unexpectedEndReader.Read());

            JsonReaderException endEx = ExceptionAssert.Throws<JsonReaderException>(() => unexpectedEndReader.Read());
            Assert.IsTrue(endEx.Message.StartsWith("Unexpected end when reading JSON.", StringComparison.Ordinal));
            Assert.AreEqual("a", endEx.Path);
        }

        [Test]
        public void ContainerDepth_ClosingStillAllowedAfterLimitExceeded()
        {
            string json = "[[[1]]]";

            JsonTextReader reader = new JsonTextReader(new StringReader(json))
            {
                MaxDepth = 2
            };

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(1, reader.ContainerDepth);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(2, reader.ContainerDepth);

            ExceptionAssert.Throws<JsonReaderException>(() => reader.Read(),
                "The reader's MaxDepth of 2 has been exceeded. Path '[0][0]', line 1, position 3.");

            // closing the containers that are already open still works
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual(3, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);
            Assert.AreEqual(2, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);
            Assert.AreEqual(1, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);
            Assert.AreEqual(0, reader.ContainerDepth);

            Assert.IsFalse(reader.Read());
        }

        [Test]
        public void ContainerDepth_CommentsDoNotCount()
        {
            string json = "{/*c1*/\"a\":[1/*c2*/]}/*c3*/";

            JsonTextReader reader = new JsonTextReader(new StringReader(json))
            {
                MaxDepth = 2
            };

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);
            Assert.AreEqual(1, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Comment, reader.TokenType);
            Assert.AreEqual(1, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual(1, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);
            Assert.AreEqual(2, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual(2, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Comment, reader.TokenType);
            Assert.AreEqual(2, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);
            Assert.AreEqual(1, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndObject, reader.TokenType);
            Assert.AreEqual(0, reader.ContainerDepth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Comment, reader.TokenType);
            Assert.AreEqual(0, reader.ContainerDepth);

            Assert.IsFalse(reader.Read());
        }

        [Test]
        public void ContainerDepth_DefaultReaderUnchanged()
        {
            // the default reader keeps its default MaxDepth of 64 and reads deeper JSON without error
            string json = ReadExample("DepthThreeLevels.json");

            JsonTextReader reader = new JsonTextReader(new StringReader(json));
            Assert.AreEqual(64, reader.MaxDepth);

            int maxContainerDepth = 0;
            while (reader.Read())
            {
                maxContainerDepth = Math.Max(maxContainerDepth, reader.ContainerDepth);
            }

            Assert.AreEqual(3, maxContainerDepth);
            Assert.AreEqual(0, reader.ContainerDepth);
        }

#if !(NET20 || NET35 || NET40 || PORTABLE40)
        [Test]
        public async Task ContainerDepth_TwoLevelsCompleteWithinLimitAsync()
        {
            string json = ReadExample("DepthTwoLevels.json");

            JsonTextReader reader = new JsonTextReader(new StringReader(json))
            {
                MaxDepth = 2
            };

            int maxContainerDepth = 0;
            while (await reader.ReadAsync())
            {
                maxContainerDepth = Math.Max(maxContainerDepth, reader.ContainerDepth);
            }

            Assert.AreEqual(2, maxContainerDepth);
            Assert.AreEqual(0, reader.ContainerDepth);
        }

        [Test]
        public async Task ContainerDepth_ThirdLevelRejectedBeforeOpeningAsync()
        {
            string json = ReadExample("DepthThreeLevels.json");

            JsonTextReader reader = new JsonTextReader(new StringReader(json))
            {
                MaxDepth = 2
            };

            Assert.IsTrue(await reader.ReadAsync());
            Assert.IsTrue(await reader.ReadAsync());
            Assert.IsTrue(await reader.ReadAsync());
            Assert.IsTrue(await reader.ReadAsync());

            JsonReaderException ex = await ExceptionAssert.ThrowsAsync<JsonReaderException>(
                async () => await reader.ReadAsync(),
                "The reader's MaxDepth of 2 has been exceeded. Path 'outer.inner', line 3, position 14.");

            Assert.AreEqual("outer.inner", ex.Path);
            Assert.AreEqual(3, ex.LineNumber);
        }

        [Test]
        public async Task ContainerDepth_MultipleContentResetsPerRootAsync()
        {
            string json = ReadExample("DepthMultipleContent.json");

            JsonTextReader reader = new JsonTextReader(new StringReader(json))
            {
                MaxDepth = 2,
                SupportMultipleContent = true
            };

            int maxContainerDepth = 0;
            while (await reader.ReadAsync())
            {
                maxContainerDepth = Math.Max(maxContainerDepth, reader.ContainerDepth);
            }

            Assert.AreEqual(2, maxContainerDepth);
            Assert.AreEqual(0, reader.ContainerDepth);
        }
#endif
    }
}
