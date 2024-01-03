using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class BalancedReqDto
{
  [Required]
  public string QuoteSymbol { get; set; } = null!;

  [Required]
  public decimal AmountQuoteTotal { get; set; }

  [Required]
  public ConfigReqDto Config { get; set; } = null!;

  [Required]
  public ApiCredReqDto ExchangeApiCred { get; set; } = null!;
}