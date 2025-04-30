using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace NetBricks.Test;

public class ConfigTest : IDisposable
{

    public ConfigTest()
    {

        // configure services
        var builder = new HostBuilder().ConfigureServices((hostContext, services) =>
        {
            services.AddSingleLineConsoleLogger(logParams: false);
            services.AddConfig();
        }).UseConsoleLifetime();
        this.Host = builder.Build();

        // get config
        this.Config = this.Host.Services.GetService<IConfig>();
        if (this.Config == null) throw new Exception("ConfigTest: Config of type \"ConfigTestObj\" was not found.");

    }

    private IHost Host { get; }
    private IConfig Config { get; }

    [Fact]
    public async Task TestManual()
    {

        // manual get-cache, get-env, get-vault, set-cache
        Func<string, Task<string>> f1 = async key =>
        {
            if (Config.TryGetFromCache<string>(key, out string val)) return val;
            val = NetBricks.Config.GetOnce(key) ?? "my-default";
            val = await Config.GetFromKeyVaultAsync(val);
            Config.AddToCache<string>(key, val);
            return val;
        };

        // default
        string v0 = await f1("MY_KEY_asdh");
        Assert.Equal("my-default", v0);

        // set by env
        System.Environment.SetEnvironmentVariable("MY_KEY_rirr", "my-original-value");
        string v1 = await f1("MY_KEY_rirr");
        Assert.Equal("my-original-value", v1);

        // still pulling from cache
        System.Environment.SetEnvironmentVariable("MY_KEY_rirr", "my-changed-value");
        string v2 = await f1("MY_KEY_rirr");
        Assert.Equal("my-original-value", v2);

    }

    [Fact]
    public void TestGetOnceString()
    {

        // default
        string v0 = NetBricks.Config.GetOnce("MY_KEY_djur") ?? "my-default";
        Assert.Equal("my-default", v0);

        // set by env
        // NOTE: this also demonstrates that there is no cache
        System.Environment.SetEnvironmentVariable("MY_KEY_djur", "my-original-value");
        string v1 = NetBricks.Config.GetOnce("MY_KEY_djur") ?? "my-default";
        Assert.Equal("my-original-value", v1);

    }

    [Fact]
    public void TestGetString()
    {

        // default
        string v0 = Config.Get<string>("MY_KEY_bbie", str =>
        {
            if (string.IsNullOrEmpty(str)) return "my-default";
            return str;
        });
        Assert.Equal("my-default", v0);

        // still default based on cache
        string v1 = Config.Get<string>("MY_KEY_bbie");
        Assert.Equal("my-default", v1);

        // set by env
        System.Environment.SetEnvironmentVariable("MY_KEY_rirj", "my-original-value");
        string v2 = Config.Get<string>("MY_KEY_rirj");
        Assert.Equal("my-original-value", v2);

        // still pulling from cache
        System.Environment.SetEnvironmentVariable("MY_KEY_rirj", "my-changed-value");
        string v3 = Config.Get<string>("MY_KEY_rirj");
        Assert.Equal("my-original-value", v3);

    }

    [Fact]
    public async Task TestGetSecretString()
    {

        // default
        string v0 = await Config.GetSecretAsync<string>("MY_KEY_bbie", str =>
        {
            if (string.IsNullOrEmpty(str)) return "my-default";
            return str;
        });
        Assert.Equal("my-default", v0);

        // still default based on cache
        string v1 = await Config.GetSecretAsync<string>("MY_KEY_bbie");
        Assert.Equal("my-default", v1);

        // set by env
        System.Environment.SetEnvironmentVariable("MY_KEY_rirj", "my-original-value");
        string v2 = await Config.GetSecretAsync<string>("MY_KEY_rirj");
        Assert.Equal("my-original-value", v2);

        // still pulling from cache
        System.Environment.SetEnvironmentVariable("MY_KEY_rirj", "my-changed-value");
        string v3 = await Config.GetSecretAsync<string>("MY_KEY_rirj");
        Assert.Equal("my-original-value", v3);

    }

