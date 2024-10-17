namespace Tggl.Tests;

using System.Text.Json;
using Xunit;

public class StandardTests
{
    public class TestData
    {
        public string Name { get; set; }
        public Flag Flag { get; set; }
        public Dictionary<string, object> Context { get; set; }
        public Flag.Variation Expected { get; set; }
        
        public override string ToString()
        {
            return Name;
        }
    }
    
    public static IEnumerable<object[]> GetTestData()
    {
        var jsonData = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(),"standard_tests.json"));
        var testData = JsonSerializer.Deserialize<List<TestData>>(jsonData, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new Flag.OperatorEnumConverter() },
        });

        if (testData == null)
        {
            throw new Exception("Failed to deserialize test data");
        }

        foreach (var data in testData)
        {
            yield return new object[] { data };
        }
    }
    
    [Theory(DisplayName = "Test")]
    [MemberData(nameof(GetTestData))]
    public void Standard(TestData data)
    {
        var variation = TgglLocalClient.EvalFlag(data.Context, data.Flag);
        Assert.Equivalent(data.Expected, variation);
    }
}