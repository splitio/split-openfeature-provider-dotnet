using Newtonsoft.Json.Linq;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Splitio.Domain;
using Splitio.Services.Client.Classes;
using Splitio.Services.Client.Interfaces;
using Splitio.Services.Logger;
using Splitio.Services.Shared.Classes;
using SplitOpenFeatureProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Splitio.OpenFeature
{
    public class Provider : FeatureProvider
    {
        private readonly Metadata _metadata = new Metadata(Constants.ProviderName);
        private readonly SplitWrapper _splitWrapper;
        protected readonly ISplitLogger _log;

        public Provider(Dictionary<string, object> initialContext)
        {
            ValidateInitialContext(initialContext);

            if (initialContext.ContainsKey(Constants.SplitClientKey))
            {
                initialContext.TryGetValue(Constants.SplitClientKey, out var client);
                _splitWrapper = new SplitWrapper((ISplitClient)client);
                _log = WrapperAdapter.Instance().GetLogger(typeof(Provider));
                return;
            }

            _splitWrapper = CreateSplitWrapper(initialContext);
            _log = WrapperAdapter.Instance().GetLogger(typeof(Provider));
        }

        public override Metadata GetMetadata() => _metadata;

        public override Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            _splitWrapper.getSplitClient().Destroy();
            return Task.CompletedTask;
        }

        public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue,
            EvaluationContext context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<bool>(flagKey, defaultValue, context);  
        }

        public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue,
            EvaluationContext context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<string>(flagKey, defaultValue, context);
        }

        public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue,
            EvaluationContext context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<int>(flagKey, defaultValue, context);
        }

        public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue,
            EvaluationContext context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<double>(flagKey, defaultValue, context);
        }

        public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue,
            EvaluationContext context = null, CancellationToken cancellationToken = default)
        {
            return Evaluate<Value>(flagKey, defaultValue, context);
        }

        public override void Track(string trackingEventName, EvaluationContext evaluationContext = null, TrackingEventDetails trackingEventDetails = null)
        {
            if (!ValidateTrackDetails(trackingEventName, evaluationContext))
            {
                _log.Error("Track call is ignored");
                return;
            }

            var key = GetTargetingKey(evaluationContext);
            double value = 0;
            Dictionary<string, object> attributes = new Dictionary<string, object>();
            if (trackingEventDetails != null)
            {
                value = (double)trackingEventDetails.Value;
                attributes = trackingEventDetails.AsDictionary().ToDictionary(x => x.Key, x => x.Value.AsObject);
            }

            _splitWrapper.getSplitClient().Track(
                key,
                evaluationContext.GetValue(Constants.TrafficType).AsString,
                trackingEventName,
                value,
                attributes);
        }

        private static ResolutionDetails<T> KeyNotFound<T>(string flagKey, T defaultValue)
        {
            return new ResolutionDetails<T>(
                                flagKey,
                                defaultValue,
                                variant: Constants.CONTROL,
                                reason: Reason.Error,
                                errorType: ErrorType.TargetingKeyMissing);
        }

        private static ResolutionDetails<T> ParseError<T>(string flagKey, T defaultValue)
        {
            return new ResolutionDetails<T>(
                                flagKey,
                                defaultValue,
                                variant: Constants.CONTROL,
                                reason: Reason.Error,
                                errorType: ErrorType.ParseError);
        }

        private static ResolutionDetails<T> FlagNotFound<T>(string flagKey, T defaultValue)
        {
            return new ResolutionDetails<T>(
                                flagKey,
                                defaultValue,
                                variant: Constants.CONTROL,
                                reason: Reason.Error,
                                errorType: ErrorType.FlagNotFound);
        }

        private static ResolutionDetails<T> ProviderNotReady<T>(string flagKey, T defaultValue)
        {
            return new ResolutionDetails<T>(
                                flagKey,
                                defaultValue,
                                variant: Constants.CONTROL,
                                reason: Reason.Error,
                                errorType: ErrorType.ProviderNotReady);
        }

        private Task<ResolutionDetails<T>> Evaluate<T>(string flagKey, T defaultValue,
            EvaluationContext context = null)
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
                T evaluationResult = Parse<T>(originalResult);
	            return Task.FromResult(new ResolutionDetails<T>(
                        flagKey,
                        evaluationResult,
                        variant: structureResult.Treatment,
                        flagMetadata: new ImmutableMetadata(
                            new Dictionary<string, object>
                            {
                                { "config", structureResult.Config },
                            }),
                        reason: Reason.TargetingMatch,
                        errorType: ErrorType.None));
            }
            catch (Exception)
            {
                _log.Error($"Exception: {originalResult} is not a {typeof(T)}");
                return Task.FromResult(ParseError<T>(flagKey, defaultValue));
            }
        }

        private static SplitWrapper CreateSplitWrapper(Dictionary<string, object> initialContext)
        {
            initialContext.TryGetValue(Constants.SdkApiKey, out var key);
            string apiKey = (string)key;
            var config = new ConfigurationOptions();
            initialContext.TryGetValue(Constants.ConfigKey, out var configs);
            if (configs != null)
            {
                config = (ConfigurationOptions)configs;
            }
            initialContext.TryGetValue(Constants.ReadyBlockTime, out var readyBlockTime);
            if (readyBlockTime != null)
            {
                return new SplitWrapper(apiKey, config, (int)readyBlockTime);
            }

            return new SplitWrapper(apiKey, config);
        }
        private string GetTargetingKey(EvaluationContext context)
        {
            Value key;
            if (!context.TryGetValue("targetingKey", out key))
            {
                _log.Error("Split provider: targeting key missing!");
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
                if (strValue.ToLower().Equals("true") || strValue.ToLower().Equals("on")) {
                    object vv = true;
                    return (T)vv;
                }
                if (strValue.ToLower().Equals("false") || strValue.ToLower().Equals("off"))
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
            else if (type == typeof(Value))
            {
                var dict = JObject.Parse(strValue).ToObject<Dictionary<string, string>>();
                if (dict == null)
                {
                    throw new FormatException("Could not parse value");
                }
                var dict2 = dict.ToDictionary(x => x.Key, x => new Value(x.Value));
                object vv = new Value(new Structure(dict2));
                return (T)vv;
            }

            throw new FormatException("Could not parse value");
        }       

        private static void ValidateInitialContext(Dictionary<string, object> initialContext)
        {
            if (initialContext == null)
            {
                throw new ArgumentException("Missing SplitClient instance or SDK ApiKey");
            }

            if (!initialContext.ContainsKey(Constants.SplitClientKey) && !initialContext.ContainsKey(Constants.SdkApiKey))
            {
                throw new ArgumentException("Missing Split SDK ApiKey");
            }
        }

        private bool ValidateTrackDetails(string trackingEventName, EvaluationContext evaluationContext)
        {
            if (evaluationContext == null)
            {
                _log.Error("Track: Key, trafficType and eventType are required.");
                return false;
            }

            if (String.IsNullOrEmpty(trackingEventName)) {
                _log.Error("Track: eventName should be non-empty string.");
                return false;
            }

            if (String.IsNullOrEmpty(GetTargetingKey(evaluationContext)))
            {
                _log.Error("Track: Key is insvalid or mising.");
                return false;
            }

            if (!evaluationContext.ContainsKey(Constants.TrafficType) || 
                String.IsNullOrEmpty(evaluationContext.GetValue(Constants.TrafficType).AsString))
            {
                _log.Error("Track: trafficType is invalid or mising.");
                return false;
            }

            return true;
        }
    }
}