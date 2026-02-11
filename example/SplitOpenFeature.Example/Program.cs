using OpenFeature;
using OpenFeature.Model;
using Splitio.OpenFeature.Provider;
using Splitio.Services.Client.Classes;
using Splitio.Services.Client.Interfaces;

namespace SplitOpenFeature.Example;

/// <summary>
/// Console app demonstrating two ways to use the Split OpenFeature Provider:
/// 1) With an API key (provider creates the SDK internally).
/// 2) With an injected ISplitClient (you control SDK configuration).
/// Set SPLIT_API_KEY environment variable to your SDK key before running.
/// </summary>
public static class Program
{
    private const string ApiKeyEnvVar = "SPLIT_API_KEY";
    private const string FlagKey = "my_feature";
    private const string TargetingKey = "example-user";

    public static async Task Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine($"Set {ApiKeyEnvVar} to your Split SDK API key and run again.");
            Console.WriteLine("Example: export SPLIT_API_KEY=your_api_key");
            return;
        }

        var runApiKeyExample = args.Length == 0 || args.Contains("apikey", StringComparer.OrdinalIgnoreCase);
        var runClientExample = args.Length == 0 || args.Contains("client", StringComparer.OrdinalIgnoreCase);

        if (runApiKeyExample)
            await RunWithApiKeyAsync(apiKey);

        if (runClientExample)
            await RunWithSplitClientAsync(apiKey);
    }

    /// <summary>
    /// Example 1: Create the provider with only the API key.
    /// The provider initializes the Split SDK internally with default config and blocks until ready (10s).
    /// </summary>
    private static async Task RunWithApiKeyAsync(string apiKey)
    {
        Console.WriteLine("=== Example 1: Provider with API key ===");
        Console.WriteLine("Creating provider with Provider(apiKey)...");

        var provider = new Provider(apiKey);
        await Api.Instance.SetProviderAsync(provider);

        await EvaluateFlagAsync("API key");
        await Api.Instance.ShutdownAsync();
    }

    /// <summary>
    /// Example 2: Create the Split client yourself and inject it into the provider.
    /// Use this when you need custom SDK configuration (timeout, refresh rate, logger, etc.).
    /// </summary>
    private static async Task RunWithSplitClientAsync(string apiKey)
    {
        Console.WriteLine("=== Example 2: Provider with injected ISplitClient ===");
        Console.WriteLine("Creating SplitFactory and client, then Provider(splitClient)...");

        var config = new ConfigurationOptions()};

        var factory = new SplitFactory(apiKey, config);
        ISplitClient splitClient = factory.Client();

        try
        {
            splitClient.BlockUntilReady(10000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Split client not ready: {ex.Message}");
            splitClient.Destroy();
            return;
        }

        var provider = new Provider(splitClient);
        await Api.Instance.SetProviderAsync(provider);

        await EvaluateFlagAsync("ISplitClient");

        await Api.Instance.ShutdownAsync();
        splitClient.Destroy();
    }

    private static async Task EvaluateFlagAsync(string exampleLabel)
    {
        var client = Api.Instance.GetClient();
        var context = EvaluationContext.Builder()
            .Set("targetingKey", TargetingKey)
            .Build();
        client.SetContext(context);

        var value = await client.GetBooleanValueAsync(FlagKey, false);
        var details = await client.GetBooleanDetailsAsync(FlagKey, false);

        Console.WriteLine($"[{exampleLabel}] Flag '{FlagKey}' (targeting key: {TargetingKey}):");
        Console.WriteLine($"  Value: {value}");
        Console.WriteLine($"  Variant: {details.Variant}, Reason: {details.Reason}, Config: {details?.FlagMetadata?.GetString("config")}");
    }
}
