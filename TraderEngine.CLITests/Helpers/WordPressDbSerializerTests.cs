using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.CLI.Helpers.Tests;

[TestClass()]
public class WordPressDbSerializerTests
{
  private static readonly ConfigReqDto _configDto = new()
  {
    QuoteTakeout = 0,
    QuoteAllocation = 0,
    AltWeightingFactors = new() { { "BTC", .9m }, { "DOGE", 0 }, },
    TagsToIgnore = new() { "stablecoin", "meme", },
    TopRankingCount = 10,
    Smoothing = 8,
    NthRoot = 2,
    MinimumDiffQuote = 15,
    MinimumDiffAllocation = 1.5,
    AutomationEnabled = true,
    IntervalHours = 6,
    LastRebalance = new DateTime(2022, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc),
  };

  private static readonly string _serializedConfigDto =
    "O:12:\"ConfigReqDto\":12:{" +
    "s:12:\"QuoteTakeout\";d:0;" +
    "s:15:\"QuoteAllocation\";d:0;" +
    "s:19:\"AltWeightingFactors\";a:2:{s:3:\"BTC\";d:0.9;s:4:\"DOGE\";d:0;}" +
    "s:12:\"TagsToIgnore\";a:2:{i:0;s:10:\"stablecoin\";i:1;s:4:\"meme\";}" +
    "s:15:\"TopRankingCount\";i:10;" +
    "s:9:\"Smoothing\";i:8;" +
    "s:7:\"NthRoot\";d:2;" +
    "s:16:\"MinimumDiffQuote\";i:15;" +
    "s:21:\"MinimumDiffAllocation\";d:1.5;" +
    "s:17:\"AutomationEnabled\";b:1;" +
    "s:13:\"IntervalHours\";i:6;" +
    "s:13:\"LastRebalance\";O:8:\"DateTime\":3:{s:4:\"date\";s:26:\"2022-10-24 00:00:00.000000\";s:13:\"timezone_type\";i:3;s:8:\"timezone\";s:3:\"Utc\";}}";

  [TestMethod()]
  public void SerializeBasicTypesTest()
  {
    WordPressDbSerializer.Serialize(null)
      .Should().Be("N;");

    WordPressDbSerializer.Serialize("test")
      .Should().Be("s:4:\"test\";");

    WordPressDbSerializer.Serialize(16)
      .Should().Be("i:16;");

    WordPressDbSerializer.Serialize(14.32)
      .Should().Be("d:14.32;");

    WordPressDbSerializer.Serialize(true)
      .Should().Be("b:1;");

    WordPressDbSerializer.Serialize(false)
      .Should().Be("b:0;");

    WordPressDbSerializer.Serialize(new DateTime(2022, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc))
      .Should().Be("O:8:\"DateTime\":3:{s:4:\"date\";s:26:\"2022-10-24 00:00:00.000000\";s:13:\"timezone_type\";i:3;s:8:\"timezone\";s:3:\"Utc\";}");

    WordPressDbSerializer.Serialize(new List<int> { 1, 2, 3 })
      .Should().Be("a:3:{i:0;i:1;i:1;i:2;i:2;i:3;}");

    WordPressDbSerializer.Serialize(new int[] { 1, 2, 3 })
      .Should().Be("a:3:{i:0;i:1;i:1;i:2;i:2;i:3;}");

    WordPressDbSerializer.Serialize(new List<string> { "a", "b", "c" })
      .Should().Be("a:3:{i:0;s:1:\"a\";i:1;s:1:\"b\";i:2;s:1:\"c\";}");

    WordPressDbSerializer.Serialize(new string[] { "a", "b", "c" })
      .Should().Be("a:3:{i:0;s:1:\"a\";i:1;s:1:\"b\";i:2;s:1:\"c\";}");

    WordPressDbSerializer.Serialize(new List<bool> { true, false, true })
      .Should().Be("a:3:{i:0;b:1;i:1;b:0;i:2;b:1;}");

    WordPressDbSerializer.Serialize(new bool[] { true, false, true })
      .Should().Be("a:3:{i:0;b:1;i:1;b:0;i:2;b:1;}");

    WordPressDbSerializer.Serialize(new Dictionary<string, decimal>() { { "first", 1.2m }, { "second", 2.1m }, })
      .Should().Be("a:2:{s:5:\"first\";d:1.2;s:6:\"second\";d:2.1;}");

    WordPressDbSerializer.Serialize(new Dictionary<string, string>() { { "first", "tsrif" }, { "second", "dnoces" }, })
      .Should().Be("a:2:{s:5:\"first\";s:5:\"tsrif\";s:6:\"second\";s:6:\"dnoces\";}");

    WordPressDbSerializer.Serialize(new Dictionary<int, string>() { { 1, "first" }, { 2, "second" }, })
      .Should().Be("a:2:{i:1;s:5:\"first\";i:2;s:6:\"second\";}");
  }

