using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Net.Http.Headers;
using TraderEngine.API.DTOs.Bitvavo.Response;
using TraderEngine.API.Mappers;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Models;
using TraderEngine.Common.Results;

namespace TraderEngine.API.Exchanges;

public class BitvavoExchange : IExchange, IExchangeOrderNotifications
{
  private readonly ILogger<BitvavoExchange> _logger;
  private readonly HttpClient _httpClient;
  private readonly BitvavoWebSocketConnectionPool _wsPool;

  public ILogger<IExchange> Logger => _logger;

  public string QuoteSymbol { get; } = "EUR";
  public decimal MinOrderSizeInQuote { get; } = 5;
  public decimal MakerFee { get; } = .0015m;
  public decimal TakerFee { get; } = .0025m;

  public BitvavoExchange(
    ILogger<BitvavoExchange> logger,
    HttpClient httpClient,
    BitvavoWebSocketConnectionPool wsPool)
  {
    _logger = logger;
    _wsPool = wsPool;

    _httpClient = httpClient;
    _httpClient.BaseAddress = new("https://api.bitvavo.com/v2/");
  }

  private static string CreateSignature(ExchangeCredentials credentials, long timestamp, string method, string url, string? payload)
  {
    return BitvavoSignature.Compute(credentials.ApiSecret, timestamp, method, url, payload);
  }

  private HttpRequestMessage CreateRequestMsg(ExchangeCredentials credentials, HttpMethod method, string requestPath, object? body = null)
  {
    var request = new HttpRequestMessage(method, new Uri(_httpClient.BaseAddress!, requestPath));

    string? payload = null;

    if (null != body)
    {
      payload = AppJsonSerializer.Serialize(body, body.GetType());

      request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
    }

    request.Headers.Add(HeaderNames.Accept, "application/json");
    request.Headers.Add("bitvavo-access-window", BitvavoDefaults.AccessWindowMs.ToString());

    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    var signature = CreateSignature(credentials, timestamp, request.Method.ToString(), request.RequestUri!.PathAndQuery, payload);

    request.Headers.Add("bitvavo-access-key", credentials.ApiKey);
    request.Headers.Add("bitvavo-access-timestamp", timestamp.ToString());
    request.Headers.Add("bitvavo-access-signature", signature);

    return request;
  }

  public async Task<Result<Balance, ExchangeErrCodeEnum>> GetBalance(ExchangeCredentials credentials)
  {
    using var request = CreateRequestMsg(credentials, HttpMethod.Get, "balance");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      try
      {
        var error = await response.Content.DeserializeAsync<JsonObject>();

        var errorCode = error?["errorCode"]?.ToString();

        if ((int)response.StatusCode is 401 or 403 || errorCode == "105" || errorCode?.StartsWith('3') is true)
        {
          return Result<Balance, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.AuthenticationError);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to deserialize Bitvavo get balance error response: {Content}", await response.Content.ReadAsStringAsync());
      }

      _logger.LogCritical("Failed to get balance from Bitvavo. {url} returned {code} {reason} with response: {response}",
          request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return Result<Balance, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.Other);
    }

