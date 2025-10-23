using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Splitio.OpenFeature;
using Splitio.Services.Cache.Interfaces;
using Splitio.Services.Client.Classes;
using Splitio.Services.Client.Interfaces;
using Splitio.Services.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace ProviderTests
{

    [TestClass]
    public class ProviderTests
    {
        FeatureClient client;
        ISplitClient sdk;
        Dictionary<String, Object> initialContext = new Dictionary<String, Object>();

        public ProviderTests()
        {
            // Create the Split client
            var config = new ConfigurationOptions
            {
                LocalhostFilePath = "../../../split.yaml",
                Logger = new CustomLogger()
            };
            var factory = new SplitFactory("localhost", config);
            sdk = factory.Client();
            try
            {
                sdk.BlockUntilReady(10000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception initializing Split client! {ex}");
                throw;
            }
            initialContext.Add("SplitClient", sdk);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), "Missing SplitClient instance or SDK ApiKey")]
        public async Task InitializeWithNullTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(null));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), "Missing Split SDK ApiKey")]
        public async Task InitializeWithoutApiKeyTest()
        {
            Dictionary<string, object> initialContext2 = new Dictionary<string, object>();
            initialContext2.Add("something", "sdk");
            await Api.Instance.SetProviderAsync(new Provider(initialContext2));
        }

        [TestMethod]
        public async Task UseDefaultTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            string flagName = "random-non-existent-feature";

            var result = await client.GetBooleanValueAsync(flagName, false);
            Assert.IsFalse(result);
            result = await client.GetBooleanValueAsync(flagName, true);
            Assert.IsTrue(result);

            string defaultString = "blah";
            var resultString = await client.GetStringValueAsync(flagName, defaultString);
            Assert.AreEqual(defaultString, resultString);

            int defaultInt = 100;
            var resultInt = await client.GetIntegerValueAsync(flagName, defaultInt);
            Assert.AreEqual(defaultInt, resultInt);

            Structure defaultStructure = Structure.Builder().Set("foo", new Value("bar")).Build();
            Value resultStructure = await client.GetObjectValueAsync(flagName, new Value(defaultStructure));
            Assert.IsTrue(StructuresMatch(defaultStructure, resultStructure.AsStructure));
        }

        [TestMethod]
        public async Task MissingTargetingKeyTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();

            client.SetContext(EvaluationContext.Builder().Build());
            FlagEvaluationDetails<bool> details = await client.GetBooleanDetailsAsync("non-existent-feature", false);
            Assert.IsFalse(details.Value);
            Assert.AreEqual(ErrorType.TargetingKeyMissing, details.ErrorType);
        }

        [TestMethod]
        public async Task GetControlVariantNonExistentSplit()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            FlagEvaluationDetails<bool> details = await client.GetBooleanDetailsAsync("non-existent-feature", false);
            Assert.IsFalse(details.Value);
            Assert.AreEqual("control", details.Variant);
            Assert.AreEqual(ErrorType.FlagNotFound, details.ErrorType);
        }

        [TestMethod]
        public async Task GetBooleanSplitTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var result = await client.GetBooleanValueAsync("some_other_feature", true);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task GetBooleanSplitWithKeyTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var result = await client.GetBooleanValueAsync("my_feature", false);
            Assert.IsTrue(result);

            context = EvaluationContext.Builder().Set("targetingKey", "randomKey").Build();
            result = await client.GetBooleanValueAsync("my_feature", true, context);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task GetStringSplitTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var result = await client.GetStringValueAsync("some_other_feature", "on");
            Assert.AreEqual("off", result);
        }

        [TestMethod]
        public async Task GetIntegerSplitTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var result = await client.GetIntegerValueAsync("int_feature", 0);
            Assert.AreEqual(32, result);
        }

        [TestMethod]
        public async Task GetObjectSplitTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var result = await client.GetObjectValueAsync("obj_feature", new Value());
            Structure expectedValue = Structure.Builder().Set("key", new Value("value")).Build();
            Assert.IsTrue(StructuresMatch(expectedValue, result.AsStructure));
        }

        [TestMethod]
        public async Task GetDoubleSplitTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var result = await client.GetDoubleValueAsync("int_feature", 0D);
            Assert.AreEqual(32D, result);
        }

        [TestMethod]
        public async Task GetBooleanDetailsTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var details = await client.GetBooleanDetailsAsync("some_other_feature", true);
            Assert.AreEqual("some_other_feature", details.FlagKey);
            Assert.AreEqual(Reason.TargetingMatch, details.Reason);
            Assert.IsFalse(details.Value);
            // the flag has a treatment of "off", this is returned as a value of false but the variant is still "off"
            Assert.AreEqual("off", details.Variant);
            Assert.AreEqual(ErrorType.None, details.ErrorType);
            Assert.AreEqual("{\"key\": \"value\"}", details.FlagMetadata.GetString("config"));
        }

        [TestMethod]
        public async Task GetIntegerDetailsAsyncTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var details = await client.GetIntegerDetailsAsync("int_feature", 0);
            Assert.AreEqual("int_feature", details.FlagKey);
            Assert.AreEqual(Reason.TargetingMatch, details.Reason);
            Assert.AreEqual(32, details.Value);
            // the flag has a treatment of "off", this is returned as a value of false but the variant is still "off"
            Assert.AreEqual("32", details.Variant);
            Assert.AreEqual(ErrorType.None, details.ErrorType);
            Assert.AreEqual("{\"key\": \"value\"}", details.FlagMetadata.GetString("config"));
        }

        [TestMethod]
        public async Task GetStringDetailsAsyncTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var details = await client.GetStringDetailsAsync("some_other_feature", "blah");
            Assert.AreEqual("some_other_feature", details.FlagKey);
            Assert.AreEqual(Reason.TargetingMatch, details.Reason);
            Assert.AreEqual("off", details.Value);
            // the flag has a treatment of "off", this is returned as a value of false but the variant is still "off"
            Assert.AreEqual("off", details.Variant);
            Assert.AreEqual(ErrorType.None, details.ErrorType);
            Assert.AreEqual("{\"key\": \"value\"}", details.FlagMetadata.GetString("config"));
        }

        [TestMethod]
        public async Task GetDoubleDetailsAsyncTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var details = await client.GetDoubleDetailsAsync("int_feature", 0D);
            Assert.AreEqual("int_feature", details.FlagKey);
            Assert.AreEqual(Reason.TargetingMatch, details.Reason);
            Assert.AreEqual(32D, details.Value);
            // the flag has a treatment of "off", this is returned as a value of false but the variant is still "off"
            Assert.AreEqual("32", details.Variant);
            Assert.AreEqual(ErrorType.None, details.ErrorType);
            Assert.AreEqual("{\"key\": \"value\"}", details.FlagMetadata.GetString("config"));
        }

        [TestMethod]
        public async Task GetObjectDetailsAsyncTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var details = await client.GetObjectDetailsAsync("obj_feature", new Value());
            Assert.AreEqual("obj_feature", details.FlagKey);
            Assert.AreEqual(Reason.TargetingMatch, details.Reason);
            Structure expected = Structure.Builder().Set("key", new Value("value")).Build();
            Assert.IsTrue(StructuresMatch(expected, details.Value.AsStructure));
            // the flag's treatment is stored as a string, and the variant is that raw string
            Assert.AreEqual("{\"key\": \"value\"}", details.Variant);
            Assert.AreEqual(ErrorType.None, details.ErrorType);
        }

        [TestMethod]
        public async Task GetBooleanFailTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            // attempt to fetch an object treatment as a Boolean. Should result in the default
            var value = await client.GetBooleanValueAsync("obj_feature", false);
            Assert.IsFalse(value);

            var details = await client.GetBooleanDetailsAsync("obj_feature", false);
            Assert.IsFalse(details.Value);
            Assert.AreEqual(ErrorType.ParseError, details.ErrorType);
            Assert.AreEqual(Reason.Error, details.Reason);
        }

        [TestMethod]
        public async Task GetIntegerFailTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            // attempt to fetch an object treatment as an integer. Should result in the default
            var value = await client.GetIntegerValueAsync("obj_feature", 10);
            Assert.AreEqual(10, value);

            var details = await client.GetIntegerDetailsAsync("obj_feature", 10);
            Assert.AreEqual(10, details.Value);
            Assert.AreEqual(ErrorType.ParseError, details.ErrorType);
            Assert.AreEqual(Reason.Error, details.Reason);
        }

        [TestMethod]
        public async Task GetDoubleFailTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            // attempt to fetch an object treatment as a double. Should result in the default
            var value = await client.GetDoubleValueAsync("obj_feature", 10D);
            Assert.AreEqual(10D, value);

            var details = await client.GetDoubleDetailsAsync("obj_feature", 10D);
            Assert.AreEqual(10D, details.Value);
            Assert.AreEqual(ErrorType.ParseError, details.ErrorType);
            Assert.AreEqual(Reason.Error, details.Reason);
        }

        [TestMethod]
        public async Task GetObjectFailTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            // attempt to fetch an int as an object. Should result in the default
            Structure defaultValue = Structure.Builder().Set("key", new Value("value")).Build();
            var value = await client.GetObjectValueAsync("int_feature_2", new Value(defaultValue));
            Assert.IsTrue(StructuresMatch(defaultValue, value.AsStructure));

            var details = await client.GetObjectDetailsAsync("int_feature_2", new Value(defaultValue));
            Assert.IsTrue(StructuresMatch(defaultValue, details.Value.AsStructure));
            Assert.AreEqual(ErrorType.ParseError, details.ErrorType);
            Assert.AreEqual(Reason.Error, details.Reason);
        }

        [TestMethod]
        public async Task SDKNotReadyTest()
        {
            var config2 = new ConfigurationOptions
            {
                FeaturesRefreshRate = 1000
            };
            var factory2 = new SplitFactory("apikey", config2);
            ISplitClient sdk2 = factory2.Client();
            Dictionary<string, object> initialContext2 = new Dictionary<string, object>();
            initialContext2.Add("SplitClient", sdk2);

            await Api.Instance.SetProviderAsync(new Provider(initialContext2));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var result = await client.GetStringValueAsync("some_other_feature", "on");
            Assert.AreEqual("on", result);
            var details = await client.GetObjectDetailsAsync("some_other_feature", new Value("on"));
            Assert.AreEqual(ErrorType.ProviderNotReady, details.ErrorType);
            Assert.AreEqual(Reason.Error, details.Reason);
            sdk2.Destroy();
        }

        [TestMethod]
        public async Task TestTrack()
        {
            Mock<ISplitClient> splitClient = new Mock<ISplitClient>();
            Dictionary<string, object> initialContext2 = new Dictionary<string, object>();
            initialContext2.Add("SplitClient", splitClient);
            Provider splitProvider = new Provider(initialContext); 
            await Api.Instance.SetProviderAsync(splitProvider);

            Type type = typeof(Provider);
            FieldInfo privatePropertyInfo = type.GetField("_splitWrapper", BindingFlags.Instance | BindingFlags.NonPublic);
            SplitOpenFeatureProvider.SplitWrapper splitwrapper = (SplitOpenFeatureProvider.SplitWrapper)privatePropertyInfo.GetValue(splitProvider);

            type = typeof(SplitOpenFeatureProvider.SplitWrapper);
            FieldInfo splitClientProp = type.GetField("splitClient", BindingFlags.Instance | BindingFlags.NonPublic);
            splitClientProp.SetValue(splitwrapper, splitClient.Object);

            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder()
                .Set("targetingKey", "key")
                .Set("trafficType", "user")
                .Build();
            var eventDetails = TrackingEventDetails.Builder()
                .SetValue(10)
                .Set("prop", "val")
                .Build();
            client.SetContext(context);

            client.Track("event", context, eventDetails);
            Assert.AreEqual(1, splitClient.Invocations.Count);
            foreach (var item in splitClient.Invocations)
            {
                int count = 0;
                Assert.AreEqual(5, item.Arguments.Count);
                foreach (var arg in item.Arguments)
                {
                    switch (count)
                    {
                        case 0: Assert.AreEqual("key", arg.ToString()); break;
                        case 1: Assert.AreEqual("user", arg.ToString()); break;
                        case 2: Assert.AreEqual("event", arg.ToString()); break;
                        case 3: Assert.AreEqual((double)10, arg); break;
                        case 4:
                            {
                                Dictionary<string, object> temp = (Dictionary<string, object>)arg;
                                Assert.IsTrue(temp.ContainsValue("val"));
                                Assert.IsTrue(temp.ContainsKey("prop"));
                                break;
                            }
                        }
                    count++;
                }
            }
        }

        [TestMethod]
        public async Task TestTrackWithInvalidArguments()
        {
            Mock<ISplitClient> splitClient = new Mock<ISplitClient>();
            Dictionary<string, object> initialContext2 = new Dictionary<string, object>();
            initialContext2.Add("SplitClient", splitClient);
            Provider splitProvider = new Provider(initialContext);
            await Api.Instance.SetProviderAsync(splitProvider);

            Type type = typeof(Provider);
            FieldInfo privatePropertyInfo = type.GetField("_splitWrapper", BindingFlags.Instance | BindingFlags.NonPublic);
            SplitOpenFeatureProvider.SplitWrapper splitwrapper = (SplitOpenFeatureProvider.SplitWrapper)privatePropertyInfo.GetValue(splitProvider);

            type = typeof(SplitOpenFeatureProvider.SplitWrapper);
            FieldInfo splitClientProp = type.GetField("splitClient", BindingFlags.Instance | BindingFlags.NonPublic);
            splitClientProp.SetValue(splitwrapper, splitClient.Object);

            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder()
                .Set("trafficType", "user")
                .Build();
            client.SetContext(context);
            client.Track("event");
            Assert.AreEqual(0, splitClient.Invocations.Count);

            context = EvaluationContext.Builder()
                .Set("targetingKey", "key")
                .Build();
            client.SetContext(context);
            client.Track("event");
            Assert.AreEqual(0, splitClient.Invocations.Count);

            context = EvaluationContext.Builder()
                .Build();
            client.SetContext(context);
            client.Track("event");
            Assert.AreEqual(0, splitClient.Invocations.Count);

            context = EvaluationContext.Builder()
                .Set("targetingKey", "key")
                .Set("trafficType", "user")
                .Build();
            client.SetContext(context);
            try
            {
                client.Track("");
            }
            catch (Exception ex) { }
            Assert.AreEqual(0, splitClient.Invocations.Count);
        }

        private static bool StructuresMatch(Structure s1, Structure s2)
        {
            if (s1.Count != s2.Count)
            {
                return false;
            }
            foreach (string key in s1.Keys)
            {
                var v1 = s1.GetValue(key);
                var v2 = s2.GetValue(key);
                if (v1.ToString() != v2.ToString())
                {
                    return false;
                }
            }
            return true;
        }
    }
}