    [Fact]
    public void TestNullInCache()
    {

        // write null to cache
        string v0 = Config.Get<string>("MY_KEY_fhfh", str => null);
        Assert.Null(v0);

        // verify it was in cache as null
        bool wasInCache = Config.TryGetFromCache("MY_KEY_fhfh", out string v1);
        Assert.True(wasInCache);
        Assert.Null(v1);

        // verify it is still null when using Get()
        System.Environment.SetEnvironmentVariable("MY_KEY_fhfh", "my-original-value");
        string v2 = Config.Get<string>("MY_KEY_fhfh", str => null);
        Assert.Null(v2);

        // will also write null to cache
        string v3 = Config.Get<string>("MY_KEY_eiew");
        Assert.Null(v3);

        // verify it is still null
        System.Environment.SetEnvironmentVariable("MY_KEY_eiew", "my-original-value");
        string v4 = Config.Get<string>("MY_KEY_eiew");
        Assert.Null(v4);

    }

    [Fact]
    public async Task MakeSureAllEmptyStringsAreNull()
    {

        // GetOnce()
        System.Environment.SetEnvironmentVariable("MY_KEY_eija", "");
        string v0 = NetBricks.Config.GetOnce("MY_KEY_eija");
        Assert.Null(v0);

        // GetSecret()
        System.Environment.SetEnvironmentVariable("MY_KEY_ueye", "");
        string v1 = await Config.GetSecretAsync<string>("MY_KEY_ueye");
        Assert.Null(v1);

        // Get()
        System.Environment.SetEnvironmentVariable("MY_KEY_iiku", "");
        string v2 = Config.Get<string>("MY_KEY_iiku");
        Assert.Null(v2);

    }

    [Fact]
    public void VerifyThatArrayCanBeNull()
    {

        // Get()
        string[] v0 = Config.Get<string[]>("MY_KEY_ejhh", str => str.AsArray(() => null));
        Assert.Null(v0);

        // verify it was stored as null
        bool wasInCache = Config.TryGetFromCache("MY_KEY_ejhh", out string[] v1);
        Assert.True(wasInCache);
        Assert.Null(v1);

    }

    [Fact]
    public void VerifyThatNonStringRequiresConvert()
    {

        // OK
        string v0 = Config.Get<string>("MY_KEY_rjjr");
        Assert.Null(v0);

        // should show exception
        Assert.Throws<ArgumentNullException>(() =>
        {
            Config.Get<string[]>("MY_KEY_33yy");
        });

        // OK
        string[] v1 = Config.Get<string[]>("MY_KEY_44yf", str => str.AsArray(() => null));
        Assert.Null(v1);

    }

    [Fact]
    public void TestStringChain()
    {

        // if the first is set
        System.Environment.SetEnvironmentVariable("MY_KEY_yerf", "first-value");
        string v0 = NetBricks.Config.GetOnce("MY_KEY_yerf", "MY_KEY_cmdh").AsString(() => "my-default");
        Assert.Equal("first-value", v0);

        // if the second is set
        System.Environment.SetEnvironmentVariable("MY_KEY_kfue", "second-value");
        string v1 = NetBricks.Config.GetOnce("MY_KEY_ieud", "MY_KEY_kfue").AsString(() => "my-default");
        Assert.Equal("second-value", v1);

        // if none are set
        string v2 = NetBricks.Config.GetOnce("MY_KEY_vjue", "MY_KEY_eijr").AsString(() => "my-default");
        Assert.Equal("my-default", v2);

    }

