using Splitio.Services.Client.Classes;
using Splitio.Services.Client.Interfaces;
using Splitio.Services.Logger;
using Splitio.Services.Shared.Classes;
using System;

namespace SplitOpenFeatureProvider
{
    public class SplitWrapper
    {
        ISplitClient splitClient;
        bool SDKReady = false;
        protected readonly ISplitLogger _log;
        public SplitWrapper(ISplitClient splitClient)
        {
            this.splitClient = splitClient;
        }

        public SplitWrapper(string ApiKey, ConfigurationOptions Configs, int ReadyBlockTime=10000) 
        {
            var factory = new SplitFactory(ApiKey, Configs);
            _log = WrapperAdapter.Instance().GetLogger(typeof(SplitWrapper));
            splitClient = (SplitClient)factory.Client();
            try
            {
                splitClient.BlockUntilReady(ReadyBlockTime);
                SDKReady = true;
            }
            catch (Exception ex)
            {
                LogIfNotNull($"Split SDK Not ready within {ReadyBlockTime} ms!");
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
                LogIfNotNull($"Split client is not ready");
            }
            return SDKReady;
        }

        private void LogIfNotNull(string message)
        {
            if (_log != null)
            {
                _log.Error(message);
            }
        }
    }
}
