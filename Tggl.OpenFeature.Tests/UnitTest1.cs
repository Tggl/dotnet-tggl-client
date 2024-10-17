using OpenFeature;
using OpenFeature.Model;

namespace Tggl.OpenFeature.Tests;

public class UnitTest1
{
    [Fact]
    public async void Test1()
    {
        await Api.Instance.SetProviderAsync(new TgglLocalProvider("bA0ozj--i9UWVXfJAhxGgZu9okytTGZBFdQ5JixykZE"));
        Api.Instance.SetContext();
        var client = Api.Instance.GetClient();
        client.SetContext();
        var provider = Api.Instance.GetProvider();
        // var value = await client.GetStringValueAsync("transactionLimnit", "foo", EvaluationContext.Builder().Set("clientID", "u1").Build());
        // var value = await client.GetBooleanValueAsync("transactionLimnit", "foo", EvaluationContext.Builder().Set("clientID", "u1").Build());
        // var value = await client.GetIntegerValueAsync("transactionLimnit", "foo", EvaluationContext.Builder().Set("clientID", "u1").Build());
        // var value = await client.GetDoubleValueAsync("transactionLimnit", "foo", EvaluationContext.Builder().Set("clientID", "u1").Build());
        var value = await client.GetObjectValueAsync("transactionLimnit", new Value(), EvaluationContext.Builder().Set("clientID", "u1").Build());
        var p = 0;
    }
}