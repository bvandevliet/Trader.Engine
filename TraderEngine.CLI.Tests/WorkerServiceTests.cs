using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;

namespace TraderEngine.CLI.Tests;

[TestClass()]
public class WorkerServiceTests
{
  #region IsEligibleForRebalance Tests

  [TestMethod]
  public void IsEligibleForRebalance_NoOrders_ReturnsFalse()
  {
    // Arrange
    var configReqDto = new ConfigReqDto
    {
      MinimumDiffQuote = 5,
      MinimumDiffAllocation = 1,
    };

    var simulated = new SimulationDto
    {
      Orders = [],
      CurBalance = new BalanceDto
      {
        AmountQuoteTotal = 1000,
        AmountQuoteAvailable = 100,
      },
      NewBalance = new BalanceDto
      {
        AmountQuoteTotal = 1000,
        AmountQuoteAvailable = 100,
      }
    };

    // Act
    bool result = WorkerService.IsEligibleForRebalance(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void IsEligibleForRebalance_QuoteDiffBelowMinimum_ReturnsFalse()
  {
    // Arrange
    var configReqDto = new ConfigReqDto
    {
      MinimumDiffQuote = 20,
      MinimumDiffAllocation = 1,
    };

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto
        {
          AmountQuoteFilled = 15, // 1.5% of 1000
          Market = new MarketReqDto("EUR", "BTC"),
        }
      ],
      CurBalance = new BalanceDto
      {
        AmountQuoteTotal = 1000,
        AmountQuoteAvailable = 100,
      },
      NewBalance = new BalanceDto
      {
        AmountQuoteTotal = 1000,
        AmountQuoteAvailable = 100,
      }
    };

    // Act
    bool result = WorkerService.IsEligibleForRebalance(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void IsEligibleForRebalance_AllocDiffBelowMinimum_ReturnsFalse()
  {
    // Arrange
    var configReqDto = new ConfigReqDto
    {
      MinimumDiffQuote = 10,
      MinimumDiffAllocation = 2,
    };

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto
        {
          AmountQuoteFilled = 15, // 1.5% of 1000
          Market = new MarketReqDto("EUR", "BTC"),
        }
      ],
      CurBalance = new BalanceDto
      {
        AmountQuoteTotal = 1000,
        AmountQuoteAvailable = 100,
      },
      NewBalance = new BalanceDto
      {
        AmountQuoteTotal = 1000,
        AmountQuoteAvailable = 100,
      }
    };

    // Act
    bool result = WorkerService.IsEligibleForRebalance(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void IsEligibleForRebalance_OrderExceedsMinimumAmount_ReturnsTrue()
  {
    // Arrange
    var configReqDto = new ConfigReqDto
    {
      MinimumDiffQuote = 10,
      MinimumDiffAllocation = 1,
    };

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto
        {
          AmountQuoteFilled = 15, // 1.5% of 1000
          Market = new MarketReqDto("EUR", "BTC"),
        }
      ],
      CurBalance = new BalanceDto
      {
        AmountQuoteTotal = 1000,
        AmountQuoteAvailable = 100,
      },
      NewBalance = new BalanceDto
      {
        AmountQuoteTotal = 1000,
        AmountQuoteAvailable = 100,
      }
    };

    // Act
    bool result = WorkerService.IsEligibleForRebalance(configReqDto, simulated);

    // Assert
    Assert.IsTrue(result);
  }

  #endregion

  #region HasNonContiguousFullSellOrder Tests

