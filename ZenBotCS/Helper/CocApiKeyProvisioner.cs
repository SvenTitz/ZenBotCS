using System.Net;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace ZenBotCS.Helper;

/// <summary>
/// Dev-machine convenience: the CoC developer API locks each key to a CIDR range.
/// On a connection without a static IP, the configured CocApiToken breaks whenever
/// the IP rotates. When CocApiAutoProvisionKey is set, this logs into
/// developer.clashofclans.com with CocApiEmail/CocApiPassword and replaces the key
/// named CocApiKeyName with one scoped to the current public IP.
/// </summary>
public static class CocApiKeyProvisioner
{
    private const string BaseUrl = "https://developer.clashofclans.com/api";

    public static async Task<string> EnsureKeyAsync(IConfiguration configuration)
    {
        var configuredToken = configuration["CocApiToken"];

        if (!configuration.GetValue<bool>("CocApiAutoProvisionKey"))
            return configuredToken!;

        var email = configuration["CocApiEmail"];
        var password = configuration["CocApiPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("[CocApiKeyProvisioner] CocApiAutoProvisionKey is set but CocApiEmail/CocApiPassword are missing. Falling back to the configured CocApiToken.");
            return configuredToken!;
        }

        var keyName = configuration["CocApiKeyName"] ?? $"ZenBotCS-{Environment.MachineName}";

        try
        {
            return await ProvisionKeyAsync(email, password, keyName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CocApiKeyProvisioner] Failed to auto-provision a CoC API key, falling back to the configured CocApiToken. {ex.Message}");
            return configuredToken!;
        }
    }

    private static async Task<string> ProvisionKeyAsync(string email, string password, string keyName)
    {
        var currentIp = await GetCurrentIpAsync();

        var options = new RestClientOptions(BaseUrl) { CookieContainer = new CookieContainer() };
        using var client = new RestClient(options);

        var loginRequest = new RestRequest("login", Method.Post).AddJsonBody(new { email, password });
        var loginResponse = await client.ExecuteAsync(loginRequest);
        if (!loginResponse.IsSuccessful)
            throw new InvalidOperationException($"login failed: {loginResponse.StatusCode} {loginResponse.Content}");

        var listRequest = new RestRequest("apikey/list", Method.Post).AddJsonBody(new { });
        var listResponse = await client.ExecuteAsync(listRequest);
        if (!listResponse.IsSuccessful)
            throw new InvalidOperationException($"apikey/list failed: {listResponse.StatusCode} {listResponse.Content}");

        var keys = (JArray?)JObject.Parse(listResponse.Content!)["keys"] ?? [];
        var existing = keys.FirstOrDefault(k => (string?)k["name"] == keyName);

        if (existing is not null)
        {
            var existingCidrs = existing["cidrRanges"]?.Values<string>().ToList() ?? [];
            if (existingCidrs.Contains(currentIp))
            {
                Console.WriteLine($"[CocApiKeyProvisioner] Existing key '{keyName}' is already scoped to {currentIp}.");
                return (string)existing["key"]!;
            }

            var revokeRequest = new RestRequest("apikey/revoke", Method.Post).AddJsonBody(new { id = (string)existing["id"]! });
            var revokeResponse = await client.ExecuteAsync(revokeRequest);
            if (!revokeResponse.IsSuccessful)
                throw new InvalidOperationException($"apikey/revoke failed: {revokeResponse.StatusCode} {revokeResponse.Content}");
        }
        else if (keys.Count >= 10)
        {
            throw new InvalidOperationException(
                $"CoC developer account already has {keys.Count} API keys and none named '{keyName}' to replace. " +
                "Revoke one manually at developer.clashofclans.com or change CocApiKeyName.");
        }

        var createRequest = new RestRequest("apikey/create", Method.Post).AddJsonBody(new
        {
            name = keyName,
            description = "Auto-provisioned by ZenBotCS for the current machine IP.",
            cidrRanges = new[] { currentIp },
            scopes = new[] { "clash" }
        });
        var createResponse = await client.ExecuteAsync(createRequest);
        if (!createResponse.IsSuccessful)
            throw new InvalidOperationException($"apikey/create failed: {createResponse.StatusCode} {createResponse.Content}");

        var newKey = (string)JObject.Parse(createResponse.Content!)["key"]!["key"]!;
        Console.WriteLine($"[CocApiKeyProvisioner] Provisioned CoC API key '{keyName}' for IP {currentIp}.");
        return newKey;
    }

    private static async Task<string> GetCurrentIpAsync()
    {
        using var client = new RestClient("https://api.ipify.org");
        var request = new RestRequest("").AddQueryParameter("format", "text");
        var response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
            throw new InvalidOperationException("Could not determine current public IP address.");
        return response.Content.Trim();
    }
}
