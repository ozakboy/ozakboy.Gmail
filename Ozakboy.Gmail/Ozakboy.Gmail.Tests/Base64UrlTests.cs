using System;
using System.Text;
using Ozakboy.Gmail.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ozakboy.Gmail.Tests
{
    /// <summary>
    /// base64url 編解碼測試,涵蓋補回填補與 '-' / '_' 轉換。
    /// </summary>
    [TestClass]
    public class Base64UrlTests
    {
        [TestMethod]
        public void Decode_沒有填補時會自動補回()
        {
            Assert.AreEqual("hello", Encoding.UTF8.GetString(Base64Url.Decode("aGVsbG8")));
            Assert.AreEqual("hell", Encoding.UTF8.GetString(Base64Url.Decode("aGVsbA")));
            Assert.AreEqual("hel", Encoding.UTF8.GetString(Base64Url.Decode("aGVs")));
        }

        [TestMethod]
        public void Decode_已帶填補時也能解()
        {
            Assert.AreEqual("hello", Encoding.UTF8.GetString(Base64Url.Decode("aGVsbG8=")));
        }

        [TestMethod]
        public void Decode_把減號與底線換回加號與斜線()
        {
            var bytes = new byte[] { 0xFB, 0xFF, 0xBF };
            var encoded = Convert.ToBase64String(bytes);

            Assert.Contains("+", encoded, StringComparison.Ordinal);
            Assert.Contains("/", encoded, StringComparison.Ordinal);
            CollectionAssert.AreEqual(bytes, Base64Url.Decode(encoded.Replace('+', '-').Replace('/', '_')));
        }

        [TestMethod]
        public void Decode_空字串回傳空陣列()
        {
            Assert.IsEmpty(Base64Url.Decode(string.Empty));
        }

        [TestMethod]
        public void Decode_內容不合法_拋出FormatException()
        {
            Assert.ThrowsExactly<FormatException>(() => Base64Url.Decode("@@@@"));
        }

        [TestMethod]
        public void Encode_不帶填補且使用url安全字元()
        {
            var encoded = Base64Url.Encode(new byte[] { 0xFB, 0xFF, 0xBF });

            Assert.DoesNotContain("=", encoded, StringComparison.Ordinal);
            Assert.DoesNotContain("+", encoded, StringComparison.Ordinal);
            Assert.DoesNotContain("/", encoded, StringComparison.Ordinal);
        }

        [TestMethod]
        public void Encode與Decode_可以來回轉換中文內容()
        {
            var original = Encoding.UTF8.GetBytes("Gmail 附件內容 測試");

            CollectionAssert.AreEqual(original, Base64Url.Decode(Base64Url.Encode(original)));
        }
    }
}