  [TestMethod]
  public void HasNonContiguousFullSellOrder_SmallestAllocationFullSell_ReturnsFalse()
  {
    // Arrange
    var configReqDto = new ConfigReqDto
    {
      MinimumDiffQuote = 5,
    };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto
        {
          Market = marketBnb,
          Side = OrderSide.Sell,
          Amount = 100,
        },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto()
          {
            Market = marketBtc,
            AmountQuote = 100,
            Amount = 2
          },
          new AllocationDto()
          {
            Market = marketEth,
            AmountQuote = 70,
            Amount = 10
          },
          new AllocationDto()
          {
            Market = marketAda,
            AmountQuote = 15,
            Amount = 100
          },
          new AllocationDto()
          {
            Market = marketBnb,
            AmountQuote = 10,
            Amount = 100
          },
        ]
      }
    };

    // Act
    bool result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_AllFullSellOrdersContiguous_ReturnsFalse()
  {
    // Arrange
    var configReqDto = new ConfigReqDto
    {
      MinimumDiffQuote = 5,
    };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto
        {
          Market = marketAda,
          Side = OrderSide.Sell,
          Amount = 100,
        },
        new OrderDto
        {
          Market = marketBnb,
          Side = OrderSide.Sell,
          Amount = 100,
        },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto()
          {
            Market = marketBtc,
            AmountQuote = 100,
            Amount = 2
          },
          new AllocationDto()
          {
            Market = marketEth,
            AmountQuote = 70,
            Amount = 10
          },
          new AllocationDto()
          {
            Market = marketAda,
            AmountQuote = 15,
            Amount = 100
          },
          new AllocationDto()
          {
            Market = marketBnb,
            AmountQuote = 10,
            Amount = 100
          },
        ]
      }
    };

    // Act
    bool result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_NonContiguousFullSell_ReturnsTrue()
  {
    // Arrange
    var configReqDto = new ConfigReqDto
    {
      MinimumDiffQuote = 5,
    };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto
        {
          Market = marketEth,
          Side = OrderSide.Sell,
          Amount = 10,
        },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto()
          {
            Market = marketBtc,
            AmountQuote = 100,
            Amount = 2
          },
          new AllocationDto()
          {
            Market = marketEth,
            AmountQuote = 70,
            Amount = 10
          },
          new AllocationDto()
          {
            Market = marketAda,
            AmountQuote = 15,
            Amount = 100
          },
          new AllocationDto()
          {
            Market = marketBnb,
            AmountQuote = 10,
            Amount = 100
          },
        ]
      }
    };

    // Act
    bool result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsTrue(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_SmallestAllocationAndNonContiguousFullSell_ReturnsTrue()
  {
    // Arrange
    var configReqDto = new ConfigReqDto
    {
      MinimumDiffQuote = 5,
    };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto
        {
          Market = marketEth,
          Side = OrderSide.Sell,
          Amount = 10,
        },
        new OrderDto
        {
          Market = marketBnb,
          Side = OrderSide.Sell,
          Amount = 100,
        },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto()
          {
            Market = marketBtc,
            AmountQuote = 100,
            Amount = 2
          },
          new AllocationDto()
          {
            Market = marketEth,
            AmountQuote = 70,
            Amount = 10
          },
          new AllocationDto()
          {
            Market = marketAda,
            AmountQuote = 15,
            Amount = 100
          },
          new AllocationDto()
          {
            Market = marketBnb,
            AmountQuote = 10,
            Amount = 100
          },
        ]
      }
    };

    // Act
    bool result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsTrue(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_SmallestAllocationFullSell_IgnoresAllocationsBelowMinimumDiff_ReturnsFalse()
  {
    // Arrange
    var configReqDto = new ConfigReqDto
    {
      MinimumDiffQuote = 5,
    };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");
    var marketLtc = new MarketReqDto("EUR", "LTC");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto
        {
          Market = marketBnb,
          Side = OrderSide.Sell,
          Amount = 100,
        },
        new OrderDto
        {
          Market = marketLtc,
          Side = OrderSide.Sell,
          Amount = 5,
        },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto()
          {
            Market = marketBtc,
            AmountQuote = 100,
            Amount = 2
          },
          new AllocationDto()
          {
            Market = marketEth,
            AmountQuote = 70,
            Amount = 10
          },
          new AllocationDto()
          {
            Market = marketAda,
            AmountQuote = 15,
            Amount = 100
          },
          new AllocationDto()
          {
            Market = marketBnb,
            AmountQuote = 10,
            Amount = 100
          },
          new AllocationDto()
          {
            Market = marketLtc,
            AmountQuote = 1,
            Amount = 5
          },
        ]
      }
    };

    // Act
    bool result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_AllFullSellOrdersContiguous_IgnoresAllocationsBelowMinimumDiff_ReturnsFalse()
  {
    // Arrange
    var configReqDto = new ConfigReqDto
    {
      MinimumDiffQuote = 5,
    };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");
    var marketLtc = new MarketReqDto("EUR", "LTC");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto
        {
          Market = marketAda,
          Side = OrderSide.Sell,
          Amount = 100,
        },
        new OrderDto
        {
          Market = marketBnb,
          Side = OrderSide.Sell,
          Amount = 100,
        },
        new OrderDto
        {
          Market = marketLtc,
          Side = OrderSide.Sell,
          Amount = 5,
        },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto()
          {
            Market = marketBtc,
            AmountQuote = 100,
            Amount = 2
          },
          new AllocationDto()
          {
            Market = marketEth,
            AmountQuote = 70,
            Amount = 10
          },
          new AllocationDto()
          {
            Market = marketAda,
            AmountQuote = 15,
            Amount = 100
          },
          new AllocationDto()
          {
            Market = marketBnb,
            AmountQuote = 10,
            Amount = 100
          },
          new AllocationDto()
          {
            Market = marketLtc,
            AmountQuote = 1,
            Amount = 5
          },
        ]
      }
    };

    // Act
    bool result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_NonContiguousFullSell_IgnoresAllocationsBelowMinimumDiff_ReturnsTrue()
  {
    // Arrange
    var configReqDto = new ConfigReqDto
    {
      MinimumDiffQuote = 5,
    };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");
    var marketLtc = new MarketReqDto("EUR", "LTC");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto
        {
          Market = marketEth,
          Side = OrderSide.Sell,
          Amount = 10,
        },
        new OrderDto
        {
          Market = marketLtc,
          Side = OrderSide.Sell,
          Amount = 5,
        },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto()
          {
            Market = marketBtc,
            AmountQuote = 100,
            Amount = 2
          },
          new AllocationDto()
          {
            Market = marketEth,
            AmountQuote = 70,
            Amount = 10
          },
          new AllocationDto()
          {
            Market = marketAda,
            AmountQuote = 15,
            Amount = 100
          },
          new AllocationDto()
          {
            Market = marketBnb,
            AmountQuote = 10,
            Amount = 100
          },
          new AllocationDto()
          {
            Market = marketLtc,
            AmountQuote = 1,
            Amount = 5
          },
        ]
      }
    };

    // Act
    bool result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsTrue(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_SmallestAllocationAndNonContiguousFullSell_IgnoresAllocationsBelowMinimumDiff_ReturnsTrue()
  {
    // Arrange
    var configReqDto = new ConfigReqDto
    {
      MinimumDiffQuote = 5,
    };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");
    var marketLtc = new MarketReqDto("EUR", "LTC");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto
        {
          Market = marketEth,
          Side = OrderSide.Sell,
          Amount = 10,
        },
        new OrderDto
        {
          Market = marketBnb,
          Side = OrderSide.Sell,
          Amount = 100,
        },
        new OrderDto
        {
          Market = marketLtc,
          Side = OrderSide.Sell,
          Amount = 5,
        },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto()
          {
            Market = marketBtc,
            AmountQuote = 100,
            Amount = 2
          },
          new AllocationDto()
          {
            Market = marketEth,
            AmountQuote = 70,
            Amount = 10
          },
          new AllocationDto()
          {
            Market = marketAda,
            AmountQuote = 15,
            Amount = 100
          },
          new AllocationDto()
          {
            Market = marketBnb,
            AmountQuote = 10,
            Amount = 100
          },
          new AllocationDto()
          {
            Market = marketLtc,
            AmountQuote = 1,
            Amount = 5
          },
        ]
      }
    };

    // Act
    bool result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsTrue(result);
  }

  #endregion
}