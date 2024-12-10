namespace TraderEngine.Common.Results;

public class Result<TSuccess>(TSuccess? value, string[]? messages)
{
  public TSuccess? Value { get; } = value;

  public string[] Messages { get; } = messages ?? [];

  public string Summary => string.Join("; ", Messages);

  public static Result<TSuccess> Success(TSuccess value, string[]? messages = null) => new(value, messages);

  public static Result<TSuccess> Failure(string[]? messages = null) => new(default, messages);
}

public class Result<TSuccess, TErrCode>(TSuccess? value, TErrCode errorCode, string[]? messages) : Result<TSuccess>(value, messages) where TErrCode : Enum
{
  public TErrCode ErrorCode { get; } = errorCode;

  public new static Result<TSuccess, TErrCode> Success(TSuccess value, string[]? messages = null) => new(value, default!, messages);

  public static Result<TSuccess, TErrCode> Failure(TErrCode errorCode, string[]? messages = null) => new(default, errorCode, messages);
}
