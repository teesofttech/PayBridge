using System.Text.Json.Serialization;

namespace PayBridge.SDK.Dtos.Request;
public class VerifyRequest
{
    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("tx_ref")]
    public string? RefAlias
    {
        set => Reference = value;
    }
}
