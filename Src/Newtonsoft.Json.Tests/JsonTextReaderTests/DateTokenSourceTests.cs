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
using Newtonsoft.Json;

namespace Newtonsoft.Json.Tests.JsonTextReaderTests
{
    [TestFixture]
#if !DNXCORE50
    [Category("JsonTextReaderTests")]
#endif
    public class DateTokenSourceTests : TestFixtureBase
    {
        // the same instant written with Z, with an explicit offset and with no offset
        private const string UtcDateText = "2012-03-21T05:40:00Z";
        private const string EastOfUtcDateText = "2012-03-21T13:40:00+08:00";
        private const string UnspecifiedDateText = "2012-03-21T05:40:00";

        private static readonly DateTime UtcDateTime = new DateTime(2012, 3, 21, 5, 40, 0, DateTimeKind.Utc);
        private static readonly TimeSpan EightHours = new TimeSpan(8, 0, 0);

        private static JsonTextReader CreateDateReader(string text, Json.DateParseHandling dateParseHandling)
        {
            JsonTextReader reader = new JsonTextReader(new StringReader("\"" + text + "\""));
            reader.DateParseHandling = dateParseHandling;
            return reader;
        }

        [Test]
        public void DateTimeParseHandling_PreservesExplicitOffset()
        {
            // Z suffix
            JsonTextReader reader = CreateDateReader(UtcDateText, Json.DateParseHandling.DateTime);
            reader.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Date, reader.TokenType);
            Assert.AreEqual(UtcDateTime, reader.Value);

            JsonDateTokenSource source = reader.DateTokenSource;
            Assert.IsNotNull(source);
            Assert.AreEqual(UtcDateText, source.Text);
            Assert.IsTrue(source.HasExplicitOffset);
            Assert.AreEqual(TimeSpan.Zero, source.Offset);

            // explicit offset, same instant
            reader = CreateDateReader(EastOfUtcDateText, Json.DateParseHandling.DateTime);
            reader.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Date, reader.TokenType);
            Assert.AreEqual(UtcDateTime, reader.Value);

            source = reader.DateTokenSource;
            Assert.IsNotNull(source);
            Assert.AreEqual(EastOfUtcDateText, source.Text);
            Assert.IsTrue(source.HasExplicitOffset);
            Assert.AreEqual(EightHours, source.Offset);

            // no offset
            reader = CreateDateReader(UnspecifiedDateText, Json.DateParseHandling.DateTime);
            reader.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Date, reader.TokenType);
            Assert.AreEqual(UtcDateTime, reader.Value);

            source = reader.DateTokenSource;
            Assert.IsNotNull(source);
            Assert.AreEqual(UnspecifiedDateText, source.Text);
            Assert.IsFalse(source.HasExplicitOffset);
            Assert.IsNull(source.Offset);
        }

        [Test]
        public void DateTimeParseHandling_DefaultRoundtripKind_SameInstant()
        {
            // default DateTimeZoneHandling.RoundtripKind converts the explicit offset to local time;
            // compare as instants so the check does not depend on the machine time zone
            JsonTextReader reader = CreateDateReader(EastOfUtcDateText, Json.DateParseHandling.DateTime);

            Assert.IsTrue(reader.Read());
            DateTime value = (DateTime)reader.Value;
            Assert.AreEqual(UtcDateTime, value.ToUniversalTime());

            JsonDateTokenSource source = reader.DateTokenSource;
            Assert.IsNotNull(source);
            Assert.IsTrue(source.HasExplicitOffset);
            Assert.AreEqual(EightHours, source.Offset);

            reader = CreateDateReader(UtcDateText, Json.DateParseHandling.DateTime);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(UtcDateTime, reader.Value);
            Assert.AreEqual(TimeSpan.Zero, reader.DateTokenSource.Offset);
        }

#if !NET20
        [Test]
        public void DateTimeOffsetParseHandling_PreservesExplicitOffset()
        {
            // Z suffix
            JsonTextReader reader = CreateDateReader(UtcDateText, Json.DateParseHandling.DateTimeOffset);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Date, reader.TokenType);
            Assert.AreEqual(new DateTimeOffset(UtcDateTime, TimeSpan.Zero), reader.Value);

            JsonDateTokenSource source = reader.DateTokenSource;
            Assert.IsNotNull(source);
            Assert.AreEqual(UtcDateText, source.Text);
            Assert.IsTrue(source.HasExplicitOffset);
            Assert.AreEqual(TimeSpan.Zero, source.Offset);

            // explicit offset, same instant
            reader = CreateDateReader(EastOfUtcDateText, Json.DateParseHandling.DateTimeOffset);

            Assert.IsTrue(reader.Read());
            DateTimeOffset offsetValue = (DateTimeOffset)reader.Value;
            Assert.AreEqual(EightHours, offsetValue.Offset);
            Assert.AreEqual(UtcDateTime, offsetValue.UtcDateTime);

            source = reader.DateTokenSource;
            Assert.IsNotNull(source);
            Assert.AreEqual(EastOfUtcDateText, source.Text);
            Assert.IsTrue(source.HasExplicitOffset);
            Assert.AreEqual(EightHours, source.Offset);

            // no offset: the value picks up the local offset but the source view must not invent one
            reader = CreateDateReader(UnspecifiedDateText, Json.DateParseHandling.DateTimeOffset);

            Assert.IsTrue(reader.Read());
            offsetValue = (DateTimeOffset)reader.Value;
            Assert.AreEqual(TimeZoneInfo.Local.GetUtcOffset(offsetValue.DateTime), offsetValue.Offset);

            source = reader.DateTokenSource;
            Assert.IsNotNull(source);
            Assert.AreEqual(UnspecifiedDateText, source.Text);
            Assert.IsFalse(source.HasExplicitOffset);
            Assert.IsNull(source.Offset);
        }
