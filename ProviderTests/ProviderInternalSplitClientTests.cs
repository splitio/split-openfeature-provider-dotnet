using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Splitio.Services.Client.Classes;
using Splitio.OpenFeature;
using Splitio.Services.Client.Interfaces;

namespace ProviderTests;

[TestClass]
public class ProviderInternalSplitClientTests
{
    FeatureClient client;
    ISplitClient sdk;
    Dictionary<String, Object> initialContext = new Dictionary<String, Object>();

    public ProviderInternalSplitClientTests()
    {
        // Create the Split client
        var config = new ConfigurationOptions
        {
            LocalhostFilePath = "../../../split.yaml",
            Logger = new CustomLogger()
        };
        initialContext.Add("ConfigOptions", config);
        initialContext.Add("ApiKey", "localhost");
    }

    [TestMethod]
    public async Task UseDefaultTest()
    {
        await Api.Instance.SetProviderAsync(new Provider(initialContext));
        client = OpenFeature.Api.Instance.GetClient();
        var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
        client.SetContext(context);

        String flagName = "random-non-existent-feature";

        var result = await client.GetBooleanValueAsync(flagName, false);
        Assert.IsFalse(result);
        result = await client.GetBooleanValueAsync(flagName, true);
        Assert.IsTrue(result);

        String defaultString = "blah";
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
        var value = await client.GetObjectValueAsync("int_feature", new Value(defaultValue));
        Assert.IsTrue(StructuresMatch(defaultValue, value.AsStructure));

        var details = await client.GetObjectDetailsAsync("int_feature", new Value(defaultValue));
        Assert.IsTrue(StructuresMatch(defaultValue, details.Value.AsStructure));
        Assert.AreEqual(ErrorType.ParseError, details.ErrorType);
        Assert.AreEqual(Reason.Error, details.Reason);
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
