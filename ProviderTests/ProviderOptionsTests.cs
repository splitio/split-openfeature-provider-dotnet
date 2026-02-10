using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Splitio.OpenFeature.Provider;

namespace ProviderTests
{
    [TestClass]
    public class ProviderOptionsTests
    {
        [TestMethod]
        public void ProviderOptionsConstructorThrowsWhenSdkKeyIsNull()
        {
            // arrange, act + assert
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                new ProviderOptions(
                    null);
            });
        }
    }
}