using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Splitio.OpenFeature;
using Splitio.Services.Client.Classes;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProviderTests
{

    [TestClass]
    public class ProviderInternalSplitClientTests
    {
        FeatureClient client;
        Dictionary<string, object> initialContext = new Dictionary<string, object>();

        public ProviderInternalSplitClientTests()
        {
            // Create the Split client
            var config = new ConfigurationOptions
            {
                LocalhostFilePath = "../../../split.yaml",
//                Logger = new CustomLogger()
            };
            initialContext.Add("ConfigOptions", config);
            initialContext.Add("SdkKey", "localhost");
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
            await Api.Instance.ShutdownAsync();
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

            var result = await client.GetObjectValueAsync("obj_feature", new Value("default"));
            Assert.AreEqual("default", result.AsString);

            result = await client.GetObjectValueAsync("obj_feature_special", new Value("default"));
            Structure expectedValue = Structure.Builder().Set("treatment", new Value("on")).Build();
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
        }

        [TestMethod]
        public async Task GetObjectDetailsAsyncTest()
        {
            await Api.Instance.SetProviderAsync(new Provider(initialContext));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var details = await client.GetObjectDetailsAsync("obj_feature", new Value("default"));
            Assert.AreEqual(ErrorType.ParseError, details.ErrorType);
            Assert.AreEqual("default", details.Value.AsString);

            var result = await client.GetObjectDetailsAsync("obj_feature_special", new Value("default"));
            Structure expectedValue = Structure.Builder().Set("treatment", new Value("on")).Build();
            Assert.IsTrue(StructuresMatch(expectedValue, result.Value.AsStructure));
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
        public async Task PassSDKReadyTimeTest()
        {
            var config2 = new ConfigurationOptions
            {
                LocalhostFilePath = "../../../split.yaml",
                // Logger = new CustomLogger()
            };
            Dictionary<string, object> initialContext2 = new Dictionary<string, object>();
            initialContext2.Add("ConfigOptions", config2);
            initialContext2.Add("SdkKey", "localhost");
            initialContext2.Add("ReadyBlockTime", 1000);

            await Api.Instance.SetProviderAsync(new Provider(initialContext2));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var result = await client.GetStringValueAsync("some_other_feature", "on");
            Assert.AreEqual("off", result);
        }

        [TestMethod]
        public async Task SDKNotReadyTest()
        {
            var config2 = new ConfigurationOptions
            {
                FeaturesRefreshRate = 1000
            };
            Dictionary<string, object> initialContext2 = new Dictionary<string, object>();
            initialContext2.Add("ConfigOptions", config2);
            initialContext2.Add("SdkKey", "apikey");

            await Api.Instance.SetProviderAsync(new Provider(initialContext2));
            client = OpenFeature.Api.Instance.GetClient();
            var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
            client.SetContext(context);

            var result = await client.GetStringValueAsync("some_other_feature", "on");
            Assert.AreEqual("on", result);
            var details = await client.GetStringDetailsAsync("some_other_feature", "default");
            Assert.AreEqual(ErrorType.ProviderNotReady, details.ErrorType);
            Assert.AreEqual(Reason.Error, details.Reason);
            await OpenFeature.Api.Instance.GetProvider().ShutdownAsync();
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