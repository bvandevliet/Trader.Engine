namespace TraderEngine.Common.Results;

public class Result<TErrCode> where TErrCode : Enum
{
  public TErrCode ErrorCode { get; }

  public string ErrorMessage { get; }

  public Result(TErrCode errorCode, string? errorMessage = null)
  {
    ErrorCode = errorCode;
    ErrorMessage = errorMessage ?? string.Empty;
  }

  public static Result<TErrCode> Success() => new(default!);

  public static Result<TErrCode> Failure(TErrCode errorCode, string errorMessage = "") => new(errorCode, errorMessage);
}

public class Result<TSuccess, TErrCode> : Result<TErrCode> where TErrCode : Enum
{
  public TSuccess? Value { get; }

  public Result(TSuccess? value, TErrCode errorCode, string? errorMessage = null) : base(errorCode, errorMessage)
  {
    Value = value;
  }

  public static Result<TSuccess, TErrCode> Success(TSuccess value) => new(value, default!);

  public new static Result<TSuccess, TErrCode> Failure(TErrCode errorCode, string errorMessage = "") => new(default, errorCode, errorMessage);
}
