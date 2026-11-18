# Split OpenFeature Provider for .NET
[![Twitter Follow](https://img.shields.io/twitter/follow/splitsoftware.svg?style=social&label=Follow&maxAge=1529000)](https://twitter.com/intent/follow?screen_name=splitsoftware)

## Overview
This Provider is designed to allow the use of OpenFeature with Harness Feature Management & Experimentation, the platform for controlled rollouts, serving features to your users via the Split feature flag to manage your complete customer experience.

## Compatibility
This SDK is compatible with .NET 6.0 and higher.

## Getting started
Below is a simple example that describes the instantiation of the Split Provider. Please see the [OpenFeature Documentation](https://docs.openfeature.dev/docs/reference/concepts/evaluation-api) for details on how to use the OpenFeature SDK.

```c#
using OpenFeature;
using Splitio.OpenFeature;

Dictionary<string, object> initialContext = new Dictionary<string, object>();
var config = new ConfigurationOptions
{
    Logger = new CustomLogger()
};
initialContext.Add("ConfigOptions", config);
initialContext.Add("SdkKey", "SPLIT SDK API KEY");
initialContext.Add("ReadyBlockTime", 5000);

Api api = OpenFeature.Api.Instance;

api.setProviderAsync(new Provider(initialContext));
```

If you are more familiar with Split or want access to other initialization options, you can provide a `Split Client` to the constructor. See the [Split .NET Documentation](https://help.split.io/hc/en-us/articles/360020240172--NET-SDK) for more information.
```c#
using OpenFeature;
using Splitio.OpenFeature;
using Splitio.Services.Client.Classes

Api api = OpenFeature.Api.Instance;

var config = new ConfigurationOptions
{
   Ready = 10000
};
var splitClient = new SplitFactory("YOUR_API_KEY", config).Client();
Dictionary<string, object> initialContext = new Dictionary<string, object>();
initialContext.Add("SplitClient", splitClient);
api.SetProviderAsync(new Provider(initialContext));
```

## Use of OpenFeature with Split
After the initial setup you can use OpenFeature according to their [documentation](https://docs.openfeature.dev/docs/reference/concepts/evaluation-api/).

One important note is that the Split Provider **requires a targeting key** to be set. Often times this should be set when evaluating the value of a flag by [setting an EvaluationContext](https://docs.openfeature.dev/docs/reference/concepts/evaluation-context) which contains the targeting key. An example flag evaluation is
```csharp
var context = EvaluationContext.Builder().Set("targetingKey", "randomKey").Build();
var result = await client.GetBooleanValueAsync("boolFlag", false, context);
```
If the same targeting key is used repeatedly, the evaluation context may be set at the client level 
```csharp
var context = EvaluationContext.Builder().Set("targetingKey", "randomKey").Build();
client.SetContext(context)
```
or at the OpenFeatureAPI level 
```csharp
var context = EvaluationContext.Builder().Set("targetingKey", "randomKey").Build();
api.setEvaluationContext(context)
````
If the context was set at the client or api level, it is not required to provide it during flag evaluation.

### Bucketing key

Split can bucket on a key other than the targeting key (the Split *bucketing key*).
Set it through the reserved `bucketingKey` context attribute, next to `targetingKey` and `trafficType`:

```csharp
var context = EvaluationContext.Builder()
    .Set("targetingKey", "user-123")      // matching key: used by targeting rules, segments and individual targets
    .Set("bucketingKey", "account-42")    // bucketing key: used for the percentage split
    .Build();
var result = await client.GetBooleanValueAsync("boolFlag", false, context);
```

- `bucketingKey` must be a non-blank string; surrounding whitespace is trimmed. A blank or non-string value is ignored with a warning, and the flag is evaluated with the targeting key only.
- `bucketingKey` is reserved: it is **not** forwarded to Split as a targeting attribute.
- A targeting key is still required; `bucketingKey` alone returns `TARGETING_KEY_MISSING`.
- As with the targeting key, a value longer than 250 characters is rejected by the Split SDK: the evaluation returns the default value with `FLAG_NOT_FOUND` (`control`).
- `Track` is unchanged and sends only the targeting key.

## Submitting issues
 
The Split team monitors all issues submitted to this [issue tracker](https://github.com/splitio/split-openfeature-provider-dotnet/issues). We encourage you to use this issue tracker to submit any bug reports, feedback, and feature enhancements. We'll do our best to respond in a timely manner.

## Contributing
Please see [Contributors Guide](CONTRIBUTORS-GUIDE.md) to find all you need to submit a Pull Request (PR).

## License
Licensed under the Apache License, Version 2.0. See: [Apache License](http://www.apache.org/licenses/).
