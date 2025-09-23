using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace GiselX.Service.Dto.Common;

public class ServiceResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
    public List<string> Errors { get; init; } = [];
    public bool IsSuccess => Errors.Count == 0;
    public int StatusCode { get; init; } = StatusCodes.Status200OK;
}

public class ServiceResponse<T> : ServiceResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }
}