  [TestMethod()]
  public void DeserializeBasicTypesTest()
  {
    WordPressDbSerializer.Deserialize<string>("s:4:\"test\";")
        .Should().Be("test");

    WordPressDbSerializer.Deserialize<int>("i:16;")
        .Should().Be(16);

    WordPressDbSerializer.Deserialize<double>("d:14.32;")
        .Should().Be(14.32);

    WordPressDbSerializer.Deserialize<bool>("b:1;")
        .Should().BeTrue();

    WordPressDbSerializer.Deserialize<List<int>>("a:3:{i:0;i:1;i:1;i:2;i:2;i:3;}")
        .Should().BeEquivalentTo(new List<int> { 1, 2, 3 });

    WordPressDbSerializer.Deserialize<int[]>("a:3:{i:0;i:1;i:1;i:2;i:2;i:3;}")
        .Should().BeEquivalentTo(new int[] { 1, 2, 3 });

    WordPressDbSerializer.Deserialize<List<string>>("a:3:{i:0;s:1:\"a\";i:1;s:1:\"b\";i:2;s:1:\"c\";}")
        .Should().BeEquivalentTo(new List<string> { "a", "b", "c" });

    WordPressDbSerializer.Deserialize<string[]>("a:3:{i:0;s:1:\"a\";i:1;s:1:\"b\";i:2;s:1:\"c\";}")
        .Should().BeEquivalentTo(new string[] { "a", "b", "c" });

    WordPressDbSerializer.Deserialize<List<bool>>("a:3:{i:0;b:1;i:1;b:0;i:2;b:1;}")
        .Should().BeEquivalentTo(new List<bool> { true, false, true });

    WordPressDbSerializer.Deserialize<bool[]>("a:3:{i:0;b:1;i:1;b:0;i:2;b:1;}")
        .Should().BeEquivalentTo(new bool[] { true, false, true });

    WordPressDbSerializer.Deserialize<Dictionary<string, decimal>>("a:2:{s:5:\"first\";d:1.2;s:6:\"second\";d:2.1;}")
        .Should().BeEquivalentTo(new Dictionary<string, decimal>() { { "first", 1.2m }, { "second", 2.1m } });

    WordPressDbSerializer.Deserialize<Dictionary<string, string>>("a:2:{s:5:\"first\";s:5:\"tsrif\";s:6:\"second\";s:6:\"dnoces\";}")
        .Should().BeEquivalentTo(new Dictionary<string, string>() { { "first", "tsrif" }, { "second", "dnoces" } });

    WordPressDbSerializer.Deserialize<Dictionary<int, string>>("a:2:{i:1;s:5:\"first\";i:2;s:6:\"second\";}")
      .Should().BeEquivalentTo(new Dictionary<int, string>() { { 1, "first" }, { 2, "second" } });
  }

  [TestMethod()]
  public void SerializeCustomTypesTest()
  {
    string result = WordPressDbSerializer.Serialize(_configDto);

    result.Should().Be(_serializedConfigDto);
  }

  [TestMethod()]
  public void DeserializeCustomTypesTest()
  {
    var result = WordPressDbSerializer.Deserialize<ConfigReqDto>(_serializedConfigDto);

    result.Should().BeEquivalentTo(_configDto);
  }
}