    [Fact]
    public void TestGetOnceInt()
    {

        // default
        int v0 = NetBricks.Config.GetOnce("MY_KEY_urur").AsInt(() => -1);
        Assert.Equal(-1, v0);

        // set legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_ruhr", "100");
        int v1 = NetBricks.Config.GetOnce("MY_KEY_ruhr").AsInt(() => -1);
        Assert.Equal(100, v1);

        // set bad value
        System.Environment.SetEnvironmentVariable("MY_KEY_ehhf", "non-int");
        int v2 = NetBricks.Config.GetOnce("MY_KEY_ehhf").AsInt(() => -2);
        Assert.Equal(-2, v2);

    }

    [Fact]
    public async Task TestGetSecretInt()
    {

        // default
        int v0 = await Config.GetSecretAsync<int>("MY_KEY_vgvg", str => str.AsInt(() => -1));
        Assert.Equal(-1, v0);

        // set legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_wueh", "100");
        int v1 = await Config.GetSecretAsync<int>("MY_KEY_wueh", str => str.AsInt(() => -1));
        Assert.Equal(100, v1);

        // set bad value
        System.Environment.SetEnvironmentVariable("MY_KEY_swie", "non-int");
        int v2 = await Config.GetSecretAsync<int>("MY_KEY_swie", str => str.AsInt(() => -2));
        Assert.Equal(-2, v2);

    }

    [Fact]
    public void TestGetInt()
    {

        // default
        int v0 = Config.Get<int>("MY_KEY_iiuy", str => str.AsInt(() => -1));
        Assert.Equal(-1, v0);

        // set legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_ojfd", "100");
        int v1 = Config.Get<int>("MY_KEY_ojfd", str => str.AsInt(() => -1));
        Assert.Equal(100, v1);

        // set bad value
        System.Environment.SetEnvironmentVariable("MY_KEY_jfhr", "non-int");
        int v2 = Config.Get<int>("MY_KEY_jfhr", str => str.AsInt(() => -2));
        Assert.Equal(-2, v2);

    }

    [Fact]
    public void TestGetOnceBool()
    {

        // default
        bool v0 = NetBricks.Config.GetOnce("MY_KEY_jfjr").AsBool(() => false);
        Assert.False(v0);

        // set legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_kjhy", "true");
        bool v1 = NetBricks.Config.GetOnce("MY_KEY_kjhy").AsBool(() => false);
        Assert.True(v1);

        // set bad value
        System.Environment.SetEnvironmentVariable("MY_KEY_ruvd", "non-bool");
        bool v2 = NetBricks.Config.GetOnce("MY_KEY_ruvd").AsBool(() => false);
        Assert.False(v2);

        // test default of true
        bool v3 = NetBricks.Config.GetOnce("MY_KEY_jhhd").AsBool(() => true);
        Assert.True(v3);

    }

    [Fact]
    public async Task TestGetSecretBool()
    {

        // default
        bool v0 = await Config.GetSecretAsync<bool>("MY_KEY_jhgc", str => str.AsBool(() => false));
        Assert.False(v0);

        // set legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_iiur", "true");
        bool v1 = await Config.GetSecretAsync<bool>("MY_KEY_iiur", str => str.AsBool(() => false));
        Assert.True(v1);

        // set bad value
        System.Environment.SetEnvironmentVariable("MY_KEY_oiue", "non-bool");
        bool v2 = await Config.GetSecretAsync<bool>("MY_KEY_oiue", str => str.AsBool(() => false));
        Assert.False(v2);

        // test default of true
        bool v3 = await Config.GetSecretAsync<bool>("MY_KEY_juyf", str => str.AsBool(() => true));
        Assert.True(v3);

    }

    [Fact]
    public void TestGetBool()
    {

        // default
        bool v0 = Config.Get<bool>("MY_KEY_jhfd", str => str.AsBool(() => false));
        Assert.False(v0);

        // set legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_wdfr", "true");
        bool v1 = Config.Get<bool>("MY_KEY_wdfr", str => str.AsBool(() => false));
        Assert.True(v1);

        // set bad value
        System.Environment.SetEnvironmentVariable("MY_KEY_icgg", "non-bool");
        bool v2 = Config.Get<bool>("MY_KEY_icgg", str => str.AsBool(() => false));
        Assert.False(v2);

        // test default of true
        bool v3 = Config.Get<bool>("MY_KEY_zzaw", str => str.AsBool(() => true));
        Assert.True(v3);

    }

