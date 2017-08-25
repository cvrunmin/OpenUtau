using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenUtau.Core.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenUtau.Core.Util.Tests
{
    [TestClass()]
    public class LyricsHelperTests
    {
        [TestMethod()]
        public void GetVowelTest()
        {
            Assert.AreEqual("ung", LyricsHelper.GetVowel("cung"));
            Assert.AreEqual("a", LyricsHelper.GetVowel("a"));
            Assert.AreEqual("eu", LyricsHelper.GetVowel("deu"));
            Assert.AreEqual("aat", LyricsHelper.GetVowel("jaat"));
            Assert.AreEqual("a", LyricsHelper.GetVowel("きゃ"));
            Assert.AreEqual("n", LyricsHelper.GetVowel("ん"));
            Assert.AreEqual("ong", LyricsHelper.GetVowel("cong"));
        }
    }
}