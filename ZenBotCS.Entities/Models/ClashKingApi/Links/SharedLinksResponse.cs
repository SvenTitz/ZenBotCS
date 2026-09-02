using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.Links;

/// <summary>
/// <c>POST /v2/links/shared</c>. Only visible links are listed -- a requested tag or user that is
/// unlinked is simply absent, not present with a null value.
/// </summary>
public class SharedLinksResponse
{
    [JsonProperty("items")]
    public List<SharedLink> Items { get; set; } = [];
}
