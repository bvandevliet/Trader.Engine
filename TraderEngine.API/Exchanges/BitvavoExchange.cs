using AutoMapper;
using Microsoft.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TraderEngine.API.DTOs.Bitvavo.Request;
using TraderEngine.API.DTOs.Bitvavo.Response;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Models;
using TraderEngine.Common.Results;

namespace TraderEngine.API.Exchanges;

public class BitvavoExchange : IExchange
{
  private readonly ILogger<BitvavoExchange> _logger;
  private readonly IMapper _mapper;
  private readonly HttpClient _httpClient;

  public ILogger<IExchange> Logger => _logger;

  public string QuoteSymbol { get; } = "EUR";
  public decimal MinOrderSizeInQuote { get; } = 5;
  public decimal MakerFee { get; } = .0015m;
  public decimal TakerFee { get; } = .0025m;
  public string ApiKey { get; set; } = string.Empty;
  public string ApiSecret { get; set; } = string.Empty;

  private readonly JsonSerializerOptions _jsonOptions = new()
  {
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
  };

  public BitvavoExchange(
    ILogger<BitvavoExchange> logger,
    IMapper mapper,
    HttpClient httpClient)
  {
    _logger = logger;
    _mapper = mapper;

    _httpClient = httpClient;
    _httpClient.BaseAddress = new("https://api.bitvavo.com/v2/");

    _jsonOptions.Converters
      .Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
  }

