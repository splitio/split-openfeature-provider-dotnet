using Splitio.Services.Client.Classes;
using Splitio.Services.Client.Interfaces;

namespace SplitOpenFeatureProvider
{
    internal class SplitWrapper
    {
        ISplitClient splitClient;
        bool SDKReady = false;

        public SplitWrapper(ISplitClient splitClient)
        {
            this.splitClient = splitClient;
        }

        public SplitWrapper(string ApiKey, ConfigurationOptions Configs) 
        {
            var factory = new SplitFactory(ApiKey, Configs);
            splitClient = (SplitClient)factory.Client();
            try
            {
                splitClient.BlockUntilReady(5000);
                SDKReady = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception initializing Split client! {ex}");
            }
        }

        public ISplitClient getSplitClient() 
        { 
            return splitClient; 
        }

        public bool IsSDKReady()
        {
            if (SDKReady) return true;

            try
            {
                splitClient.BlockUntilReady(1);
                SDKReady = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Split client is not ready");
            }
            return SDKReady;
        }
    }
}
