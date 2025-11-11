using Microsoft.VisualStudio.TestTools.UnitTesting;
using Splitio.Services.Client.Classes;
using Splitio.Services.Client.Interfaces;
using Splitio.OpenFeature.Provider;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace ProviderTests
{
    [TestClass]
    public class SplitWrapperTests
    {
        [TestMethod]
        public void InitializeSDKTest()
        {
            var config = new ConfigurationOptions
            {
                LocalhostFilePath = "../../../split.yaml",
                Logger = new CustomLogger()
            };
            SplitWrapper splitWrapper = new SplitWrapper("localhost", config);
            Assert.IsNotNull(splitWrapper);
            Assert.IsNotNull(splitWrapper.getSplitClient());
            Assert.IsTrue(splitWrapper.IsSDKReady());
            splitWrapper.getSplitClient().Destroy();
        }

        [TestMethod]
        public void PassSplitClientTest()
        {
            var config = new ConfigurationOptions
            {
                LocalhostFilePath = "../../../split.yaml",
                Logger = new CustomLogger()
            };
            var factory = new SplitFactory("localhost", config);
            ISplitClient splitClient = (SplitClient)factory.Client();
            try
            {
                splitClient.BlockUntilReady(1000);
            }
            catch (Exception) {}

            SplitWrapper splitWrapper = new SplitWrapper(splitClient);
            Assert.IsNotNull(splitWrapper);
            Assert.AreEqual(splitClient, splitWrapper.getSplitClient());
            Assert.IsTrue(splitWrapper.IsSDKReady());
            splitWrapper.getSplitClient().Destroy();
        }

        [TestMethod]
        public void SetReadyTimeoutTest()
        {
            StringWriter sw = new StringWriter();
            Console.SetOut(sw);

            var config = new ConfigurationOptions
            {
                Logger = new CustomLogger()
            };
            SplitWrapper splitWrapper = new SplitWrapper("sdkapi", config, 10);
            Thread.Sleep(1000);

            string capturedOutput = sw.ToString();
            Assert.IsTrue(capturedOutput.Contains("Split SDK Not ready within 10 ms"));

            Assert.IsNotNull(splitWrapper);
            Assert.IsFalse(splitWrapper.IsSDKReady());

            splitWrapper.getSplitClient().Destroy();
        }
    }
}
