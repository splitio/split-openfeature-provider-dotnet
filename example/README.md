# Split OpenFeature Provider – Examples

This folder contains a console application that demonstrates two ways to use the Split OpenFeature Provider for .NET.

## Requirements

- .NET 9.0 SDK
- A Split API key (SDK key)

## Configuration

Set the `SPLIT_API_KEY` environment variable to your Split SDK key:

```bash
# Linux / macOS
export SPLIT_API_KEY=your_split_sdk_api_key

# Windows (PowerShell)
$env:SPLIT_API_KEY = "your_split_sdk_api_key"
```

## Running the example

From the repository root:

```bash
dotnet run --project example/SplitOpenFeature.Example/SplitOpenFeature.Example.csproj
```

By default both examples run. To run only one:

```bash
# API key example only
dotnet run --project example/SplitOpenFeature.Example/SplitOpenFeature.Example.csproj -- apikey

# Injected ISplitClient example only
dotnet run --project example/SplitOpenFeature.Example/SplitOpenFeature.Example.csproj -- client
```

## Included examples

### 1. Provider with API key

The provider is created by passing only the API key. The Split SDK is initialized internally with default configuration and blocks until ready (10 second timeout).

```csharp
var provider = new Provider(apiKey);
await Api.Instance.SetProviderAsync(provider);
```

### 2. Provider with injected ISplitClient

The Split client is created with `SplitFactory` and configured as needed (timeout, refresh, logger, etc.). The client is then passed to the provider.

```csharp
var config = new ConfigurationOptions();
var factory = new SplitFactory(apiKey, config);
ISplitClient splitClient = factory.Client();
splitClient.BlockUntilReady(10000);

var provider = new Provider(splitClient);
await Api.Instance.SetProviderAsync(provider);
```

Both examples evaluate the `my_feature` flag with a sample targeting key and print the value and details to the console.
