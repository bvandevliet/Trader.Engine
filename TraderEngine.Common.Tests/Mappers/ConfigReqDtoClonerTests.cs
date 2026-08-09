using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Mappers;

namespace TraderEngine.Common.Tests.Mappers;

[TestClass]
public class ConfigReqDtoClonerTests
{
  [TestMethod]
  public void DeepClone_CopiesScalarValues()
  {
    // Arrange
    var source = new ConfigReqDto { UseLimitOrders = true, QuoteTakeout = 50, TopRankingCount = 7 };

    // Act
    var clone = source.DeepClone();

    // Assert
    Assert.AreNotSame(source, clone);
    Assert.IsTrue(clone.UseLimitOrders);
    Assert.AreEqual(50m, clone.QuoteTakeout);
    Assert.AreEqual(7, clone.TopRankingCount);
  }

  [TestMethod]
  public void DeepClone_MutatingCloneCollections_DoesNotAffectSource()
  {
    // Arrange
    // The bug UseDeepCloning guards against: a MemberwiseClone shallow copy would share these
    // Dictionary/List references between source and clone, so mutating one mutates both.
    var source = new ConfigReqDto();
    source.AltWeightingFactors["BTC"] = 1.5;
    source.TagsToInclude.Add("layer1");
    source.TagsToIgnore.Add("meme");

    // Act
    var clone = source.DeepClone();
    clone.AltWeightingFactors["BTC"] = 99;
    clone.AltWeightingFactors["ETH"] = 2;
    clone.TagsToInclude.Add("added-to-clone-only");
    clone.TagsToIgnore.Clear();

    // Assert
    Assert.AreNotSame(source.AltWeightingFactors, clone.AltWeightingFactors);
    Assert.AreNotSame(source.TagsToInclude, clone.TagsToInclude);
    Assert.AreNotSame(source.TagsToIgnore, clone.TagsToIgnore);

    Assert.AreEqual(1.5, source.AltWeightingFactors["BTC"]);
    Assert.IsFalse(source.AltWeightingFactors.ContainsKey("ETH"));
    Assert.AreEqual(1, source.TagsToInclude.Count);
    Assert.AreEqual(2, source.TagsToIgnore.Count); // Default-seeded "stablecoin" + "meme".
    Assert.AreEqual(0, clone.TagsToIgnore.Count); // Cleared on the clone only.
  }

  [TestMethod]
  public void DeepClone_MutatingSourceCollectionsAfterClone_DoesNotAffectClone()
  {
    // Arrange
    var source = new ConfigReqDto();
    source.TagsToInclude.Add("layer1");

    // Act
    var clone = source.DeepClone();
    source.TagsToInclude.Add("added-to-source-only");

    // Assert
    Assert.AreEqual(2, source.TagsToInclude.Count);
    Assert.AreEqual(1, clone.TagsToInclude.Count);
  }
}
