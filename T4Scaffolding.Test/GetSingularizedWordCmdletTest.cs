using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using T4Scaffolding.Cmdlets;
using T4Scaffolding.Test.TestUtils;

namespace T4Scaffolding.Test
{
    [TestClass]
    public class GetSingularizedWordCmdletTest
    {
        [TestMethod]
        public void ShouldReturnSingularizedWordForSupportedCulture()
        {
            var testCases = new Dictionary<string, string> {
                { "cats", "cat" },
                { "Cities", "City" },
            };

            foreach (var testCase in testCases) {
                // Act
                var result = new GetSingularizedWordCmdlet {
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
            var result = new GetSingularizedWordCmdlet {
                Word = "anything",
                Culture = "uz-Cyrl-UZ" // Arbitrary CultureInfo name that's not yet supported for pluralization
            }.GetResults<string>();

            // Assert
            Assert.AreEqual("anything", result.Single());
        }
    }
}