    [Fact]
    public void VerifyBoolCanBeTrueDifferentWays()
    {

        // true
        System.Environment.SetEnvironmentVariable("MY_KEY_cuuf", "true");
        bool v0 = NetBricks.Config.GetOnce("MY_KEY_cuuf").AsBool(() => false);
        Assert.True(v0);

        // True
        System.Environment.SetEnvironmentVariable("MY_KEY_cjdh", "True");
        bool v1 = NetBricks.Config.GetOnce("MY_KEY_cjdh").AsBool(() => false);
        Assert.True(v1);

        // tRUe
        System.Environment.SetEnvironmentVariable("MY_KEY_eiuw", "tRUe");
        bool v2 = NetBricks.Config.GetOnce("MY_KEY_eiuw").AsBool(() => false);
        Assert.True(v2);

        // yes
        System.Environment.SetEnvironmentVariable("MY_KEY_ivur", "yes");
        bool v3 = NetBricks.Config.GetOnce("MY_KEY_ivur").AsBool(() => false);
        Assert.True(v3);

        // Yes
        System.Environment.SetEnvironmentVariable("MY_KEY_iurh", "Yes");
        bool v4 = NetBricks.Config.GetOnce("MY_KEY_iurh").AsBool(() => false);
        Assert.True(v4);

        // YeS
        System.Environment.SetEnvironmentVariable("MY_KEY_cjfh", "YeS");
        bool v5 = NetBricks.Config.GetOnce("MY_KEY_cjfh").AsBool(() => false);
        Assert.True(v5);

        // 1
        System.Environment.SetEnvironmentVariable("MY_KEY_okhg", "1");
        bool v6 = NetBricks.Config.GetOnce("MY_KEY_okhg").AsBool(() => false);
        Assert.True(v6);

    }

    [Fact]
    public void VerifyBoolCanBeFalseDifferentWays()
    {

        // false
        System.Environment.SetEnvironmentVariable("MY_KEY_cuuf", "false");
        bool v0 = NetBricks.Config.GetOnce("MY_KEY_cuuf").AsBool(() => true);
        Assert.False(v0);

        // False
        System.Environment.SetEnvironmentVariable("MY_KEY_cjdh", "False");
        bool v1 = NetBricks.Config.GetOnce("MY_KEY_cjdh").AsBool(() => true);
        Assert.False(v1);

        // fAlSe
        System.Environment.SetEnvironmentVariable("MY_KEY_eiuw", "fAlSe");
        bool v2 = NetBricks.Config.GetOnce("MY_KEY_eiuw").AsBool(() => true);
        Assert.False(v2);

        // no
        System.Environment.SetEnvironmentVariable("MY_KEY_ivur", "no");
        bool v3 = NetBricks.Config.GetOnce("MY_KEY_ivur").AsBool(() => true);
        Assert.False(v3);

        // No
        System.Environment.SetEnvironmentVariable("MY_KEY_iurh", "No");
        bool v4 = NetBricks.Config.GetOnce("MY_KEY_iurh").AsBool(() => true);
        Assert.False(v4);

        // nO
        System.Environment.SetEnvironmentVariable("MY_KEY_cjfh", "nO");
        bool v5 = NetBricks.Config.GetOnce("MY_KEY_cjfh").AsBool(() => true);
        Assert.False(v5);

        // 0
        System.Environment.SetEnvironmentVariable("MY_KEY_okhg", "0");
        bool v6 = NetBricks.Config.GetOnce("MY_KEY_okhg").AsBool(() => true);
        Assert.False(v6);

    }

