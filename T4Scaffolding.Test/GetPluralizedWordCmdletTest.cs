using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class GetPluralizedWordCmdletTest
    {
        [TestMethod]
        public void ShouldReturnPluralizedWordForSupportedCulture()
        {
            var testCases = new Dictionary<string, string> {
                { "cat", "cats" },
                { "City", "Cities" },
            };

            foreach (var testCase in testCases) {
                // Act
                var result = new GetPluralizedWordCmdlet {
                    Word = testCase.Key,
                    Culture = "en-US"
                }.GetResults<string>();

                // Assert
                Assert.AreEqual(testCase.Value, result.Single());
            }
        }

        [TestMethod]
        public void ShouldReturnUnchangedWordForUnsupportedCulture()
        {
            // Act
            var result = new GetPluralizedWordCmdlet {
                Word = "anything",
                Culture = "uz-Cyrl-UZ" // Arbitrary CultureInfo name that's not yet supported for pluralization
            }.GetResults<string>();

            // Assert
            Assert.AreEqual("anything", result.Single());
        }
    }
}
