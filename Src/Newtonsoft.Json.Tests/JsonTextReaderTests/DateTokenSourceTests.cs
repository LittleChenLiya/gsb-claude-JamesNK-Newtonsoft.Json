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
#if DNXCORE50
using Xunit;
using Test = Xunit.FactAttribute;
using Assert = Newtonsoft.Json.Tests.XUnitAssert;
#else
using NUnit.Framework;
#endif

namespace Newtonsoft.Json.Tests.JsonTextReaderTests
{
    [TestFixture]
#if !DNXCORE50
    [Category("JsonTextReaderTests")]
#endif
    public class DateTokenSourceTests : TestFixtureBase
    {
        // The same instant written three ways: Z, an explicit +08:00 offset and no offset at all.
        private const string UtcText = "2000-01-01T00:00:00Z";
        private const string EastText = "2000-01-01T08:00:00+08:00";
        private const string NoOffsetText = "2000-01-01T00:00:00";

        private static JsonTextReader CreateReader(string json, DateParseHandling dateParseHandling)
        {
            JsonTextReader reader = new JsonTextReader(new StringReader(json));
            reader.DateParseHandling = dateParseHandling;
            return reader;
        }

        private static JsonDateTokenSource ReadSingleDateSource(string text, DateParseHandling dateParseHandling, out object value)
        {
            JsonTextReader reader = CreateReader("[\"" + text + "\"]", dateParseHandling);
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Date, reader.TokenType);
            value = reader.Value;

            JsonDateTokenSource? source = reader.DateTokenSource;
            Assert.IsTrue(source.HasValue);
            Assert.AreEqual(text, source.Value.Text);
            return source.Value;
        }

        [Test]
        public void DateTokenSource_DateTime_PreservesExplicitOffset()
        {
            object value;

            JsonDateTokenSource utc = ReadSingleDateSource(UtcText, DateParseHandling.DateTime, out value);
            Assert.AreEqual(TimeSpan.Zero, utc.Offset);
            Assert.IsTrue(utc.HasExplicitOffset);
            Assert.AreEqual(DateTimeKind.Utc, ((DateTime)value).Kind);
            Assert.AreEqual(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), (DateTime)value);