#endif

        [Test]
        public void NoneDateParseHandling_NoSource()
        {
            JsonTextReader reader = CreateDateReader(UtcDateText, Json.DateParseHandling.None);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual(UtcDateText, reader.Value);
            Assert.IsNull(reader.DateTokenSource);
        }

        [Test]
        public void NonDateText_NoSource()
        {
            // plain text
            JsonTextReader reader = new JsonTextReader(new StringReader("\"not a date\""));

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.IsNull(reader.DateTokenSource);

            // looks like a date but is not valid; no offset may be fabricated
            reader = new JsonTextReader(new StringReader("\"2012-13-21T05:40:00Z\""));

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("2012-13-21T05:40:00Z", reader.Value);
            Assert.IsNull(reader.DateTokenSource);

            // non-string token
            reader = new JsonTextReader(new StringReader("123"));

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.IsNull(reader.DateTokenSource);
        }

        [Test]
        public void SourceViewSurvivesReaderAdvance()
        {
            JsonTextReader reader = new JsonTextReader(new StringReader("[\"" + EastOfUtcDateText + "\",\"hello\"]"));

            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Date, reader.TokenType);

            JsonDateTokenSource source = reader.DateTokenSource;
            Assert.IsNotNull(source);
            Assert.AreEqual(EastOfUtcDateText, source.Text);
            Assert.AreEqual(EightHours, source.Offset);

            // advance past the date token; the reader no longer exposes the old view
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.IsNull(reader.DateTokenSource);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);
            Assert.IsNull(reader.DateTokenSource);

            // the previously captured view is independent of the reader
            Assert.AreEqual(EastOfUtcDateText, source.Text);
            Assert.IsTrue(source.HasExplicitOffset);
            Assert.AreEqual(EightHours, source.Offset);
            Assert.AreEqual(EastOfUtcDateText, source.ToString());
        }

        [Test]
        public void MicrosoftDate_PreservesExplicitOffset()
        {
            JsonTextReader reader = new JsonTextReader(new StringReader(@"""\/Date(1198908717056+0800)\/"""));

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Date, reader.TokenType);

            JsonDateTokenSource source = reader.DateTokenSource;
            Assert.IsNotNull(source);
            Assert.AreEqual("/Date(1198908717056+0800)/", source.Text);
            Assert.IsTrue(source.HasExplicitOffset);
            Assert.AreEqual(EightHours, source.Offset);

            // no offset in the source text
            reader = new JsonTextReader(new StringReader(@"""\/Date(1198908717056)\/"""));

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Date, reader.TokenType);

            source = reader.DateTokenSource;
            Assert.IsNotNull(source);
            Assert.AreEqual("/Date(1198908717056)/", source.Text);
            Assert.IsFalse(source.HasExplicitOffset);
            Assert.IsNull(source.Offset);
        }

#if !NET20
        [Test]
        public void ReadAsDateTimeOffset_PreservesExplicitOffset()
        {
            JsonTextReader reader = CreateDateReader(EastOfUtcDateText, Json.DateParseHandling.None);

            DateTimeOffset? value = reader.ReadAsDateTimeOffset();
            Assert.IsTrue(value != null);
            Assert.AreEqual(EightHours, value.Value.Offset);
            Assert.AreEqual(UtcDateTime, value.Value.UtcDateTime);

            JsonDateTokenSource source = reader.DateTokenSource;
            Assert.IsNotNull(source);
            Assert.AreEqual(EastOfUtcDateText, source.Text);
            Assert.IsTrue(source.HasExplicitOffset);
            Assert.AreEqual(EightHours, source.Offset);
        }
#endif

        [Test]
        public void ReadAsDateTime_PreservesExplicitOffset()
        {
            JsonTextReader reader = CreateDateReader(EastOfUtcDateText, Json.DateParseHandling.None);
            reader.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

            DateTime? value = reader.ReadAsDateTime();
            Assert.IsTrue(value != null);
            Assert.AreEqual(UtcDateTime, value.Value);

            JsonDateTokenSource source = reader.DateTokenSource;
            Assert.IsNotNull(source);
            Assert.AreEqual(EastOfUtcDateText, source.Text);
            Assert.IsTrue(source.HasExplicitOffset);
            Assert.AreEqual(EightHours, source.Offset);
        }

        [Test]
        public void TempFileFixture_PreservesExplicitOffset()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "[\"" + UtcDateText + "\",\"" + EastOfUtcDateText + "\",\"" + UnspecifiedDateText + "\"]");

                using (StreamReader streamReader = new StreamReader(path))
                using (JsonTextReader reader = new JsonTextReader(streamReader))
                {
                    reader.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

                    Assert.IsTrue(reader.Read());

                    Assert.IsTrue(reader.Read());
                    Assert.AreEqual(UtcDateTime, reader.Value);
                    Assert.AreEqual(TimeSpan.Zero, reader.DateTokenSource.Offset);

                    Assert.IsTrue(reader.Read());
                    Assert.AreEqual(UtcDateTime, reader.Value);
                    Assert.AreEqual(EightHours, reader.DateTokenSource.Offset);

                    Assert.IsTrue(reader.Read());
                    Assert.AreEqual(UtcDateTime, reader.Value);
                    Assert.IsFalse(reader.DateTokenSource.HasExplicitOffset);

                    Assert.IsTrue(reader.Read());
                    Assert.AreEqual(JsonToken.EndArray, reader.TokenType);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