    List<BitvavoAllocationDto>? result;
    try
    {
      result = await response.Content.DeserializeAsync<List<BitvavoAllocationDto>>();

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

        var price = market.BaseSymbol.Equals(QuoteSymbol) ? 1 : await GetPrice(credentials, market);

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

  public async Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited(ExchangeCredentials credentials)
  {
    using var request = CreateRequestMsg(
      credentials, HttpMethod.Get, $"depositHistory?symbol={QuoteSymbol}&start=0");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      try
      {
        var error = await response.Content.DeserializeAsync<JsonObject>();

        var errorCode = error?["errorCode"]?.ToString();

        if ((int)response.StatusCode is 401 or 403 || errorCode == "105" || errorCode?.StartsWith('3') is true)
        {
          return Result<decimal, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.AuthenticationError);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to deserialize Bitvavo get total deposited error response: {Content}", await response.Content.ReadAsStringAsync());
      }

      _logger.LogCritical("Failed to get total deposited from Bitvavo. {url} returned {code} {reason} with response: {response}",
          request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return Result<decimal, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.Other);
    }

    JsonArray? result;
    try
    {
      result = await response.Content.DeserializeAsync<JsonArray>();

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

  public async Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn(ExchangeCredentials credentials)
  {
    using var request = CreateRequestMsg(
      credentials, HttpMethod.Get, $"withdrawalHistory?symbol={QuoteSymbol}&start=0");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      try
      {
        var error = await response.Content.DeserializeAsync<JsonObject>();

        var errorCode = error?["errorCode"]?.ToString();

        if ((int)response.StatusCode is 401 or 403 || errorCode == "105" || errorCode?.StartsWith('3') is true)
        {
          return Result<decimal, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.AuthenticationError);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to deserialize Bitvavo get total withdrawn error response: {Content}", await response.Content.ReadAsStringAsync());
      }

      _logger.LogCritical("Failed to get total withdrawn from Bitvavo. {url} returned {code} {reason} with response: {response}",
          request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return Result<decimal, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.Other);
    }

    JsonArray? result;
    try
    {
      result = await response.Content.DeserializeAsync<JsonArray>();

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

  public async Task<MarketDataDto?> GetMarket(ExchangeCredentials credentials, MarketReqDto market)
  {
    using var request = CreateRequestMsg(
      credentials, HttpMethod.Get, $"markets?market={market}");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      try
      {
        var error = await response.Content.DeserializeAsync<JsonObject>();

        if (error?["errorCode"]?.ToString() == "205")
        {
          return new MarketDataDto()
          {
            Status = MarketStatus.Unavailable,
          };
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to deserialize Bitvavo get market error response: {Content}", await response.Content.ReadAsStringAsync());
      }

      _logger.LogError("Failed to get market from Bitvavo. {url} returned {code} {reason} with response: {response}",
        request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return null;
    }

    BitvavoMarketDataDto? result;
    try
    {
      result = await response.Content.DeserializeAsync<BitvavoMarketDataDto>();

      if (null == result)
        throw new Exception("Bitvavo get market response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo get market response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return ApiMapper.MapMarketData(result);
  }

  public async Task<AssetDataDto?> GetAsset(ExchangeCredentials credentials, string baseSymbol)
  {
    using var request = CreateRequestMsg(
      credentials, HttpMethod.Get, $"assets?symbol={baseSymbol}");

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
      result = await response.Content.DeserializeAsync<BitvavoAssetDataDto>();

      if (null == result)
        throw new Exception("Bitvavo asset response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo asset response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return ApiMapper.MapAssetData(result);
  }

  public async Task<decimal> GetPrice(ExchangeCredentials credentials, MarketReqDto market)
  {
    using var request = CreateRequestMsg(
      credentials, HttpMethod.Get, $"ticker/price?market={market}");

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
      result = await response.Content.DeserializeAsync<BitvavoTickerPriceDto>();

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

  public async Task<BestBidAskDto?> GetBestBidAsk(ExchangeCredentials credentials, MarketReqDto market)
  {
    using var request = CreateRequestMsg(
      credentials, HttpMethod.Get, $"ticker/book?market={market}");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      _logger.LogError("Failed to get ticker book from Bitvavo. {url} returned {code} {reason} with response: {response}",
        request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return null;
    }

    BitvavoTickerBookDto? result;
    try
    {
      result = await response.Content.DeserializeAsync<BitvavoTickerBookDto>();

      if (null == result)
        throw new Exception("Bitvavo ticker book response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo ticker book response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return ApiMapper.MapTickerBook(result);
  }

  public async Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(ExchangeCredentials credentials, OrderReqDto order, string source = "API")
  {
    var newOrderDto = ApiMapper.MapOrderReq(order);

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
      using var request = CreateRequestMsg(credentials, HttpMethod.Post, "order", newOrderDto);

      using var response = await _httpClient.SendAsync(request);

      if (!response.IsSuccessStatusCode)
      {
        try
        {
          var error = await response.Content.DeserializeAsync<JsonObject>();

          var errorCode = error?["errorCode"]?.ToString();

          if ((int)response.StatusCode is 401 or 403 || errorCode == "105" || errorCode?.StartsWith('3') is true)
          {
            return Result<OrderDto, ExchangeErrCodeEnum>.Failure(failedOrder, ExchangeErrCodeEnum.AuthenticationError);
          }
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to deserialize Bitvavo new order error response: {Content}", await response.Content.ReadAsStringAsync());
        }

        _logger.LogCritical("Failed to create new order on Bitvavo. {url} returned {code} {reason} with response: {response}\nRequest payload was {payload}",
            request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync(), await request.Content!.ReadAsStringAsync());

        return Result<OrderDto, ExchangeErrCodeEnum>.Failure(failedOrder, ExchangeErrCodeEnum.Other);
      }

      BitvavoOrderDto? result;
      try
      {
        result = await response.Content.DeserializeAsync<BitvavoOrderDto>();

        if (null == result)
          throw new Exception("Bitvavo new order response was empty or null.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to deserialize Bitvavo new order response: {Content}", await response.Content.ReadAsStringAsync());
        throw;
      }

      var executedOrder = ApiMapper.MapOrder(result);

      return Result<OrderDto, ExchangeErrCodeEnum>.Success(executedOrder);
    }
    catch (Exception ex)
    {
      _logger.LogCritical(ex, "Failed to place order.");

      return Result<OrderDto, ExchangeErrCodeEnum>.Failure(failedOrder, ExchangeErrCodeEnum.Exception);
    }
  }

  public async Task<OrderDto?> GetOrder(ExchangeCredentials credentials, string orderId, MarketReqDto market)
  {
    using var request = CreateRequestMsg(
      credentials, HttpMethod.Get, $"order?orderId={orderId}&market={market}");

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
      result = await response.Content.DeserializeAsync<BitvavoOrderDto>();

      if (null == result)
        throw new Exception("Bitvavo get order response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo get order response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return ApiMapper.MapOrder(result);
  }

  public async Task<OrderDto?> CancelOrder(ExchangeCredentials credentials, string orderId, MarketReqDto market, string source = "API")
  {
    using var request = CreateRequestMsg(
      credentials, HttpMethod.Delete, $"order?orderId={orderId}&market={market}");

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      _logger.LogError("Failed to cancel order on Bitvavo. {url} returned {code} {reason} with response: {response}",
        request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return null;
    }

    BitvavoOrderDto? result;
    try
    {
      result = await response.Content.DeserializeAsync<BitvavoOrderDto>();

      if (null == result)
        throw new Exception("Bitvavo cancel order response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo cancel order response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return ApiMapper.MapOrder(result);
  }

  public async Task<IEnumerable<OrderDto>?> GetOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null)
  {
    var requestPath = market is null ? "ordersOpen" : $"ordersOpen?market={market}";

    using var request = CreateRequestMsg(credentials, HttpMethod.Get, requestPath);

    using var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      _logger.LogError("Failed to get open orders from Bitvavo. {url} returned {code} {reason} with response: {response}",
        request.RequestUri, (int)response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync());

      return null;
    }

    List<BitvavoOrderDto>? result;
    try
    {
      result = await response.Content.DeserializeAsync<List<BitvavoOrderDto>>();

      if (null == result)
        throw new Exception("Bitvavo get open orders response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo get open orders response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return ApiMapper.MapOrders(result);
  }

  public async Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null, string source = "API")
  {
    using var request = CreateRequestMsg(credentials, HttpMethod.Delete, $"orders?operatorId={$"trader.{source.ToLower()}".GetHashCode()}");

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
      result = await response.Content.DeserializeAsync<List<BitvavoOrderDto>>();

      if (null == result)
        throw new Exception("Bitvavo cancel all open orders response was empty or null.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to deserialize Bitvavo cancel all open orders response: {Content}", await response.Content.ReadAsStringAsync());
      throw;
    }

    return ApiMapper.MapOrders(result);
  }

  public Task<Result<IEnumerable<OrderDto>?, ExchangeErrCodeEnum>> SellAllPositions(ExchangeCredentials credentials, string? asset = null, string source = "API")
  {
    throw new NotImplementedException();
  }

  public async Task<IAsyncDisposable> BeginOrderNotificationSessionAsync(ExchangeCredentials credentials, CancellationToken ct = default)
  {
    try
    {
      return await _wsPool.AcquireSessionAsync(credentials, ct);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to begin Bitvavo WebSocket session; orders in this run will fall back to REST polling.");

      return NoOpAsyncDisposable.Instance;
    }
  }

  public async Task<OrderDto?> WaitForOrderEndedAsync(ExchangeCredentials credentials, OrderDto order, TimeSpan timeout, CancellationToken ct = default)
  {
    if (order.Id is not string orderId)
      return order;

    var market = order.Market.ToString();

    BitvavoWebSocketClient client;

    try
    {
      client = await _wsPool.GetConnectedAsync(credentials, ct);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to establish Bitvavo WebSocket connection for order {OrderId}; falling back to REST polling.", orderId);
      return null;
    }

    var tcs = new TaskCompletionSource<OrderDto>(TaskCreationOptions.RunContinuationsAsynchronously);

    void OnAccountEvent(JsonElement element)
    {
      if (!element.TryGetProperty("orderId", out var idProp) || idProp.GetString() != orderId)
        return;

      BitvavoOrderDto? dto;

      try
      {
        dto = element.Deserialize<BitvavoOrderDto>(AppJsonSerializer.Options);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to deserialize Bitvavo WebSocket order push for order {OrderId}.", orderId);
        return;
      }

      if (dto is null)
        return;

      var mapped = ApiMapper.MapOrder(dto);

      if (mapped.HasEnded)
        tcs.TrySetResult(mapped);
    }

    try
    {
      await client.SubscribeAccountAsync(market, OnAccountEvent, ct);

      using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      timeoutCts.CancelAfter(timeout);

      return await tcs.Task.WaitAsync(timeoutCts.Token);
    }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
    {
      // Timed out waiting for a push update — not an error, the caller falls back to a REST check.
      return null;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Bitvavo WebSocket subscription failed for order {OrderId}; falling back to REST polling.", orderId);
      return null;
    }
    finally
    {
      client.UnsubscribeAccount(market);
    }
  }
}
