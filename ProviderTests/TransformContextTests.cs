using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenFeature.Model;
using Splitio.OpenFeature.Provider;
using System.Collections.Generic;
using System.Reflection;

namespace ProviderTests
{
    [TestClass]
    public class TransformContextTests
    {
        private static MethodInfo _convertValueMethod;
        private static MethodInfo _transformContextMethod;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            // Get private static methods using reflection
            var providerType = typeof(Provider);
            _convertValueMethod = providerType.GetMethod("ConvertValue", BindingFlags.NonPublic | BindingFlags.Static);
            _transformContextMethod = providerType.GetMethod("TransformContext", BindingFlags.NonPublic | BindingFlags.Static);
        }

        #region ConvertValue Direct Tests

        [TestMethod]
        public void ConvertValue_WholeNumber_ConvertsToInt()
        {
            // OpenFeature stores 32 as 32.0 internally
            var value = new Value(32);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(int));
            Assert.AreEqual(32, result);
        }

        [TestMethod]
        public void ConvertValue_DoubleWithZeroDecimal_ConvertsToInt()
        {
            // 32.0 should be converted to int
            var value = new Value(32.0);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(int));
            Assert.AreEqual(32, result);
        }

        [TestMethod]
        public void ConvertValue_DoubleWithFractionalPart_RemainsDouble()
        {
            // 32.5 should remain as double
            var value = new Value(32.5);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(double));
            Assert.AreEqual(32.5, result);
        }

        [TestMethod]
        public void ConvertValue_NegativeWholeNumber_ConvertsToInt()
        {
            var value = new Value(-42.0);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(int));
            Assert.AreEqual(-42, result);
        }

        [TestMethod]
        public void ConvertValue_NegativeFractional_RemainsDouble()
        {
            var value = new Value(-42.7);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(double));
            Assert.AreEqual(-42.7, result);
        }

        [TestMethod]
        public void ConvertValue_Zero_ConvertsToInt()
        {
            var value = new Value(0);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(int));
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void ConvertValue_MaxInt_ConvertsToInt()
        {
            var value = new Value((double)int.MaxValue);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(int));
            Assert.AreEqual(int.MaxValue, result);
        }

        [TestMethod]
        public void ConvertValue_MinInt_ConvertsToInt()
        {
            var value = new Value((double)int.MinValue);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(int));
            Assert.AreEqual(int.MinValue, result);
        }

        [TestMethod]
        public void ConvertValue_LargerThanIntButWholeNumber_ConvertsToLong()
        {
            // Number larger than int.MaxValue but fits in long
            double largeWholeNumber = (double)int.MaxValue + 1000.0;
            var value = new Value(largeWholeNumber);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(long));
            Assert.AreEqual((long)largeWholeNumber, result);
        }

        [TestMethod]
        public void ConvertValue_SmallerThanIntMinButWholeNumber_ConvertsToLong()
        {
            // Number smaller than int.MinValue but fits in long
            double largeNegativeWholeNumber = (double)int.MinValue - 1000.0;
            var value = new Value(largeNegativeWholeNumber);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(long));
            Assert.AreEqual((long)largeNegativeWholeNumber, result);
        }

        [TestMethod]
        public void ConvertValue_VeryLargeWholeNumber_RemainsDouble()
        {
            // Number larger than long.MaxValue should remain as double
            double veryLargeNumber = (double)long.MaxValue * 2.0;
            var value = new Value(veryLargeNumber);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(double));
            Assert.AreEqual(veryLargeNumber, result);
        }

        [TestMethod]
        public void ConvertValue_PositiveInfinity_RemainsDouble()
        {
            var value = new Value(double.PositiveInfinity);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(double));
            Assert.AreEqual(double.PositiveInfinity, result);
        }

        [TestMethod]
        public void ConvertValue_NegativeInfinity_RemainsDouble()
        {
            var value = new Value(double.NegativeInfinity);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(double));
            Assert.AreEqual(double.NegativeInfinity, result);
        }

        [TestMethod]
        public void ConvertValue_NaN_RemainsDouble()
        {
            var value = new Value(double.NaN);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(double));
            Assert.IsTrue(double.IsNaN((double)result));
        }

        [TestMethod]
        public void ConvertValue_StringValue_RemainsString()
        {
            var value = new Value("test string");
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(string));
            Assert.AreEqual("test string", result);
        }

        [TestMethod]
        public void ConvertValue_BooleanValue_RemainsBoolean()
        {
            var value = new Value(true);
            var result = _convertValueMethod.Invoke(null, new object[] { value });

            Assert.IsInstanceOfType(result, typeof(bool));
            Assert.AreEqual(true, result);
        }

        #endregion

        #region TransformContext Integration Tests

        [TestMethod]
        public void TransformContext_MixedNumericTypes_ConvertsCorrectly()
        {
            var context = EvaluationContext.Builder()
                .Set("targetingKey", "user123")
                .Set("age", 32)              // whole number -> int
                .Set("score", 85.0)          // whole number with .0 -> int
                .Set("rating", 4.5)          // fractional -> double
                .Set("balance", -100.25)     // negative fractional -> double
                .Set("count", 0)             // zero -> int
                .Build();

            var result = (Dictionary<string, object>)_transformContextMethod.Invoke(null, new object[] { context });

            Assert.AreEqual(6, result.Count);
            Assert.IsInstanceOfType(result["age"], typeof(int));
            Assert.AreEqual(32, result["age"]);

            Assert.IsInstanceOfType(result["score"], typeof(int));
            Assert.AreEqual(85, result["score"]);

            Assert.IsInstanceOfType(result["rating"], typeof(double));
            Assert.AreEqual(4.5, result["rating"]);

            Assert.IsInstanceOfType(result["balance"], typeof(double));
            Assert.AreEqual(-100.25, result["balance"]);

            Assert.IsInstanceOfType(result["count"], typeof(int));
            Assert.AreEqual(0, result["count"]);
        }

        [TestMethod]
        public void TransformContext_WithNonNumericTypes_PreservesTypes()
        {
            var context = EvaluationContext.Builder()
                .Set("targetingKey", "user123")
                .Set("name", "John Doe")
                .Set("isActive", true)
                .Set("age", 32)
                .Build();

            var result = (Dictionary<string, object>)_transformContextMethod.Invoke(null, new object[] { context });

            Assert.AreEqual(4, result.Count);
            Assert.IsInstanceOfType(result["name"], typeof(string));
            Assert.AreEqual("John Doe", result["name"]);

            Assert.IsInstanceOfType(result["isActive"], typeof(bool));
            Assert.AreEqual(true, result["isActive"]);

            Assert.IsInstanceOfType(result["age"], typeof(int));
            Assert.AreEqual(32, result["age"]);
        }

        [TestMethod]
        public void TransformContext_NullContext_ReturnsEmptyDictionary()
        {
            var result = (Dictionary<string, object>)_transformContextMethod.Invoke(null, new object[] { null });

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void TransformContext_LargeNumbers_ConvertsToLong()
        {
            double largeNumber = (double)int.MaxValue + 1000.0;
            var context = EvaluationContext.Builder()
                .Set("targetingKey", "user123")
                .Set("bigNumber", largeNumber)
                .Build();

            var result = (Dictionary<string, object>)_transformContextMethod.Invoke(null, new object[] { context });

            Assert.IsInstanceOfType(result["bigNumber"], typeof(long));
            Assert.AreEqual((long)largeNumber, result["bigNumber"]);
        }

        #endregion
    }
}
