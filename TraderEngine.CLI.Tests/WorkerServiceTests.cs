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
    var result = WorkerService.IsEligibleForRebalance(configReqDto, simulated);

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
    var result = WorkerService.IsEligibleForRebalance(configReqDto, simulated);

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
    var result = WorkerService.IsEligibleForRebalance(configReqDto, simulated);

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
    var result = WorkerService.IsEligibleForRebalance(configReqDto, simulated);

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
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

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
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

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
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

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
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

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
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

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
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

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
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

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
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsTrue(result);
  }

  // ── No orders / trivial edge cases ────────────────────────────────────────

  [TestMethod]
  public void HasNonContiguousFullSellOrder_NoSellOrders_ReturnsFalse()
  {
    // Arrange
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 5 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");

    var simulated = new SimulationDto
    {
      Orders = [],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2 },
          new AllocationDto { Market = marketEth, AmountQuote = 70, Amount = 10 },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_AllAllocationsFullySold_ReturnsFalse()
  {
    // Arrange
    // Selling everything is contiguous by definition — no "kept" allocation creates a gap.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 5 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketAda, Side = OrderSide.Sell, Amount = 100 },
        new OrderDto { Market = marketEth, Side = OrderSide.Sell, Amount = 10 },
        new OrderDto { Market = marketBtc, Side = OrderSide.Sell, Amount = 2 },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2 },
          new AllocationDto { Market = marketEth, AmountQuote = 70, Amount = 10 },
          new AllocationDto { Market = marketAda, AmountQuote = 15, Amount = 100 },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_SingleAllocationFullySold_ReturnsFalse()
  {
    // Arrange
    // Only one allocation — nothing to be non-contiguous with.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 5 };

    var marketEth = new MarketReqDto("EUR", "ETH");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketEth, Side = OrderSide.Sell, Amount = 10 },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketEth, AmountQuote = 70, Amount = 10 },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  // ── Partial sells ──────────────────────────────────────────────────────────

  [TestMethod]
  public void HasNonContiguousFullSellOrder_PartialSellOnLargerAllocation_ReturnsFalse()
  {
    // Arrange
    // ETH is being partially sold (order.Amount != allocation.Amount), so it does not
    // count as a full sell even though smaller allocations are kept.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 5 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");

    var simulated = new SimulationDto
    {
      Orders =
      [
        // Sell 5 out of 10 ETH — partial, not a full sell.
        new OrderDto { Market = marketEth, Side = OrderSide.Sell, Amount = 5 },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2 },
          new AllocationDto { Market = marketEth, AmountQuote = 70, Amount = 10 },
          new AllocationDto { Market = marketAda, AmountQuote = 15, Amount = 100 },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_PartialSellOnSmallest_BuyOnLarger_ReturnsFalse()
  {
    // Arrange
    // The smallest allocation (ADA) is partially reduced and the proceeds go to buying more ETH.
    // potentialGapFound is set when ADA is processed (it's not a full sell), but since no
    // allocation is fully sold, Any() never returns true — no false positive.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 5 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketAda, Side = OrderSide.Sell, Amount = 50 }, // partial: allocation.Amount = 100
        new OrderDto { Market = marketEth, Side = OrderSide.Buy,  Amount = null, AmountQuote = 7 },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2   },
          new AllocationDto { Market = marketEth, AmountQuote = 70,  Amount = 10  },
          new AllocationDto { Market = marketAda, AmountQuote = 15,  Amount = 100 },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_PartialSellOnSmallest_FullSellOnLarger_ReturnsTrue()
  {
    // Arrange
    // ADA (smallest) is partially reduced — treated as "kept" because it's not a full sell,
    // so potentialGapFound is set to true. ETH (larger) is fully sold alongside this.
    // The method correctly identifies the non-contiguous full sell: ETH > ADA and ADA is kept.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 5 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketAda, Side = OrderSide.Sell, Amount = 50 }, // partial: allocation.Amount = 100
        new OrderDto { Market = marketEth, Side = OrderSide.Sell, Amount = 10 }, // full sell: matches allocation.Amount
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2   },
          new AllocationDto { Market = marketEth, AmountQuote = 70,  Amount = 10  },
          new AllocationDto { Market = marketAda, AmountQuote = 15,  Amount = 100 },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert — correctly fires: ETH (70) is fully sold while ADA (15) is still in the portfolio
    // (partially sold = treated as kept). This is the intended detection.
    Assert.IsTrue(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_AllAllocationsPartiallySold_ReturnsFalse()
  {
    // Arrange
    // Every allocation has a partial sell order — none are fully sold.
    // potentialGapFound gets set but Any() never returns true.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 5 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketAda, Side = OrderSide.Sell, Amount = 50  }, // partial
        new OrderDto { Market = marketEth, Side = OrderSide.Sell, Amount = 5   }, // partial
        new OrderDto { Market = marketBtc, Side = OrderSide.Sell, Amount = 1   }, // partial
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2   },
          new AllocationDto { Market = marketEth, AmountQuote = 70,  Amount = 10  },
          new AllocationDto { Market = marketAda, AmountQuote = 15,  Amount = 100 },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  // ── Order side ─────────────────────────────────────────────────────────────

  [TestMethod]
  public void HasNonContiguousFullSellOrder_BuyOrderMatchingFullAmount_ReturnsFalse()
  {
    // Arrange
    // A buy order whose Amount matches the allocation Amount must not be treated as a full sell.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 5 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");

    var simulated = new SimulationDto
    {
      Orders =
      [
        // A buy order for ETH that happens to match the allocation amount — should not trigger.
        new OrderDto { Market = marketEth, Side = OrderSide.Buy, Amount = 10 },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2 },
          new AllocationDto { Market = marketEth, AmountQuote = 70, Amount = 10 },
          new AllocationDto { Market = marketAda, AmountQuote = 15, Amount = 100 },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  // ── Quote allocation in the middle ─────────────────────────────────────────

  [TestMethod]
  public void HasNonContiguousFullSellOrder_QuoteAllocationInMiddle_OnlyCryptoFullySold_ReturnsFalse()
  {
    // Arrange
    // Key false-positive guard: the EUR/EUR quote allocation (AmountQuote=50) sits between the
    // two allocations in sorted order. Without the BaseSymbol != QuoteSymbol filter it would
    // be treated as a "kept" allocation, setting potentialGapFound=true — causing ETH being sold
    // to incorrectly return true. The filter must prevent this.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 5 };

    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketQuote = new MarketReqDto("EUR", "EUR"); // quote self-pair

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketEth, Side = OrderSide.Sell, Amount = 10 },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          // Quote allocation sits below ETH in sorted order; must be excluded from analysis.
          new AllocationDto { Market = marketQuote, AmountQuote = 50, Amount = 50 },
          new AllocationDto { Market = marketEth, AmountQuote = 70, Amount = 10 },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_QuoteAllocationInMiddle_ContiguousSells_ReturnsFalse()
  {
    // Arrange
    // EUR/EUR at 50 sits between ADA (15) and ETH (70) in the sorted order.
    // Selling the two smallest above-threshold allocations is contiguous — should return false.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 5 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");
    var marketQuote = new MarketReqDto("EUR", "EUR");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketBnb, Side = OrderSide.Sell, Amount = 100 },
        new OrderDto { Market = marketAda, Side = OrderSide.Sell, Amount = 100 },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2 },
          new AllocationDto { Market = marketEth, AmountQuote = 70, Amount = 10 },
          // Quote allocation sits between ADA (15) and ETH (70) — must be transparent.
          new AllocationDto { Market = marketQuote, AmountQuote = 50, Amount = 50 },
          new AllocationDto { Market = marketAda, AmountQuote = 15, Amount = 100 },
          new AllocationDto { Market = marketBnb, AmountQuote = 10, Amount = 100 },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_QuoteAllocationInMiddle_NonContiguousFullSell_ReturnsTrue()
  {
    // Arrange
    // EUR/EUR at 50 sits between ADA (15) and ETH (70). Selling ETH while keeping ADA is
    // non-contiguous. The quote allocation must not interfere with gap detection.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 5 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");
    var marketQuote = new MarketReqDto("EUR", "EUR");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketEth, Side = OrderSide.Sell, Amount = 10 },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2 },
          new AllocationDto { Market = marketEth, AmountQuote = 70, Amount = 10 },
          new AllocationDto { Market = marketQuote, AmountQuote = 50, Amount = 50 },
          new AllocationDto { Market = marketAda, AmountQuote = 15, Amount = 100 },
          new AllocationDto { Market = marketBnb, AmountQuote = 10, Amount = 100 },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsTrue(result);
  }

  // ── Dust allocation numerically between above-threshold allocations ─────────

  [TestMethod]
  public void HasNonContiguousFullSellOrder_DustBetweenAboveThresholdAllocations_ContiguousSell_ReturnsFalse()
  {
    // Arrange
    // MinimumDiffQuote=25 makes BNB(10) and XRP(18) dust. ADA(30), ETH(70), BTC(100) are
    // above-threshold. XRP(18) is numerically between BNB(10) and ADA(30) in sorted order,
    // sitting "between" allocations but must be excluded as dust.
    // Selling only ADA (smallest above-threshold) is contiguous — should return false.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 25 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");
    var marketXrp = new MarketReqDto("EUR", "XRP");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketAda, Side = OrderSide.Sell, Amount = 100 },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2 },
          new AllocationDto { Market = marketEth, AmountQuote = 70, Amount = 10 },
          new AllocationDto { Market = marketAda, AmountQuote = 30, Amount = 100 },
          new AllocationDto { Market = marketXrp, AmountQuote = 18, Amount = 200 }, // dust — between BNB and ADA
          new AllocationDto { Market = marketBnb, AmountQuote = 10, Amount = 100 }, // dust
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_DustBetweenAboveThresholdAllocations_NonContiguousFullSell_ReturnsTrue()
  {
    // Arrange
    // Same setup as above but ETH (70) is fully sold while ADA (30) is kept.
    // Dust allocations (BNB=10, XRP=18) must not affect gap detection.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 25 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");
    var marketXrp = new MarketReqDto("EUR", "XRP");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketEth, Side = OrderSide.Sell, Amount = 10 },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2 },
          new AllocationDto { Market = marketEth, AmountQuote = 70, Amount = 10 },
          new AllocationDto { Market = marketAda, AmountQuote = 30, Amount = 100 },
          new AllocationDto { Market = marketXrp, AmountQuote = 18, Amount = 200 }, // dust — between BNB and ADA
          new AllocationDto { Market = marketBnb, AmountQuote = 10, Amount = 100 }, // dust
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsTrue(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_DustAllocationFullySold_AllAboveMinimumKept_ReturnsFalse()
  {
    // Arrange
    // A dust allocation (XRP=18) has a full-sell order (exact amount match). Because it falls
    // below MinimumDiffQuote=25 it is skipped entirely via `continue` — its sell order must
    // not set gapDetected or trigger an early return. The above-minimum allocations are all
    // kept, so no non-contiguous full sell exists.
    //
    // In the previous implementation the dust allocation fell through to `return potentialGapFound`
    // inside the Any() lambda instead of being skipped. Because ascending sort guarantees dust
    // is processed before any above-minimum allocation can set potentialGapFound=true, both
    // implementations produce the same result. This test documents and guards that invariant.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 25 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketXrp = new MarketReqDto("EUR", "XRP");

    var simulated = new SimulationDto
    {
      Orders =
      [
        // Full sell of the dust allocation — must be ignored.
        new OrderDto { Market = marketXrp, Side = OrderSide.Sell, Amount = 200 },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2   },
          new AllocationDto { Market = marketEth, AmountQuote = 70,  Amount = 10  },
          new AllocationDto { Market = marketAda, AmountQuote = 30,  Amount = 100 },
          new AllocationDto { Market = marketXrp, AmountQuote = 18,  Amount = 200 }, // dust
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_DustAllocationFullySold_NonContiguousAboveMinimumFullySold_ReturnsTrue()
  {
    // Arrange
    // Dust allocation (XRP=18) has a full-sell order AND there is a genuine non-contiguous
    // full sell among the above-minimum allocations (ADA=30 kept, ETH=70 fully sold).
    // The dust full-sell must not suppress the correct detection — the method should still
    // return true solely based on the above-minimum allocations.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 25 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketXrp = new MarketReqDto("EUR", "XRP");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketXrp, Side = OrderSide.Sell, Amount = 200 }, // dust full sell — ignored
        new OrderDto { Market = marketEth, Side = OrderSide.Sell, Amount = 10  }, // non-contiguous full sell
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2   },
          new AllocationDto { Market = marketEth, AmountQuote = 70,  Amount = 10  },
          new AllocationDto { Market = marketAda, AmountQuote = 30,  Amount = 100 }, // kept → gapDetected
          new AllocationDto { Market = marketXrp, AmountQuote = 18,  Amount = 200 }, // dust
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsTrue(result);
  }

  // ── Only dust allocations ──────────────────────────────────────────────────

  [TestMethod]
  public void HasNonContiguousFullSellOrder_OnlyDustAllocations_AllFullySold_ReturnsFalse()
  {
    // Arrange
    // All allocations are below MinimumDiffQuote; none are meaningful enough for gap detection.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 50 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketBtc, Side = OrderSide.Sell, Amount = 2 },
        new OrderDto { Market = marketEth, Side = OrderSide.Sell, Amount = 10 },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 40, Amount = 2 },  // dust
          new AllocationDto { Market = marketEth, AmountQuote = 30, Amount = 10 }, // dust
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsFalse(result);
  }

  // ── Real-world balance regression ─────────────────────────────────────────

  [TestMethod]
  public void HasNonContiguousFullSellOrder_RealWorldBalance_AddedEurOnlyBuyOrders_ReturnsFalse()
  {
    // Arrange
    // Reproduces the real-world balance snapshot after the user added EUR to their account.
    // EUR/EUR at 300 sits between CC (284) and HYPE (447) in sorted order but is excluded by the
    // quote filter. FET has an essentially zero AmountQuote and acts as dust.
    // When the rebalance only generates buy orders to deploy the added EUR, no full sell is
    // involved and the method must return false.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 10 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketXrp = new MarketReqDto("EUR", "XRP");
    var marketSol = new MarketReqDto("EUR", "SOL");
    var marketHype = new MarketReqDto("EUR", "HYPE");
    var marketCc = new MarketReqDto("EUR", "CC");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketLink = new MarketReqDto("EUR", "LINK");
    var marketGram = new MarketReqDto("EUR", "GRAM");
    var marketHbar = new MarketReqDto("EUR", "HBAR");
    var marketSui = new MarketReqDto("EUR", "SUI");
    var marketNear = new MarketReqDto("EUR", "NEAR");
    var marketFet = new MarketReqDto("EUR", "FET");
    var marketQuote = new MarketReqDto("EUR", "EUR");

    var simulated = new SimulationDto
    {
      // Exact simulated orders from the failed automation — all market buy orders priced in
      // quote currency (AmountQuote). Amount is null for all market buy orders. None of these
      // can ever satisfy `order.Side == Sell && order.Amount == allocation.Amount`, so the
      // method must return false regardless of the balance.
      Orders =
      [
        new OrderDto { Market = marketCc,   Side = OrderSide.Buy, Amount = null, AmountQuote = 6.02m   },
        new OrderDto { Market = marketHype, Side = OrderSide.Buy, Amount = null, AmountQuote = 18.44m  },
        new OrderDto { Market = marketSui,  Side = OrderSide.Buy, Amount = null, AmountQuote = 6.62m   },
        new OrderDto { Market = marketNear, Side = OrderSide.Buy, Amount = null, AmountQuote = 15.6m   },
        new OrderDto { Market = marketBtc,  Side = OrderSide.Buy, Amount = null, AmountQuote = 18.98m  },
        new OrderDto { Market = marketSol,  Side = OrderSide.Buy, Amount = null, AmountQuote = 12.89m  },
        new OrderDto { Market = marketGram, Side = OrderSide.Buy, Amount = null, AmountQuote = 5.75m   },
        new OrderDto { Market = marketXrp,  Side = OrderSide.Buy, Amount = null, AmountQuote = 14.53m  },
        new OrderDto { Market = marketEth,  Side = OrderSide.Buy, Amount = null, AmountQuote = 95.43m  },
        new OrderDto { Market = marketAda,  Side = OrderSide.Buy, Amount = null, AmountQuote = 5.94m   },
      ],
      // Pre-manual-rebalance balance, reconstructed by subtracting the bought amounts from the
      // post-rebalance snapshot the user shared. LINK and HBAR have no orders (no buy/sell).
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc,   AmountQuote = 1308.67m, Amount = 0.023869113m    },
          new AllocationDto { Market = marketEth,   AmountQuote = 1113.16m, Amount = 0.750839555m    },
          new AllocationDto { Market = marketXrp,   AmountQuote = 835.91m,  Amount = 846.616499844m  },
          new AllocationDto { Market = marketSol,   AmountQuote = 644.36m,  Amount = 10.768532788m   },
          new AllocationDto { Market = marketHype,  AmountQuote = 428.38m,  Amount = 7.269823397m    },
          new AllocationDto { Market = marketCc,    AmountQuote = 278.07m,  Amount = 2017.627202m    },
          new AllocationDto { Market = marketAda,   AmountQuote = 268.85m,  Amount = 1919.312424m    },
          new AllocationDto { Market = marketLink,  AmountQuote = 269.89m,  Amount = 39.23430509m    }, // no order
          new AllocationDto { Market = marketGram,  AmountQuote = 231.01m,  Amount = 161.285343m     },
          new AllocationDto { Market = marketHbar,  AmountQuote = 215.22m,  Amount = 3116.12541567m  }, // no order
          new AllocationDto { Market = marketSui,   AmountQuote = 192.51m,  Amount = 308.070705m     },
          new AllocationDto { Market = marketNear,  AmountQuote = 181.65m,  Amount = 97.069571m      },
          new AllocationDto { Market = marketFet,   AmountQuote = 0.000000001m, Amount = 0.000000008779m },
          new AllocationDto { Market = marketQuote, AmountQuote = 300m,     Amount = 300m            },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert — must be false: all orders are market buys with Amount=null.
    // order.Side == Sell is false for every order, so no allocation can ever be identified
    // as "fully sold". The method cannot return true with buy-only orders.
    // If this test FAILS, it indicates that the automation was blocked by a different check,
    // or that the actual simulation orders at automation time included sell orders not present
    // in the debug data provided.
    Assert.IsFalse(result);
  }

  [TestMethod]
  public void HasNonContiguousFullSellOrder_RealWorldBalance_DroppedCoinFullySoldWhileSmallerCoinsKept_ReturnsTrue()
  {
    // Arrange
    // Same real-world balance snapshot. This time the rebalance also fully sells CC (284 EUR)
    // because it dropped off the market cap list, while NEAR (197), SUI (199), HBAR (215),
    // GRAM (237), LINK (270) and ADA (275) are all kept (they are still in the market cap list).
    // The method correctly identifies CC as a non-contiguous full sell (larger than several kept
    // allocations) and returns true, blocking the automated rebalance as a safety precaution.
    // The user experienced this as a "false positive" — the rebalance intent was legitimate, but
    // the safety check fires because the design cannot distinguish between an intentional market-cap
    // driven full sell and an erroneous one. This test documents the root cause.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 10 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketXrp = new MarketReqDto("EUR", "XRP");
    var marketSol = new MarketReqDto("EUR", "SOL");
    var marketHype = new MarketReqDto("EUR", "HYPE");
    var marketCc = new MarketReqDto("EUR", "CC");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketLink = new MarketReqDto("EUR", "LINK");
    var marketGram = new MarketReqDto("EUR", "GRAM");
    var marketHbar = new MarketReqDto("EUR", "HBAR");
    var marketSui = new MarketReqDto("EUR", "SUI");
    var marketNear = new MarketReqDto("EUR", "NEAR");
    var marketFet = new MarketReqDto("EUR", "FET");
    var marketQuote = new MarketReqDto("EUR", "EUR");

    var simulated = new SimulationDto
    {
      Orders =
      [
        // CC dropped off the market cap list — full sell.
        new OrderDto { Market = marketCc,   Side = OrderSide.Sell, Amount = 2061.79124m },
        // Buy orders for remaining positions using the freed EUR + added EUR.
        new OrderDto { Market = marketBtc,  Side = OrderSide.Buy, Amount = 0.002m },
        new OrderDto { Market = marketEth,  Side = OrderSide.Buy, Amount = 0.1m   },
        new OrderDto { Market = marketXrp,  Side = OrderSide.Buy, Amount = 30m    },
        new OrderDto { Market = marketSol,  Side = OrderSide.Buy, Amount = 0.5m   },
        new OrderDto { Market = marketHype, Side = OrderSide.Buy, Amount = 0.5m   },
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc,   AmountQuote = 1328m,  Amount = 0.02421523m      },
          new AllocationDto { Market = marketEth,   AmountQuote = 1209m,  Amount = 0.81515762m      },
          new AllocationDto { Market = marketXrp,   AmountQuote = 850m,   Amount = 861.274316m      },
          new AllocationDto { Market = marketSol,   AmountQuote = 657m,   Amount = 10.98334106m     },
          new AllocationDto { Market = marketHype,  AmountQuote = 447m,   Amount = 7.58547953m      },
          // CC is being fully sold (2061.79124 == allocation Amount) while NEAR, SUI, HBAR,
          // GRAM, LINK, ADA (all smaller) are kept — this is the non-contiguous gap.
          new AllocationDto { Market = marketCc,    AmountQuote = 284m,   Amount = 2061.79124m      },
          new AllocationDto { Market = marketAda,   AmountQuote = 275m,   Amount = 1961.538912m     },
          new AllocationDto { Market = marketLink,  AmountQuote = 270m,   Amount = 39.23430509m     },
          new AllocationDto { Market = marketGram,  AmountQuote = 237m,   Amount = 165.320431m      },
          new AllocationDto { Market = marketHbar,  AmountQuote = 215m,   Amount = 3116.12541567m   },
          new AllocationDto { Market = marketSui,   AmountQuote = 199m,   Amount = 318.66372191m    },
          new AllocationDto { Market = marketNear,  AmountQuote = 197m,   Amount = 105.44541027m    },
          new AllocationDto { Market = marketFet,   AmountQuote = 0.000000001m, Amount = 0.000000008779m },
          new AllocationDto { Market = marketQuote, AmountQuote = 300m,   Amount = 300m             },
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert — correctly fires: CC (284) is fully sold while NEAR (197), SUI (199), HBAR (215),
    // GRAM (237), LINK (270) and ADA (275) are all smaller and kept.
    Assert.IsTrue(result);
  }

  // ── Combined quote + dust ──────────────────────────────────────────────────

  [TestMethod]
  public void HasNonContiguousFullSellOrder_QuoteAllocationInMiddle_WithDust_NonContiguousFullSell_ReturnsTrue()
  {
    // Arrange
    // EUR/EUR at 50 sits between ADA (15) and ETH (70). LTC (1) is dust. Both must be excluded
    // while gap detection still fires because ADA (15) is kept and ETH (70) is fully sold.
    var configReqDto = new ConfigReqDto { MinimumDiffQuote = 5 };

    var marketBtc = new MarketReqDto("EUR", "BTC");
    var marketEth = new MarketReqDto("EUR", "ETH");
    var marketAda = new MarketReqDto("EUR", "ADA");
    var marketBnb = new MarketReqDto("EUR", "BNB");
    var marketLtc = new MarketReqDto("EUR", "LTC");
    var marketQuote = new MarketReqDto("EUR", "EUR");

    var simulated = new SimulationDto
    {
      Orders =
      [
        new OrderDto { Market = marketEth, Side = OrderSide.Sell, Amount = 10 },
        new OrderDto { Market = marketLtc, Side = OrderSide.Sell, Amount = 5 }, // dust, also fully sold
      ],
      CurBalance = new BalanceDto
      {
        Allocations =
        [
          new AllocationDto { Market = marketBtc, AmountQuote = 100, Amount = 2 },
          new AllocationDto { Market = marketEth, AmountQuote = 70, Amount = 10 },
          new AllocationDto { Market = marketQuote, AmountQuote = 50, Amount = 50 }, // quote — excluded
          new AllocationDto { Market = marketAda, AmountQuote = 15, Amount = 100 },
          new AllocationDto { Market = marketBnb, AmountQuote = 10, Amount = 100 },
          new AllocationDto { Market = marketLtc, AmountQuote = 1, Amount = 5 },    // dust — excluded
        ]
      }
    };

    // Act
    var result = WorkerService.HasNonContiguousFullSellOrder(configReqDto, simulated);

    // Assert
    Assert.IsTrue(result);
  }

  #endregion
}