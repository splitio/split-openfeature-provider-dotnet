using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Splitio.Services.Client.Classes;
using Splitio.OpenFeature;

namespace ProviderTests;

[TestClass]
public class ProviderTests
{
    FeatureClient client;

    public ProviderTests()
    {
        // Create the Split client
        var config = new ConfigurationOptions
        {
            LocalhostFilePath = "../../../split.yaml",
            Logger = new CustomLogger()
        };
        var factory = new SplitFactory("localhost", config);
        var sdk = factory.Client();
        try
        {
            sdk.BlockUntilReady(10000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception initializing Split client! {ex}");
            throw;
        }

        // Sets the provider used by the client
        OpenFeature.Api.Instance.SetProvider(new Provider(sdk));

        // Gets a instance of the feature flag client
        client = OpenFeature.Api.Instance.GetClient();
        var context = EvaluationContext.Builder().Set("targetingKey", "key").Build();
        client.SetContext(context);
    }

    [TestMethod]
    public async Task UseDefaultTest()
    {
        String flagName = "random-non-existent-feature";

        var result = await client.GetBooleanValue(flagName, false);
        Assert.IsFalse(result);
        result = await client.GetBooleanValue(flagName, true);
        Assert.IsTrue(result);

        String defaultString = "blah";
        var resultString = await client.GetStringValue(flagName, defaultString);
        Assert.AreEqual(defaultString, resultString);

        int defaultInt = 100;
        var resultInt = await client.GetIntegerValue(flagName, defaultInt);
        Assert.AreEqual(defaultInt, resultInt);

        // TODO: Need to do structure
    }

    [TestMethod]
    public async Task MissingTargetingKeyTest()
    {
        client.SetContext(EvaluationContext.Builder().Build());
        FlagEvaluationDetails<bool> details = await client.GetBooleanDetails("non-existent-feature", false);
        Assert.IsFalse(details.Value);
        Assert.AreEqual(ErrorType.TargetingKeyMissing, details.ErrorType);
    }

    [TestMethod]
    public async Task GetControlVariantNonExistentSplit()
    {
        FlagEvaluationDetails<bool> details = await client.GetBooleanDetails("non-existent-feature", false);
        Assert.IsFalse(details.Value);
        Assert.AreEqual("control", details.Variant);
        Assert.AreEqual(ErrorType.FlagNotFound, details.ErrorType);
    }

    [TestMethod]
    public async Task GetBooleanSplitTest()
    {
        var result = await client.GetBooleanValue("some_other_feature", true);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GetBooleanSplitWithKeyTest()
    {
        var result = await client.GetBooleanValue("my_feature", false);
        Assert.IsTrue(result);

        var context = EvaluationContext.Builder().Set("targetingKey", "randomKey").Build();
        result = await client.GetBooleanValue("my_feature", true, context);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GetStringSplitTest()
    {
        var result = await client.GetStringValue("some_other_feature", "on");
        Assert.AreEqual("off", result);
    }

    [TestMethod]
    public async Task GetIntegerSplitTest()
    {
        var result = await client.GetIntegerValue("int_feature", 0);
        Assert.AreEqual(32, result);
    }

    [TestMethod]
    public async Task GetObjectSplitTest()
    {
        var result = await client.GetObjectValue("obj_feature", new Value());
        Structure expectedValue = Structure.Builder().Set("key", new Value("value")).Build();
        Assert.IsTrue(StructuresMatch(expectedValue, result.AsStructure));
    }

    [TestMethod]
    public async Task GetDoubleSplitTest()
    {
        var result = await client.GetDoubleValue("int_feature", 0D);
        Assert.AreEqual(32D, result);
    }

    [TestMethod]
    public async Task GetBooleanDetailsTest()
    {
        var details = await client.GetBooleanDetails("some_other_feature", true);
        Assert.AreEqual("some_other_feature", details.FlagKey);
        Assert.AreEqual(Reason.TargetingMatch, details.Reason);
        Assert.IsFalse(details.Value);
        // the flag has a treatment of "off", this is returned as a value of false but the variant is still "off"
        Assert.AreEqual("off", details.Variant);
        Assert.AreEqual(ErrorType.None, details.ErrorType);
    }

    [TestMethod]
    public async Task GetIntegerDetailsTest()
    {
        var details = await client.GetIntegerDetails("int_feature", 0);
        Assert.AreEqual("int_feature", details.FlagKey);
        Assert.AreEqual(Reason.TargetingMatch, details.Reason);
        Assert.AreEqual(32, details.Value);
        // the flag has a treatment of "off", this is returned as a value of false but the variant is still "off"
        Assert.AreEqual("32", details.Variant);
        Assert.AreEqual(ErrorType.None, details.ErrorType);
    }

    [TestMethod]
    public async Task GetStringDetailsTest()
    {
        var details = await client.GetStringDetails("some_other_feature", "blah");
        Assert.AreEqual("some_other_feature", details.FlagKey);
        Assert.AreEqual(Reason.TargetingMatch, details.Reason);
        Assert.AreEqual("off", details.Value);
        // the flag has a treatment of "off", this is returned as a value of false but the variant is still "off"
        Assert.AreEqual("off", details.Variant);
        Assert.AreEqual(ErrorType.None, details.ErrorType);
    }

    [TestMethod]
    public async Task GetDoubleDetailsTest()
    {
        var details = await client.GetDoubleDetails("int_feature", 0D);
        Assert.AreEqual("int_feature", details.FlagKey);
        Assert.AreEqual(Reason.TargetingMatch, details.Reason);
        Assert.AreEqual(32D, details.Value);
        // the flag has a treatment of "off", this is returned as a value of false but the variant is still "off"
        Assert.AreEqual("32", details.Variant);
        Assert.AreEqual(ErrorType.None, details.ErrorType);
    }

    [TestMethod]
    public async Task GetObjectDetailsTest()
    {
        var details = await client.GetObjectDetails("obj_feature", new Value());
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
        // attempt to fetch an object treatment as a Boolean. Should result in the default
        var value = await client.GetBooleanValue("obj_feature", false);
        Assert.IsFalse(value);

        var details = await client.GetBooleanDetails("obj_feature", false);
        Assert.IsFalse(details.Value);
        Assert.AreEqual(ErrorType.ParseError, details.ErrorType);
        Assert.AreEqual(Reason.Error, details.Reason);
    }

    [TestMethod]
    public async Task GetIntegerFailTest()
    {
        // attempt to fetch an object treatment as an integer. Should result in the default
        var value = await client.GetIntegerValue("obj_feature", 10);
        Assert.AreEqual(10, value);

        var details = await client.GetIntegerDetails("obj_feature", 10);
        Assert.AreEqual(10, details.Value);
        Assert.AreEqual(ErrorType.ParseError, details.ErrorType);
        Assert.AreEqual(Reason.Error, details.Reason);
    }

    [TestMethod]
    public async Task GetDoubleFailTest()
    {
        // attempt to fetch an object treatment as a double. Should result in the default
        var value = await client.GetDoubleValue("obj_feature", 10D);
        Assert.AreEqual(10D, value);

        var details = await client.GetDoubleDetails("obj_feature", 10D);
        Assert.AreEqual(10D, details.Value);
        Assert.AreEqual(ErrorType.ParseError, details.ErrorType);
        Assert.AreEqual(Reason.Error, details.Reason);
    }

    [TestMethod]
    public async Task GetObjectFailTest()
    {
        // attempt to fetch an int as an object. Should result in the default
        Structure defaultValue = Structure.Builder().Set("key", new Value("value")).Build();
        var value = await client.GetObjectValue("int_feature", new Value(defaultValue));
        Assert.IsTrue(StructuresMatch(defaultValue, value.AsStructure));

        var details = await client.GetObjectDetails("int_feature", new Value(defaultValue));
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