    [Fact]
    public void TestGetOnceArray()
    {

        // default
        string[] v0 = NetBricks.Config.GetOnce("MY_KEY_fjru").AsArray(() => null);
        Assert.Null(v0);

        // set legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_roir", "value1, value2");
        string[] v1 = NetBricks.Config.GetOnce("MY_KEY_roir").AsArray(() => null);
        Assert.Equal(2, v1.Length);
        Assert.Equal("value1", v1[0]);
        Assert.Equal("value2", v1[1]);

        // set bad value
        System.Environment.SetEnvironmentVariable("MY_KEY_fjju", "");
        string[] v2 = NetBricks.Config.GetOnce("MY_KEY_fjju").AsArray(() => null);
        Assert.Null(v2);

    }

    [Fact]
    public async Task TestGetSecretArray()
    {

        // default
        string[] v0 = await Config.GetSecretAsync<string[]>("MY_KEY_fjeu", str => str.AsArray(() => null));
        Assert.Null(v0);

        // set legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_fjey", "value1, value2");
        string[] v1 = await Config.GetSecretAsync<string[]>("MY_KEY_fjey", str => str.AsArray(() => null));
        Assert.Equal(2, v1.Length);
        Assert.Equal("value1", v1[0]);
        Assert.Equal("value2", v1[1]);

        // verify cache
        bool wasInCache = Config.TryGetFromCache<string[]>("MY_KEY_fjey", out string[] v2);
        Assert.True(wasInCache);
        Assert.Equal(2, v2.Length);
        Assert.Equal("value1", v2[0]);
        Assert.Equal("value2", v2[1]);

        // set bad value
        System.Environment.SetEnvironmentVariable("MY_KEY_uucs", "");
        string[] v3 = await Config.GetSecretAsync<string[]>("MY_KEY_uucs", str => str.AsArray(() => null));
        Assert.Null(v3);

    }

    [Fact]
    public void TestGetArray()
    {

        // default
        string[] v0 = Config.Get<string[]>("MY_KEY_fjrr", str => str.AsArray(() => null));
        Assert.Null(v0);

        // set legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_zdjf", "value1, value2");
        string[] v1 = Config.Get<string[]>("MY_KEY_zdjf", str => str.AsArray(() => null));
        Assert.Equal(2, v1.Length);
        Assert.Equal("value1", v1[0]);
        Assert.Equal("value2", v1[1]);

        // verify cache
        bool wasInCache = Config.TryGetFromCache<string[]>("MY_KEY_zdjf", out string[] v2);
        Assert.True(wasInCache);
        Assert.Equal(2, v2.Length);
        Assert.Equal("value1", v2[0]);
        Assert.Equal("value2", v2[1]);

        // set bad value
        System.Environment.SetEnvironmentVariable("MY_KEY_rjgu", "");
        string[] v3 = Config.Get<string[]>("MY_KEY_rjgu", str => str.AsArray(() => null));
        Assert.Null(v3);

    }

    [Fact]
    public void TestArrayLimits()
    {

        // 0 elements - null
        System.Environment.SetEnvironmentVariable("MY_KEY_riri", "");
        string[] v0 = NetBricks.Config.GetOnce("MY_KEY_riri").AsArray(() => null);
        Assert.Null(v0);

        // 0 elements - empty
        string[] v1 = NetBricks.Config.GetOnce("MY_KEY_riri").AsArray(() => new string[] { });
        Assert.Empty(v1);

        // 1 elements
        System.Environment.SetEnvironmentVariable("MY_KEY_jhgg", "value1");
        string[] v2 = NetBricks.Config.GetOnce("MY_KEY_jhgg").AsArray(() => null);
        Assert.Single(v2);
        Assert.Equal("value1", v2[0]);

        // 2 elements - without space
        System.Environment.SetEnvironmentVariable("MY_KEY_jhei", "value1,value2");
        string[] v3 = NetBricks.Config.GetOnce("MY_KEY_jhei").AsArray(() => null);
        Assert.Equal(2, v3.Length);
        Assert.Equal("value1", v3[0]);
        Assert.Equal("value2", v3[1]);

        // 2 elements - with space
        System.Environment.SetEnvironmentVariable("MY_KEY_jfue", "value1, value2");
        string[] v4 = NetBricks.Config.GetOnce("MY_KEY_jfue").AsArray(() => null);
        Assert.Equal(2, v4.Length);
        Assert.Equal("value1", v4[0]);
        Assert.Equal("value2", v4[1]);

    }

