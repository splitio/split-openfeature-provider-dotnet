using OpenFeature;
using OpenFeature.Model;
using OpenFeature.Constant;
using OpenFeature.Error;
using Splitio.Services.Client.Interfaces;
using Newtonsoft.Json.Linq;

namespace Splitio.OpenFeature
{
    public class Provider : FeatureProvider
    {
        private readonly Metadata _metadata = new("Split Client");
        private readonly ISplitClient _client;

        public Provider(ISplitClient client)
        {
            _client = client;
        }

        public override Metadata GetMetadata() => _metadata;

        public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            var key = GetTargetingKey(context);
            var originalResult = _client.GetTreatment(key, flagKey, TransformContext(context));
            if (originalResult == "control")
            {
                return Task.FromResult(new ResolutionDetails<bool>(flagKey, defaultValue, variant: originalResult, errorType: ErrorType.FlagNotFound));
            }
            var evaluationResult = ParseBoolean(originalResult);
            return Task.FromResult(new ResolutionDetails<bool>(flagKey, evaluationResult, errorType: ErrorType.None, variant: originalResult, reason: Reason.TargetingMatch));
        }

        public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            var key = GetTargetingKey(context);
            var evaluationResult = _client.GetTreatment(key, flagKey, TransformContext(context));
            if (evaluationResult == "control")
            {
                return Task.FromResult(new ResolutionDetails<string>(flagKey, defaultValue, variant: evaluationResult, errorType: ErrorType.FlagNotFound));
            }
            return Task.FromResult(new ResolutionDetails<string>(flagKey, evaluationResult ?? defaultValue, variant: evaluationResult, reason: Reason.TargetingMatch, errorType: ErrorType.None));
        }

        public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            var key = GetTargetingKey(context);
            var originalResult = _client.GetTreatment(key, flagKey, TransformContext(context));
            if (originalResult == "control")
            {
                return Task.FromResult(new ResolutionDetails<int>(flagKey, defaultValue, variant: originalResult, errorType: ErrorType.FlagNotFound));
            }
            try {
                var evaluationResult = int.Parse(originalResult);
                return Task.FromResult(new ResolutionDetails<int>(flagKey, evaluationResult, variant: originalResult, reason: Reason.TargetingMatch, errorType: ErrorType.None));
            }
            catch (FormatException)
            {
                throw new FeatureProviderException(ErrorType.ParseError, $"{originalResult} is not an int");
            };
        }

        public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            var key = GetTargetingKey(context);
            var originalResult = _client.GetTreatment(key, flagKey, TransformContext(context));
            if (originalResult == "control")
            {
                return Task.FromResult(new ResolutionDetails<double>(flagKey, defaultValue, variant: originalResult, errorType: ErrorType.FlagNotFound));
            }
            try
            {
                var evaluationResult = double.Parse(originalResult);
                return Task.FromResult(new ResolutionDetails<double>(flagKey, evaluationResult, variant: originalResult, reason: Reason.TargetingMatch, errorType: ErrorType.None));
            }
            catch (FormatException)
            {
                throw new FeatureProviderException(ErrorType.ParseError, $"{originalResult} is not a double");
            }
        }

        public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            var key = GetTargetingKey(context);
            var originalResult = _client.GetTreatmentWithConfig(key, flagKey, TransformContext(context));
            if (originalResult.Treatment == "control")
            {
                return Task.FromResult(new ResolutionDetails<Value>(flagKey, defaultValue, variant: originalResult.Treatment, errorType: ErrorType.FlagNotFound));
            }
            try {
                var jsonString = originalResult.Config;
                var dict = JObject.Parse(jsonString).ToObject<Dictionary<string, string>>();
                if (dict == null)
                {
                    throw new FeatureProviderException(ErrorType.ParseError, $"{originalResult.Config} is not an object");
                }
                var dict2 = dict.ToDictionary(x => x.Key, x => new Value(x.Value));
                var dictValue = new Value(new Structure(dict2));
                return Task.FromResult(new ResolutionDetails<Value>(flagKey, dictValue, variant: originalResult.Treatment, reason: Reason.TargetingMatch, errorType: ErrorType.None));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception parsing JSON: {ex}");
                Console.WriteLine($"Attempted to parse: {originalResult.Config}");
                throw new FeatureProviderException(ErrorType.ParseError, $"{originalResult.Config} is not an object");
            }
        }

        private static string GetTargetingKey(EvaluationContext context)
        {
            Value key;
            if (!context.TryGetValue("targetingKey", out key))
            {
                Console.WriteLine("Split provider: targeting key missing!");
                throw new FeatureProviderException(ErrorType.TargetingKeyMissing, "Split provider requires a userkey");
            }
            return key.AsString;
        }

        private static Dictionary<string, object> TransformContext(EvaluationContext context)
        {
            return context == null
                ? new Dictionary<string, object>()
                : context.AsDictionary().ToDictionary(x => x.Key, x => x.Value.AsObject);
        }

        private static bool ParseBoolean(string boolStr)
        {
            return boolStr.ToLower() switch
            {
                "on" or "true" => true,
                "off" or "false" => false,
                _ => throw new FeatureProviderException(ErrorType.ParseError, $"{boolStr} is not a boolean"),
            };
        }
    }
}