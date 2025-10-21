using Newtonsoft.Json.Linq;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Splitio.Domain;
using Splitio.Services.Client.Classes;
using Splitio.Services.Client.Interfaces;
using SplitOpenFeatureProvider;

namespace Splitio.OpenFeature
{
    public class Provider : FeatureProvider
    {
        private readonly Metadata _metadata = new("Split Client");
        private ISplitClient _client;
        private readonly String CONTROL = "control";
        private readonly SplitWrapper _splitWrapper;

        public Provider(Dictionary<String, Object> initialContext)
        {
            if (initialContext == null)
            {
                Console.WriteLine("Exception: Missing SplitClient instance or SDK ApiKey");
                throw new ArgumentException("Exception: Missing SplitClient instance or SDK ApiKey");
            }

            if (initialContext.ContainsKey("SplitClient"))
            {
                initialContext.TryGetValue("SplitClient", out var client);
                _client = (ISplitClient)client;
                return;
            }

            if (!initialContext.ContainsKey("ApiKey")) { 
                Console.WriteLine("Exception: Missing Split SDK ApiKey");
                throw new ArgumentException("Exception: Missing Split SDK ApiKey");
            }
            string apiKey = "";
            initialContext.TryGetValue("ApiKey", out var key);
            apiKey = (string)key;
            var config = new ConfigurationOptions();
            initialContext.TryGetValue("ConfigOptions", out var configs);
            if (configs != null)
            {
                config = (ConfigurationOptions)configs;
            }
            _splitWrapper = new SplitWrapper(apiKey, config);
            _client = _splitWrapper.getSplitClient();
        }

        public override Metadata GetMetadata() => _metadata;

        public void Dispose()
        {
            _client.Destroy();
        }

        public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<bool>(flagKey, defaultValue, context, cancellationToken);  
        }

        public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<string>(flagKey, defaultValue, context, cancellationToken);
        }

        public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<int>(flagKey, defaultValue, context, cancellationToken);
        }

        public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<double>(flagKey, defaultValue, context, cancellationToken);
        }

        public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<Value>(flagKey, defaultValue, context, cancellationToken);
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

        private ResolutionDetails<T> ProviderError<T>(string flagKey, T defaultValue)
        {
            return new ResolutionDetails<T>(
                                flagKey,
                                defaultValue,
                                variant: CONTROL,
                                reason: Reason.Error,
                                errorType: ErrorType.ProviderFatal);
        }

        private Task<ResolutionDetails<T>> Evaluate<T>(string flagKey, T defaultValue,
            EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            if (!_splitWrapper.IsSDKReady())
            {
                return Task.FromResult(ProviderError<T>(flagKey, defaultValue));
            }

            var key = GetTargetingKey(context);
            if (key == null)
            {
                return Task.FromResult(KeyNotFound<T>(flagKey, defaultValue));
            }

            var originalResult = "";
            SplitResult structureResult = new SplitResult();

            if (typeof(T) == typeof(Value))
            {
                structureResult = _client.GetTreatmentWithConfig(key, flagKey, TransformContext(context));
                originalResult = structureResult.Treatment;
            }
            else
            {
                originalResult = _client.GetTreatment(key, flagKey, TransformContext(context));
            }

            if (originalResult == CONTROL)
            {
                return Task.FromResult(FlagNotFound<T>(flagKey, defaultValue));
            }

            return ConstructResolution<T>(originalResult, flagKey, defaultValue, structureResult);
        }

        private Task<ResolutionDetails<T>> ConstructResolution<T>(string originalResult, string flagKey, T defaultValue,
            SplitResult structureResult)
        {
            try
            {
                if (typeof(T) == typeof(Value))
                {
                    var jsonString = structureResult.Config;
                    var dict = JObject.Parse(jsonString).ToObject<Dictionary<string, string>>();
                    if (dict == null)
                    {
                        Console.WriteLine($"Exception: {originalResult} is not a Json");
                        return Task.FromResult(ParseError<T>(flagKey, defaultValue));
                    }

                    var dict2 = dict.ToDictionary(x => x.Key, x => new Value(x.Value));
                    var dictValue = new Value(new Structure(dict2));
                    object vv = dictValue;

                    return Task.FromResult(new ResolutionDetails<T>(
                        flagKey,
                        (T) vv,
                        variant: structureResult.Treatment,
                        reason: Reason.TargetingMatch,
                        errorType: ErrorType.None));
                }

                T evaluationResult = Parse<T>(originalResult);
                return Task.FromResult(new ResolutionDetails<T>(flagKey, evaluationResult, errorType: ErrorType.None, variant: originalResult, reason: Reason.TargetingMatch));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {originalResult} is not a {typeof(T)}");
                return Task.FromResult(ParseError<T>(flagKey, defaultValue));
            }
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

        private static T Parse<T>(string strValue)
        {
            var type = typeof(T);
            if (type == typeof(bool))
            {
                var returnedVal = strValue.ToLower() switch
                {
                    "on" or "true" => true,
                    "off" or "false" => false
                };
                object vv = returnedVal;
                return (T)vv;
            }
            else if (type == typeof(string))
            {
                object vv = strValue;
                return (T)vv;
            }
            else if (type == typeof(int))
            {
                var evaluationResult = int.Parse(strValue);
                object vv = evaluationResult;
                return (T)vv;
            }
            else if (type == typeof(double))
            {
                var evaluationResult = double.Parse(strValue);
                object vv = evaluationResult;
                return (T)vv;
            }

            throw new FormatException("Could not parse value");
        }       
    }
}