    private enum TestEnum
    {
        Unknown,
        Value1,
        Value2
    }

    [Fact]
    public void TestGetOnceEnum()
    {

        // default
        TestEnum v0 = NetBricks.Config.GetOnce("MY_KEY_rire").AsEnum<TestEnum>(() => TestEnum.Unknown);
        Assert.Equal(TestEnum.Unknown, v0);

        // set legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_rrrr", "Value1");
        TestEnum v1 = NetBricks.Config.GetOnce("MY_KEY_rrrr").AsEnum<TestEnum>(() => TestEnum.Unknown);
        Assert.Equal(TestEnum.Value1, v1);

        // set bad value
        System.Environment.SetEnvironmentVariable("MY_KEY_fuyr", "Snowman");
        TestEnum v2 = NetBricks.Config.GetOnce("MY_KEY_fuyr").AsEnum<TestEnum>(() => TestEnum.Unknown);
        Assert.Equal(TestEnum.Unknown, v2);

    }

    [Fact]
    public async Task TestGetSecretEnum()
    {

        // default
        TestEnum v0 = await Config.GetSecretAsync<TestEnum>("MY_KEY_eury", str => str.AsEnum<TestEnum>(() => TestEnum.Unknown));
        Assert.Equal(TestEnum.Unknown, v0);

        // set legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_riti", "Value1");
        TestEnum v1 = await Config.GetSecretAsync<TestEnum>("MY_KEY_riti", str => str.AsEnum<TestEnum>(() => TestEnum.Unknown));
        Assert.Equal(TestEnum.Value1, v1);

        // set bad value
        System.Environment.SetEnvironmentVariable("MY_KEY_ruru", "Snowman");
        TestEnum v2 = await Config.GetSecretAsync<TestEnum>("MY_KEY_ruru", str => str.AsEnum<TestEnum>(() => TestEnum.Unknown));
        Assert.Equal(TestEnum.Unknown, v2);

    }

    [Fact]
    public void TestGetEnum()
    {

        // default
        TestEnum v0 = Config.Get<TestEnum>("MY_KEY_riru", str => str.AsEnum<TestEnum>(() => TestEnum.Unknown));
        Assert.Equal(TestEnum.Unknown, v0);

        // set legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_fjur", "Value1");
        TestEnum v1 = Config.Get<TestEnum>("MY_KEY_fjur", str => str.AsEnum<TestEnum>(() => TestEnum.Unknown));
        Assert.Equal(TestEnum.Value1, v1);

        // set bad value
        System.Environment.SetEnvironmentVariable("MY_KEY_iuyt", "Snowman");
        TestEnum v2 = Config.Get<TestEnum>("MY_KEY_iuyt", str => str.AsEnum<TestEnum>(() => TestEnum.Unknown));
        Assert.Equal(TestEnum.Unknown, v2);

    }