            JsonDateTokenSource east = ReadSingleDateSource(EastText, DateParseHandling.DateTime, out value);
            Assert.AreEqual(TimeSpan.FromHours(8), east.Offset);
            Assert.IsTrue(east.HasExplicitOffset);
            // converted to local time by the reader, but it is still the same instant
            Assert.AreEqual(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), ((DateTime)value).ToUniversalTime());

            JsonDateTokenSource none = ReadSingleDateSource(NoOffsetText, DateParseHandling.DateTime, out value);
            Assert.AreEqual(null, none.Offset);
            Assert.IsFalse(none.HasExplicitOffset);
            Assert.AreEqual(DateTimeKind.Unspecified, ((DateTime)value).Kind);
            Assert.AreEqual(new DateTime(2000, 1, 1, 0, 0, 0), (DateTime)value);

            Console.WriteLine("DateTime strategy: Z offset={0}, +08:00 offset={1}, none offset={2}",
                utc.Offset, east.Offset, none.Offset?.ToString() ?? "null");
        }

        [Test]
        public void DateTokenSource_DateTimeOffset_PreservesExplicitOffset()
        {
            object value;

            JsonDateTokenSource utc = ReadSingleDateSource(UtcText, DateParseHandling.DateTimeOffset, out value);
            Assert.AreEqual(TimeSpan.Zero, utc.Offset);
            Assert.AreEqual(TimeSpan.Zero, ((DateTimeOffset)value).Offset);
            DateTime utcInstant = ((DateTimeOffset)value).UtcDateTime;

            JsonDateTokenSource east = ReadSingleDateSource(EastText, DateParseHandling.DateTimeOffset, out value);
            Assert.AreEqual(TimeSpan.FromHours(8), east.Offset);
            Assert.AreEqual(TimeSpan.FromHours(8), ((DateTimeOffset)value).Offset);

            // Z and +08:00 spellings are the same instant
            Assert.AreEqual(utcInstant, ((DateTimeOffset)value).UtcDateTime);

            JsonDateTokenSource none = ReadSingleDateSource(NoOffsetText, DateParseHandling.DateTimeOffset, out value);
            Assert.AreEqual(null, none.Offset);
            Assert.IsFalse(none.HasExplicitOffset);
            Assert.AreEqual(new DateTime(2000, 1, 1, 0, 0, 0), ((DateTimeOffset)value).DateTime);

            Console.WriteLine("DateTimeOffset strategy: Z offset={0}, +08:00 offset={1}, none offset={2}",
                utc.Offset, east.Offset, none.Offset?.ToString() ?? "null");
        }

        [Test]
        public void DateTokenSource_DateParseHandlingNone_NoSource()
        {
            JsonTextReader reader = CreateReader("[\"" + EastText + "\"]", DateParseHandling.None);
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());

            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual(EastText, reader.Value);
            Assert.IsNull(reader.DateTokenSource);
        }

        [Test]
        public void DateTokenSource_PlainText_NoSource()
        {
            JsonTextReader reader = CreateReader("[\"just some text\"]", DateParseHandling.DateTime);
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());

            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("just some text", reader.Value);
            // a non-date string must not fabricate an offset
            Assert.IsNull(reader.DateTokenSource);
        }

        [Test]
        public void DateTokenSource_MicrosoftDateFormat_PreservesExplicitOffset()
        {
            object value;

            JsonDateTokenSource withOffset = ReadSingleDateSource("/Date(1198908717056+0800)/", DateParseHandling.DateTime, out value);
            Assert.AreEqual(TimeSpan.FromHours(8), withOffset.Offset);
            Assert.AreEqual("/Date(1198908717056+0800)/", withOffset.Text);

            JsonDateTokenSource withoutOffset = ReadSingleDateSource("/Date(1198908717056)/", DateParseHandling.DateTime, out value);
            Assert.AreEqual(null, withoutOffset.Offset);
            Assert.IsFalse(withoutOffset.HasExplicitOffset);
        }

        [Test]
        public void DateTokenSource_ReadAsDateTime_PreservesExplicitOffset()
        {
            JsonTextReader reader = CreateReader("[\"" + EastText + "\"]", DateParseHandling.None);
            Assert.IsTrue(reader.Read());

            DateTime? dt = reader.ReadAsDateTime();
            Assert.IsTrue(dt != null);

            JsonDateTokenSource? source = reader.DateTokenSource;
            Assert.IsTrue(source.HasValue);
            Assert.AreEqual(EastText, source.Value.Text);
            Assert.AreEqual(TimeSpan.FromHours(8), source.Value.Offset);
        }

        [Test]
        public void DateTokenSource_SavedViewSurvivesReaderAdvancing()
        {
            JsonTextReader reader = CreateReader(
                "[\"" + EastText + "\",\"plain text\",\"" + UtcText + "\",123]",
                DateParseHandling.DateTime);

            Assert.IsTrue(reader.Read()); // [
            Assert.IsTrue(reader.Read()); // the +08:00 date

            JsonDateTokenSource saved = reader.DateTokenSource.Value;
            Assert.AreEqual(EastText, saved.Text);
            Assert.AreEqual(TimeSpan.FromHours(8), saved.Offset);

            Assert.IsTrue(reader.Read()); // plain text
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.IsNull(reader.DateTokenSource);

            Assert.IsTrue(reader.Read()); // the Z date
            JsonDateTokenSource? current = reader.DateTokenSource;
            Assert.IsTrue(current.HasValue);
            Assert.AreEqual(UtcText, current.Value.Text);
            Assert.AreEqual(TimeSpan.Zero, current.Value.Offset);

            Assert.IsTrue(reader.Read()); // 123
            Assert.IsNull(reader.DateTokenSource);
            Assert.IsTrue(reader.Read()); // ]

            // the view saved before the reader advanced is unchanged
            Assert.AreEqual(EastText, saved.Text);
            Assert.AreEqual(TimeSpan.FromHours(8), saved.Offset);
            Assert.IsTrue(saved.HasExplicitOffset);
        }

        [Test]
        public void DateTokenSource_TempFileFixture()
        {
            string path = Path.Combine(Path.GetTempPath(), "DateTokenSourceTests_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, "[\"" + EastText + "\"]");

                using (JsonTextReader reader = new JsonTextReader(new StreamReader(path)))
                {
                    Assert.IsTrue(reader.Read());
                    Assert.IsTrue(reader.Read());
                    Assert.AreEqual(JsonToken.Date, reader.TokenType);

                    JsonDateTokenSource? source = reader.DateTokenSource;
                    Assert.IsTrue(source.HasValue);
                    Assert.AreEqual(EastText, source.Value.Text);
                    Assert.AreEqual(TimeSpan.FromHours(8), source.Value.Offset);

                    Console.WriteLine("TempFile fixture: value={0:o}, source.Text={1}, source.Offset={2}",
                        reader.Value, source.Value.Text, source.Value.Offset);
                }
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
