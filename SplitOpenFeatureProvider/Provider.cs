using Newtonsoft.Json.Linq;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Splitio.Domain;
using Splitio.Services.Client.Classes;
using Splitio.Services.Client.Interfaces;
using SplitOpenFeatureProvider;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Splitio.OpenFeature
{
    public class Provider : FeatureProvider
    {
        private readonly Metadata _metadata = new Metadata(Constants.ProviderName);
        private readonly SplitWrapper _splitWrapper;

        public Provider(Dictionary<string, object> initialContext)
        {
            Validate(initialContext);

            if (initialContext.ContainsKey(Constants.SplitClientKey))
            {
                initialContext.TryGetValue(Constants.SplitClientKey, out var client);
                _splitWrapper = new SplitWrapper((ISplitClient)client);
                return;
            }

            _splitWrapper = CreateSplitWrapper(initialContext);
        }

        public override System.Collections.Immutable.IImmutableList<Hook> GetProviderHooks()
        {
            return base.GetProviderHooks();
        }

        public override Metadata GetMetadata() => _metadata;

        public void Dispose()
        {
            _splitWrapper.getSplitClient().Destroy();
        }

        public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue,
            EvaluationContext context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<bool>(flagKey, defaultValue, context, cancellationToken);  
        }

        public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue,
            EvaluationContext context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<string>(flagKey, defaultValue, context, cancellationToken);
        }

        public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue,
            EvaluationContext context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<int>(flagKey, defaultValue, context, cancellationToken);
        }

        public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue,
            EvaluationContext context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<double>(flagKey, defaultValue, context, cancellationToken);
        }

        public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue,
            EvaluationContext context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<Value>(flagKey, defaultValue, context, cancellationToken);
        }

        private ResolutionDetails<T> KeyNotFound<T>(string flagKey, T defaultValue)
        {
            return new ResolutionDetails<T>(
                                flagKey,
                                defaultValue,
                                variant: Constants.CONTROL,
                                reason: Reason.Error,
                                errorType: ErrorType.TargetingKeyMissing);
        }

        private ResolutionDetails<T> ParseError<T>(string flagKey, T defaultValue)
        {
            return new ResolutionDetails<T>(
                                flagKey,
                                defaultValue,
                                variant: Constants.CONTROL,
                                reason: Reason.Error,
                                errorType: ErrorType.ParseError);
        }

        private ResolutionDetails<T> FlagNotFound<T>(string flagKey, T defaultValue)
        {
            return new ResolutionDetails<T>(
                                flagKey,
                                defaultValue,
                                variant: Constants.CONTROL,
                                reason: Reason.Error,
                                errorType: ErrorType.FlagNotFound);
        }

        private ResolutionDetails<T> ProviderNotReady<T>(string flagKey, T defaultValue)
        {
            return new ResolutionDetails<T>(
                                flagKey,
                                defaultValue,
                                variant: Constants.CONTROL,
                                reason: Reason.Error,
                                errorType: ErrorType.ProviderNotReady);
        }

        private Task<ResolutionDetails<T>> Evaluate<T>(string flagKey, T defaultValue,
            EvaluationContext context = null, CancellationToken cancellationToken = default)
        {
            if (!_splitWrapper.IsSDKReady())
            {
                return Task.FromResult(ProviderNotReady<T>(flagKey, defaultValue));
            }

            var key = GetTargetingKey(context);
            if (key == null)
            {
                return Task.FromResult(KeyNotFound<T>(flagKey, defaultValue));
            }

            SplitResult structureResult = _splitWrapper.getSplitClient().GetTreatmentWithConfig(key, flagKey, TransformContext(context));
            var originalResult = structureResult.Treatment;

            if (originalResult == Constants.CONTROL)
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
                        (T)vv,
                        variant: structureResult.Treatment,
                        flagMetadata: new ImmutableMetadata(
                            new Dictionary<string, object>
                            {
                                { "config", structureResult.Config },
                            }),
                        reason: Reason.TargetingMatch,
                        errorType: ErrorType.None));
                }

                T evaluationResult = Parse<T>(originalResult);
	            return Task.FromResult(new ResolutionDetails<T>(
                        flagKey,
                        (T)evaluationResult,
                        variant: structureResult.Treatment,
                        flagMetadata: new ImmutableMetadata(
                            new Dictionary<string, object>
                            {
                                { "config", structureResult.Config },
                            }),
                        reason: Reason.TargetingMatch,
                        errorType: ErrorType.None));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {originalResult} is not a {typeof(T)}");
                return Task.FromResult(ParseError<T>(flagKey, defaultValue));
            }
        }

        private SplitWrapper CreateSplitWrapper(Dictionary<string, object> initialContext)
        {
            initialContext.TryGetValue(Constants.SdkApiKey, out var key);
            string apiKey = (string)key;
            var config = new ConfigurationOptions();
            initialContext.TryGetValue(Constants.ConfigKey, out var configs);
            if (configs != null)
            {
                config = (ConfigurationOptions)configs;
            }
            return new SplitWrapper(apiKey, config);

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
                if (strValue.ToLower() == "true" || strValue.ToLower() == "on") {
                    object vv = true;
                    return (T)vv;
                }
                if (strValue.ToLower() == "false" || strValue.ToLower() == "off")
                {
                    object vv = false;
                    return (T)vv;
                }
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

        private static void Validate(Dictionary<string, object> initialContext)
        {
            if (initialContext == null)
            {
                Console.WriteLine("Exception: Missing SplitClient instance or SDK ApiKey");
                throw new ArgumentException("Missing SplitClient instance or SDK ApiKey");
            }

            if (!initialContext.ContainsKey(Constants.SplitClientKey) && !initialContext.ContainsKey(Constants.SdkApiKey))
            {
                Console.WriteLine("Exception: Missing Split SDK ApiKey");
                throw new ArgumentException("Missing Split SDK ApiKey");
            }
        }
    }
}