    [Fact]
    public void TestEnumCasings()
    {

        // lowercase
        System.Environment.SetEnvironmentVariable("MY_KEY_zjfh", "value1");
        TestEnum v0 = Config.Get<TestEnum>("MY_KEY_zjfh", str => str.AsEnum<TestEnum>(() => TestEnum.Unknown));
        Assert.Equal(TestEnum.Value1, v0);

        // uppercase
        System.Environment.SetEnvironmentVariable("MY_KEY_zjfh", "VALUE1");
        TestEnum v1 = Config.Get<TestEnum>("MY_KEY_zjfh", str => str.AsEnum<TestEnum>(() => TestEnum.Unknown));
        Assert.Equal(TestEnum.Value1, v1);

        // mixed case
        System.Environment.SetEnvironmentVariable("MY_KEY_zjfh", "VaLue1");
        TestEnum v2 = Config.Get<TestEnum>("MY_KEY_zjfh", str => str.AsEnum<TestEnum>(() => TestEnum.Unknown));
        Assert.Equal(TestEnum.Value1, v2);

    }

    [Fact]
    public void TestEnumMap()
    {

        // define map
        Func<string, string> map = str =>
        {
            if (str == "alt-spelling") return "Value2";
            return str;
        };

        // check for successful mapping
        System.Environment.SetEnvironmentVariable("MY_KEY_rjur", "alt-spelling");
        TestEnum v0 = Config.Get<TestEnum>("MY_KEY_rjur", str =>
        {
            return str.AsEnum<TestEnum>(() => TestEnum.Unknown, map);
        });
        Assert.Equal(TestEnum.Value2, v0);

        // check for something not in the map, but still a legit value
        System.Environment.SetEnvironmentVariable("MY_KEY_rirr", "Value1");
        TestEnum v1 = Config.Get<TestEnum>("MY_KEY_rirr", str =>
        {
            return str.AsEnum<TestEnum>(() => TestEnum.Unknown, map);
        });
        Assert.Equal(TestEnum.Value1, v1);

        // check for something not in the map at all, should be default
        System.Environment.SetEnvironmentVariable("MY_KEY_qqqr", "nada");
        TestEnum v2 = Config.Get<TestEnum>("MY_KEY_qqqr", str =>
        {
            return str.AsEnum<TestEnum>(() => TestEnum.Unknown, map);
        });
        Assert.Equal(TestEnum.Unknown, v2);

    }

    [Fact]
    public void TestConfigProvider()
    {

        // configure services
        var builder = new HostBuilder().ConfigureServices((hostContext, services) =>
        {
            services.AddSingleLineConsoleLogger(logParams: false);
            services.AddSingleton<IConfigProvider>(_ =>
            {
                var provider = new DictConfigProvider();
                provider.Add("MY_KEY_vbvb", "MY_VALUE");
                return provider;
            });
            services.AddConfig();
        }).UseConsoleLifetime();
        var host = builder.Build();

        // get config
        var config = host.Services.GetService<IConfig>();

        // ensure the ConfigProvider was used
        string val = config.Get<string>("MY_KEY_vbvb");
        Assert.Equal("MY_VALUE", val);
        string noval = config.Get<string>("MY_KEY");
        Assert.Null(noval);

    }

    [Fact]
    public void TestChain()
    {

        // setup
        System.Environment.SetEnvironmentVariable("MY_KEY_irrn", "value1");
        System.Environment.SetEnvironmentVariable("MY_KEY_iiir", "value2");

        // test that first is used
        var v0 = Config.Get<string>("MY_KEY_irrn, MY_KEY_iiir");
        Assert.Equal("value1", v0);

        // test that second is used
        var v1 = Config.Get<string>("MY_KEY_not1, MY_KEY_iiir");
        Assert.Equal("value2", v1);

        // test that none are used
        var v2 = Config.Get<string>("MY_KEY_not1, MY_KEY_not2");
        Assert.Null(v2);

    }

    [Fact]
    public void TestOptionalArray()
    {

        var v0 = Config.Get<string>("MY_KEY_foor").AsArray(() => new string[] { null });
        NetBricks.Config.Optional("MY_KEY_foor", v0);

        // asset no error

    }

    public void Dispose()
    {
        this.Host.Dispose();
    }

}
