using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexAlertRelay;

public sealed class FcmSender
{
    private static readonly HttpClient Http = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;

    public async Task SendAsync(PcConfig config, ToastPayload payload, CancellationToken cancellationToken)
    {
        ValidateConfig(config);
        var token = await GetAccessTokenAsync(config.Firebase.ServiceAccountPath, cancellationToken);
        var targetTokens = config.Firebase.GetTargetTokens();

        foreach (var targetToken in targetTokens)
        {
            await SendToTokenAsync(config, payload, targetToken, token, cancellationToken);
        }
    }

    private static async Task SendToTokenAsync(
        PcConfig config,
        ToastPayload payload,
        string targetToken,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            message = new
            {
                token = targetToken,
                android = new
                {
                    priority = "HIGH"
                },
                data = new Dictionary<string, string>
                {
                    ["type"] = "codex_notification",
                    ["version"] = "1",
                    ["pcId"] = payload.PcId,
                    ["pcName"] = payload.PcName,
                    ["sourceAppId"] = payload.SourceAppId,
                    ["sourceAppName"] = payload.SourceAppName,
                    ["toastId"] = payload.ToastId,
                    ["title"] = payload.Title,
                    ["body"] = payload.Body,
                    ["receivedAtUtc"] = payload.ReceivedAtUtc.UtcDateTime.ToString("O")
                }
            }
        };

        var uri = $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(config.Firebase.ProjectId)}/messages:send";
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"FCM send failed: {(int)response.StatusCode} {response.ReasonPhrase} {responseBody}");
        }
    }

    private async Task<string> GetAccessTokenAsync(string serviceAccountPath, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && DateTimeOffset.UtcNow < _accessTokenExpiresAt.AddMinutes(-5))
        {
            return _accessToken;
        }

        var serviceAccount = JsonSerializer.Deserialize<ServiceAccount>(
            await File.ReadAllTextAsync(serviceAccountPath, cancellationToken),
            JsonOptions) ?? throw new InvalidOperationException("Invalid service account JSON.");

        var tokenUri = string.IsNullOrWhiteSpace(serviceAccount.TokenUri)
            ? "https://oauth2.googleapis.com/token"
            : serviceAccount.TokenUri;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claimSet = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["iss"] = serviceAccount.ClientEmail,
            ["scope"] = "https://www.googleapis.com/auth/firebase.messaging",
            ["aud"] = tokenUri,
            ["iat"] = now,
            ["exp"] = now + 3600
        }));
        var signingInput = $"{header}.{claimSet}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(serviceAccount.PrivateKey);
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var assertion = $"{signingInput}.{Base64Url(signature)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUri);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion
        });

        var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OAuth token request failed: {(int)response.StatusCode} {response.ReasonPhrase} {body}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Could not parse OAuth token response.");
        _accessToken = tokenResponse.AccessToken;
        _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, tokenResponse.ExpiresIn));
        return _accessToken;
    }

    private static void ValidateConfig(PcConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Firebase.ProjectId))
        {
            throw new InvalidOperationException("firebase.projectId is required.");
        }
        if (config.Firebase.GetTargetTokens().Count == 0)
        {
            throw new InvalidOperationException("firebase.targetToken or firebase.targetTokens is required.");
        }
        if (string.IsNullOrWhiteSpace(config.Firebase.ServiceAccountPath) || !File.Exists(config.Firebase.ServiceAccountPath))
        {
            throw new InvalidOperationException($"Firebase service account JSON not found: {config.Firebase.ServiceAccountPath}");
        }
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed class ServiceAccount
    {
        [JsonPropertyName("client_email")]
        public string ClientEmail { get; set; } = "";

        [JsonPropertyName("private_key")]
        public string PrivateKey { get; set; } = "";

        [JsonPropertyName("token_uri")]
        public string TokenUri { get; set; } = "";
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