  private string CreateSignature(long timestamp, string method, string url, string? payload)
  {
    var hashString = new StringBuilder();

    _ = hashString.Append(timestamp).Append(method).Append(url);

    if (payload != null)
    {
      _ = hashString.Append(payload);
    }

    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ApiSecret));

    byte[] inputBytes = Encoding.UTF8.GetBytes(hashString.ToString());

    byte[] signatureBytes = hmac.ComputeHash(inputBytes);

    return BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();
  }

  private HttpRequestMessage CreateRequestMsg(HttpMethod method, string requestPath, object? body = null)
  {
    var request = new HttpRequestMessage(method, new Uri(_httpClient.BaseAddress!, requestPath));

    string? payload = null;

    if (null != body)
    {
      payload = JsonSerializer.Serialize(body, body.GetType(), _jsonOptions);

      request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
    }

    request.Headers.Add(HeaderNames.Accept, "application/json");
    request.Headers.Add("bitvavo-access-window", "60000 ");

    long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    string signature = CreateSignature(timestamp, request.Method.ToString(), request.RequestUri!.PathAndQuery, payload);

    request.Headers.Add("bitvavo-access-key", ApiKey);
    request.Headers.Add("bitvavo-access-timestamp", timestamp.ToString());
    request.Headers.Add("bitvavo-access-signature", signature);

    return request;
  }

  public async Task<Result<Balance, ExchangeErrCodeEnum>> GetBalance()
  {
    using var request = CreateRequestMsg(HttpMethod.Get, "balance");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      try
      {
        var error = await response.Content.ReadFromJsonAsync<JsonObject>();

        string? errorCode = error?["errorCode"]?.ToString();

        if (errorCode == "105" || errorCode?.StartsWith('3') is true)
        {
          _logger.LogError("Failed to get balance from Bitvavo. {url} returned {code} {reason} with response: {response}",
            request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

          return Result<Balance, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.AuthenticationError);
        }
      }
      catch (Exception)
      {
      }

      _logger.LogCritical("Failed to get balance from Bitvavo. {url} returned {code} {reason} with response: {response}",
          request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return Result<Balance, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.Other);
    }

    List<BitvavoAllocationDto>? result;
    try
    {
      result = await response.Content.ReadFromJsonAsync<List<BitvavoAllocationDto>>();

      if (null == result)
        throw new Exception("Bitvavo get balance response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo get balance response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    var balance = new Balance(QuoteSymbol);

    var allocations = await Task.WhenAll(
      result

      // Get amount of each asset.
      .Select(allocationDto => new
      {
        AllocDto = allocationDto,
        AmountQuote = decimal.Parse(allocationDto.Available) + decimal.Parse(allocationDto.InOrder)
      })

      // Filter out assets of which the amount is 0.
      .Where(alloc => alloc.AmountQuote > 0)

      // Get price of each asset.
      .Select(async alloc =>
      {
        var market = new MarketReqDto(QuoteSymbol, alloc.AllocDto.Symbol);

        decimal price = market.BaseSymbol.Equals(QuoteSymbol) ? 1 : await GetPrice(market);

        var allocation = new Allocation(market, price, alloc.AmountQuote);

        return allocation;
      }));

    foreach (var allocation in allocations)
    {
      _ = balance.TryAddAllocation(allocation);
    }

    // Add quote allocation if not present.
    _ = balance.TryAddAllocation(new(QuoteSymbol, QuoteSymbol, 1));

    return Result<Balance, ExchangeErrCodeEnum>.Success(balance);
  }

  public async Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited()
  {
    using var request = CreateRequestMsg(
      HttpMethod.Get, $"depositHistory?symbol={QuoteSymbol}&start=0");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      try
      {
        var error = await response.Content.ReadFromJsonAsync<JsonObject>();

        string? errorCode = error?["errorCode"]?.ToString();

        if (errorCode == "105" || errorCode?.StartsWith('3') is true)
        {
          _logger.LogError("Failed to get total deposited from Bitvavo. {url} returned {code} {reason} with response: {response}",
            request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

          return Result<decimal, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.AuthenticationError);
        }
      }
      catch (Exception)
      {
      }

      _logger.LogCritical("Failed to get total deposited from Bitvavo. {url} returned {code} {reason} with response: {response}",
          request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return Result<decimal, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.Other);
    }

    JsonArray? result;
    try
    {
      result = await response.Content.ReadFromJsonAsync<JsonArray>();

      if (null == result)
        throw new Exception("Bitvavo deposit response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo deposit response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return Result<decimal, ExchangeErrCodeEnum>.Success(
      result.Sum(obj => decimal.Parse(obj!["amount"]!.ToString())));
  }

  public async Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn()
  {
    using var request = CreateRequestMsg(
      HttpMethod.Get, $"withdrawalHistory?symbol={QuoteSymbol}&start=0");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      try
      {
        var error = await response.Content.ReadFromJsonAsync<JsonObject>();

        string? errorCode = error?["errorCode"]?.ToString();

        if (errorCode == "105" || errorCode?.StartsWith('3') is true)
        {
          _logger.LogError("Failed to get total withdrawn from Bitvavo. {url} returned {code} {reason} with response: {response}",
            request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

          return Result<decimal, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.AuthenticationError);
        }
      }
      catch (Exception)
      {
      }

      _logger.LogCritical("Failed to get total withdrawn from Bitvavo. {url} returned {code} {reason} with response: {response}",
          request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return Result<decimal, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.Other);
    }

    JsonArray? result;
    try
    {
      result = await response.Content.ReadFromJsonAsync<JsonArray>();

      if (null == result)
        throw new Exception("Bitvavo withdrawal response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo withdrawal response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return Result<decimal, ExchangeErrCodeEnum>.Success(
      result.Sum(obj => decimal.Parse(obj!["amount"]!.ToString())));
  }

  public async Task<MarketDataDto?> GetMarket(MarketReqDto market)
  {
    using var request = CreateRequestMsg(
      HttpMethod.Get, $"markets?market={market}");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      try
      {
        var error = await response.Content.ReadFromJsonAsync<JsonObject>();

        if (error?["errorCode"]?.ToString() == "205")
        {
          return new MarketDataDto()
          {
            Status = MarketStatus.Unavailable,
          };
        }
      }
      catch (Exception)
      {
      }

      _logger.LogError("Failed to get market from Bitvavo. {url} returned {code} {reason} with response: {response}",
        request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return null;
    }

    BitvavoMarketDataDto? result;
    try
    {
      result = await response.Content.ReadFromJsonAsync<BitvavoMarketDataDto>();

      if (null == result)
        throw new Exception("Bitvavo get market response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo get market response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return _mapper.Map<MarketDataDto>(result);
  }

  public async Task<AssetDataDto?> GetAsset(string baseSymbol)
  {
    using var request = CreateRequestMsg(
      HttpMethod.Get, $"assets?symbol={baseSymbol}");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      _logger.LogError("Failed to get asset from Bitvavo. {url} returned {code} {reason} with response: {response}",
        request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return null;
    }

    BitvavoAssetDataDto? result;
    try
    {
      result = await response.Content.ReadFromJsonAsync<BitvavoAssetDataDto>();

      if (null == result)
        throw new Exception("Bitvavo asset response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo asset response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return _mapper.Map<AssetDataDto>(result);
  }

  public async Task<decimal> GetPrice(MarketReqDto market)
  {
    using var request = CreateRequestMsg(
      HttpMethod.Get, $"ticker/price?market={market}");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      _logger.LogCritical("Failed to get price from Bitvavo. {url} returned {code} {reason} with response: {response}",
        request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      throw new Exception("Error while requesting price.");
    }

    BitvavoTickerPriceDto? result;
    try
    {
      result = await response.Content.ReadFromJsonAsync<BitvavoTickerPriceDto>();

      if (null == result)
        throw new Exception("Bitvavo ticker price response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo ticker price response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return decimal.Parse(result.Price);
  }

  public async Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(OrderReqDto order, string source = "API")
  {
    var newOrderDto = _mapper.Map<BitvavoOrderReqDto>(order);

    newOrderDto.DisableMarketProtection = true;
    newOrderDto.ResponseRequired = false;
    newOrderDto.OperatorId = $"trader.{source.ToLower()}".GetHashCode();

    var failedOrder = new OrderDto()
    {
      Market = order.Market,
      Side = order.Side,
      Type = order.Type,
      Price = order.Price ?? default,
      Amount = order.Amount ?? default,
      AmountQuote = order.AmountQuote ?? default,
      Status = OrderStatus.Failed,
      AmountRemaining = order.Amount ?? default,
      AmountQuoteRemaining = order.AmountQuote ?? default,
    };

    try
    {
      using var request = CreateRequestMsg(HttpMethod.Post, "order", newOrderDto);

      using var response = await _httpClient.SendAsync(request);

      if (!response.IsSuccessStatusCode)
      {
        try
        {
          var error = await response.Content.ReadFromJsonAsync<JsonObject>();

          string? errorCode = error?["errorCode"]?.ToString();

          _logger.LogError("Failed to create new order on Bitvavo. {url} returned {code} {reason} with response: {response}\nRequest payload was {payload}",
              request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync(), await request.Content!.ReadAsStringAsync());

          return errorCode == "105" || errorCode?.StartsWith('3') is true
            ? Result<OrderDto, ExchangeErrCodeEnum>.Failure(failedOrder, ExchangeErrCodeEnum.AuthenticationError)
            : Result<OrderDto, ExchangeErrCodeEnum>.Failure(failedOrder, ExchangeErrCodeEnum.Other);
        }
        catch (Exception)
        {
        }

        _logger.LogCritical("Failed to create new order on Bitvavo. {url} returned {code} {reason} with response: {response}\nRequest payload was {payload}",
            request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync(), await request.Content!.ReadAsStringAsync());

        return Result<OrderDto, ExchangeErrCodeEnum>.Failure(failedOrder, ExchangeErrCodeEnum.Other);
      }

      BitvavoOrderDto? result;
      try
      {
        result = await response.Content.ReadFromJsonAsync<BitvavoOrderDto>();

        if (null == result)
          throw new Exception("Bitvavo new order response was empty or null.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to deserialize Bitvavo new order response: {Content}", await response.Content.ReadAsStringAsync());
        throw;
      }

      var executedOrder = _mapper.Map<OrderDto>(result);

      return Result<OrderDto, ExchangeErrCodeEnum>.Success(executedOrder);
    }
    catch (Exception ex)
    {
      _logger.LogCritical(ex, "Failed to place order.");

      return Result<OrderDto, ExchangeErrCodeEnum>.Failure(failedOrder, ExchangeErrCodeEnum.Exception);
    }
  }

  public async Task<OrderDto?> GetOrder(string orderId, MarketReqDto market)
  {
    using var request = CreateRequestMsg(
      HttpMethod.Get, $"order?orderId={orderId}&market={market}");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      _logger.LogError("Failed to get order from Bitvavo. {url} returned {code} {reason} with response: {response}",
        request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return null;
    }

    BitvavoOrderDto? result;
    try
    {
      result = await response.Content.ReadFromJsonAsync<BitvavoOrderDto>();

      if (null == result)
        throw new Exception("Bitvavo get order response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo get order response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return _mapper.Map<OrderDto>(result);
  }

  public Task<OrderDto?> CancelOrder(string orderId, MarketReqDto market, string source = "API")
  {
    throw new NotImplementedException();
  }

  public Task<IEnumerable<OrderDto>?> GetOpenOrders(MarketReqDto? market = null)
  {
    throw new NotImplementedException();
  }

  public async Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(MarketReqDto? market = null, string source = "API")
  {
    using var request = CreateRequestMsg(HttpMethod.Delete, $"orders?operatorId={$"trader.{source.ToLower()}".GetHashCode()}");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      _logger.LogError("Failed to cancel all open orders on Bitvavo. {url} returned {code} {reason} with response: {response}",
        request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return null;
    }

    List<BitvavoOrderDto>? result;
    try
    {
      result = await response.Content.ReadFromJsonAsync<List<BitvavoOrderDto>>();

      if (null == result)
        throw new Exception("Bitvavo cancel all open orders response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo cancel all open orders response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return _mapper.Map<IEnumerable<OrderDto>>(result);
  }

  public Task<Result<IEnumerable<OrderDto>?, ExchangeErrCodeEnum>> SellAllPositions(string? asset = null, string source = "API")
  {
    throw new NotImplementedException();
  }
}