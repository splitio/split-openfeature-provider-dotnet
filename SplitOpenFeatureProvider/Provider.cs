using OpenFeature;
using OpenFeature.Model;
using OpenFeature.Constant;
using Splitio.Services.Client.Interfaces;
using Newtonsoft.Json.Linq;

namespace Splitio.OpenFeature
{
    public class Provider : FeatureProvider
    {
        private readonly Metadata _metadata = new("Split Client");
        private readonly ISplitClient _client;
        private readonly String CONTROL = "control";

        public Provider(ISplitClient client)
        {
            _client = client;
        }

        public override Metadata GetMetadata() => _metadata;

        public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            var key = GetTargetingKey(context);
            if (key == null)
            {
                return Task.FromResult(KeyNotFound<bool>(flagKey, defaultValue));
            }

            var originalResult = _client.GetTreatment(key, flagKey, TransformContext(context));
            if (originalResult == CONTROL)
            {
                return Task.FromResult(FlagNotFound<bool>(flagKey, defaultValue));
            }
            try
            {
                var evaluationResult = ParseBoolean(originalResult);
                return Task.FromResult(new ResolutionDetails<bool>(flagKey, evaluationResult, errorType: ErrorType.None, variant: originalResult, reason: Reason.TargetingMatch));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {originalResult} is not a bool");
                return Task.FromResult(ParseError<bool>(flagKey, defaultValue));
            }
        }

        public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            var key = GetTargetingKey(context);
            if (key == null)
            {
                return Task.FromResult(KeyNotFound<string>(flagKey, defaultValue));
            }

            var evaluationResult = _client.GetTreatment(key, flagKey, TransformContext(context));
            if (evaluationResult == CONTROL)
            {
                return Task.FromResult(FlagNotFound<string>(flagKey, defaultValue));
            }
            return Task.FromResult(new ResolutionDetails<string>(
                flagKey, 
                evaluationResult ?? defaultValue, 
                variant: evaluationResult, 
                reason: Reason.TargetingMatch, 
                errorType: ErrorType.None));
        }

        public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            var key = GetTargetingKey(context);
            if (key == null)
            {
                return Task.FromResult(KeyNotFound<int>(flagKey, defaultValue));
            }

            var originalResult = _client.GetTreatment(key, flagKey, TransformContext(context));
            if (originalResult == CONTROL)
            {
                return Task.FromResult(FlagNotFound<int>(flagKey, defaultValue));
            }

            try {
                var evaluationResult = int.Parse(originalResult);
                return Task.FromResult(new ResolutionDetails<int>(
                    flagKey, 
                    evaluationResult, 
                    variant: originalResult, 
                    reason: Reason.TargetingMatch, 
                    errorType: ErrorType.None));
            }
            catch (FormatException)
            {
                Console.WriteLine($"Exception: {originalResult} is not a int");
                return Task.FromResult(ParseError<int>(flagKey, defaultValue));
            }
            ;
        }

        public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            var key = GetTargetingKey(context);
            if (key == null)
            {
                return Task.FromResult(KeyNotFound<double>(flagKey, defaultValue));
            }

            var originalResult = _client.GetTreatment(key, flagKey, TransformContext(context));
            if (originalResult == CONTROL)
            {
                return Task.FromResult(FlagNotFound<double>(flagKey, defaultValue));
            }

            try
            {
                var evaluationResult = double.Parse(originalResult);
                return Task.FromResult(new ResolutionDetails<double>(
                    flagKey, 
                    evaluationResult,
                    variant: originalResult,
                    reason: Reason.TargetingMatch,
                    errorType: ErrorType.None));
            }
            catch (FormatException)
            {
                Console.WriteLine($"Exception: {originalResult} is not a double");
                return Task.FromResult(ParseError<double>(flagKey, defaultValue));
            }
        }

        public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            var key = GetTargetingKey(context);
            if (key == null)
            {
                return Task.FromResult(KeyNotFound<Value>(flagKey, defaultValue));
            }

            var originalResult = _client.GetTreatmentWithConfig(key, flagKey, TransformContext(context));
            if (originalResult.Treatment == CONTROL)
            {
                return Task.FromResult(FlagNotFound<Value>(flagKey, defaultValue)); 
            }

            try {
                var jsonString = originalResult.Config;
                var dict = JObject.Parse(jsonString).ToObject<Dictionary<string, string>>();
                if (dict == null)
                {
                    Console.WriteLine($"Exception: {originalResult} is not a Json");
                    return Task.FromResult(ParseError<Value>(flagKey, defaultValue));
                }

                var dict2 = dict.ToDictionary(x => x.Key, x => new Value(x.Value));
                var dictValue = new Value(new Structure(dict2));
                return Task.FromResult(new ResolutionDetails<Value>(
                    flagKey, 
                    dictValue, 
                    variant: originalResult.Treatment, 
                    reason: Reason.TargetingMatch, 
                    errorType: ErrorType.None));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception parsing JSON: {ex}");
                Console.WriteLine($"Attempted to parse: {originalResult.Config}");
                Console.WriteLine($"Exception: {originalResult} is not a double");
                return Task.FromResult(ParseError<Value>(flagKey, defaultValue));
            }
        }

        private ResolutionDetails<T> KeyNotFound<T>(string flagKey, T defaultValue)
        {
            return new ResolutionDetails<T>(
                                flagKey,
                                defaultValue,
                                variant: CONTROL,
                                reason: Reason.Error,
                                errorType: ErrorType.TargetingKeyMissing);
        }

        private ResolutionDetails<T> ParseError<T>(string flagKey, T defaultValue)
        {
            return new ResolutionDetails<T>(
                                flagKey,
                                defaultValue,
                                variant: CONTROL,
                                reason: Reason.Error,
                                errorType: ErrorType.ParseError);
        }

        private ResolutionDetails<T> FlagNotFound<T>(string flagKey, T defaultValue)
        {
            return new ResolutionDetails<T>(
                                flagKey,
                                defaultValue,
                                variant: CONTROL,
                                reason: Reason.Error,
                                errorType: ErrorType.FlagNotFound);
        }

        private static string GetTargetingKey(EvaluationContext context)
        {
            Value key;
            if (!context.TryGetValue("targetingKey", out key))
            {
                Console.WriteLine("Split provider: targeting key missing!");
                return null;
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
                "off" or "false" => false
            };
        }       
    }
}