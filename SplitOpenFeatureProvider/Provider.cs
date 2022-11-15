using OpenFeature;
using OpenFeature.Model;
using OpenFeature.Constant;
using OpenFeature.Error;
using Splitio.Services.Client.Interfaces;

namespace Splitio.OpenFeature
{
    public class Provider : FeatureProvider
    {
        private readonly Metadata _metadata = new Metadata("Split Client");
        private ISplitClient _client;

        public Provider(ISplitClient client)
        {
            _client = client;
        }

        public override Metadata GetMetadata() => _metadata;

        public override Task<ResolutionDetails<bool>> ResolveBooleanValue(string flagKey, bool defaultValue,
            EvaluationContext context)
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

        public override Task<ResolutionDetails<string>> ResolveStringValue(string flagKey, string defaultValue,
            EvaluationContext context)
        {
            var key = GetTargetingKey(context);
            var evaluationResult = _client.GetTreatment(key, flagKey, TransformContext(context));
            if (evaluationResult == "control")
            {
                return Task.FromResult(new ResolutionDetails<string>(flagKey, defaultValue, variant: evaluationResult, errorType: ErrorType.FlagNotFound));
            }
            return Task.FromResult(new ResolutionDetails<string>(flagKey, evaluationResult ?? defaultValue, variant: evaluationResult, reason: Reason.TargetingMatch, errorType: ErrorType.None));
        }

        public override Task<ResolutionDetails<int>> ResolveIntegerValue(string flagKey, int defaultValue,
            EvaluationContext context)
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

        public override Task<ResolutionDetails<double>> ResolveDoubleValue(string flagKey, double defaultValue,
            EvaluationContext context)
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

        public override Task<ResolutionDetails<Value>> ResolveStructureValue(string flagKey, Value defaultValue, EvaluationContext context)
        {
            throw new NotImplementedException("Not implemented");
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