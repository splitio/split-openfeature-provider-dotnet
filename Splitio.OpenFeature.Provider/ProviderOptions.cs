using System;
using Splitio.Services.Client.Classes;

namespace Splitio.OpenFeature.Provider
{
    public sealed class ProviderOptions
    {
        public string SdkKey { get; }
        public ConfigurationOptions Configuration { get; }
        public int ReadyBlockTime { get; }

        public ProviderOptions(
            string sdkKey,
            ConfigurationOptions configOptions = null,
            int readyBlockTime = Constants.DefaultReadyBlockTime)
        {
            SdkKey = sdkKey ?? throw new ArgumentNullException(nameof(sdkKey));
            Configuration = configOptions;
            ReadyBlockTime = readyBlockTime;
        }
    }
}