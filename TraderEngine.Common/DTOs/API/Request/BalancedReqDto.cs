using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class BalanceReqDto
{
  public string? QuoteSymbol { get; set; }

  public decimal? AmountQuoteTotal { get; set; }

  [Required]
  public ConfigReqDto Config { get; set; } = null!;

  [Required]
  public ApiCredReqDto ExchangeApiCred { get; set; } = null!;
}