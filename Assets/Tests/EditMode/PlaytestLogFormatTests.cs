using System.Globalization;
using NightPatrol.Debugging;
using NUnit.Framework;

namespace NightPatrol.Tests
{
    public class PlaytestLogFormatTests
    {
        [Test]
        public void CsvRow_PlainCells_JoinedWithCommas()
        {
            Assert.AreEqual("1,5,통과", PlaytestLog.CsvRow("1", "5", "통과"));
        }

        [Test]
        public void CsvRow_QuotesCellsWithCommaQuoteOrNewline()
        {
            Assert.AreEqual("\"a,b\",\"say \"\"hi\"\"\",\"x\ny\"", PlaytestLog.CsvRow("a,b", "say \"hi\"", "x\ny"));
        }

        [Test]
        public void CsvRow_NullOrEmptyCells_AreEmpty()
        {
            Assert.AreEqual("a,,", PlaytestLog.CsvRow("a", null, ""));
            Assert.AreEqual(string.Empty, PlaytestLog.CsvRow(null));
        }

        [Test]
        public void Seconds_OneDecimal_WithDotEvenOnCommaCultures()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");   // 이 언어 설정에서는 소수점이 쉼표
                Assert.AreEqual("12.3", PlaytestLog.Seconds(12.34f));
                Assert.AreEqual("0.0", PlaytestLog.Seconds(0f));
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [TestCase(0f, ExpectedResult = "0:00")]
        [TestCase(65.4f, ExpectedResult = "1:05")]
        [TestCase(600f, ExpectedResult = "10:00")]
        [TestCase(-3f, ExpectedResult = "0:00")]
        public string Clock_MinutesAndSeconds(float seconds) => PlaytestLog.Clock(seconds);
    }
}
