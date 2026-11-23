using System.Text.Json;
using System.Text.Json.Serialization;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Shared.Api;

/// <summary>Source-generated JSON metadata of the contracts in the shared layer (trimming-safe).</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ProblemDetailsDto))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(RegisterResponse))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(UserDto))]
internal sealed partial class SharedJsonContext : JsonSerializerContext;
