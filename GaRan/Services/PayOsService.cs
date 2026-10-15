using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GaRan.Models;

namespace GaRan.Services;

public interface IPayOsService
{
    bool IsConfigured { get; }
    Task<PayOsCheckoutResult> CreatePaymentLinkAsync(DonHang order, ThanhToan payment, int payOsOrderCode, string returnUrl, string cancelUrl, CancellationToken cancellationToken = default);
    Task<string> GetPaymentLinkStatusAsync(string paymentLinkId, CancellationToken cancellationToken = default);
    bool VerifyWebhook(JsonElement data, string signature);
}

public sealed class PayOsService : IPayOsService
{
    private const string ApiBaseUrl = "https://api-merchant.payos.vn";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PayOsService> _logger;

    public PayOsService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<PayOsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration["PayOS:ClientId"])
        && !string.IsNullOrWhiteSpace(_configuration["PayOS:ApiKey"])
        && !string.IsNullOrWhiteSpace(_configuration["PayOS:ChecksumKey"]);

    public async Task<PayOsCheckoutResult> CreatePaymentLinkAsync(
        DonHang order,
        ThanhToan payment,
        int payOsOrderCode,
        string returnUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("payOS chưa được cấu hình API credentials.");
        if (order.TongTien <= 0 || order.TongTien != decimal.Truncate(order.TongTien))
            throw new InvalidOperationException("Số tiền payOS phải là số nguyên VND lớn hơn 0.");

        var amount = decimal.ToInt64(order.TongTien);
        // The description field is limited to 9 characters for some linked bank accounts.
        var description = $"DH{order.DonHangID}";
        if (description.Length > 9) description = order.DonHangID.ToString(CultureInfo.InvariantCulture);
        var signatureData = $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={payOsOrderCode}&returnUrl={returnUrl}";
        var payload = new PayOsCreateRequest
        {
            OrderCode = payOsOrderCode,
            Amount = amount,
            Description = description,
            BuyerName = order.TenKhachDat,
            BuyerEmail = order.EmailKhachDat,
            BuyerPhone = order.SoDienThoaiNhan,
            Items = [new PayOsItem { Name = $"GaRan #{order.DonHangID}", Quantity = 1, Price = amount }],
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl,
            Signature = CreateSignature(signatureData)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/v2/payment-requests")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("x-client-id", _configuration["PayOS:ClientId"]);
        request.Headers.Add("x-api-key", _configuration["PayOS:ApiKey"]);

        using var response = await _httpClientFactory.CreateClient(nameof(PayOsService)).SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("payOS create-link request failed with HTTP {StatusCode}.", (int)response.StatusCode);
            throw new InvalidOperationException("Không tạo được liên kết thanh toán payOS. Hãy kiểm tra cấu hình tài khoản payOS.");
        }

        PayOsApiResponse? apiResponse;
        try
        {
            using var responseDocument = JsonDocument.Parse(responseBody);
            var responseRoot = responseDocument.RootElement;
            if (!responseRoot.TryGetProperty("data", out var responseData) ||
                !responseRoot.TryGetProperty("signature", out var responseSignature) ||
                responseSignature.ValueKind != JsonValueKind.String ||
                !VerifySignature(responseData, responseSignature.GetString() ?? string.Empty))
            {
                _logger.LogError("payOS create-link response signature validation failed for order {OrderId}.", order.DonHangID);
                throw new InvalidOperationException("Không xác thực được phản hồi thanh toán từ payOS.");
            }
            apiResponse = JsonSerializer.Deserialize<PayOsApiResponse>(responseBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "payOS returned an invalid create-link response.");
            throw new InvalidOperationException("Phản hồi từ payOS không hợp lệ.");
        }

        var result = apiResponse?.Data;
        if (apiResponse?.Code != "00" || result == null || result.OrderCode != payOsOrderCode ||
            result.Amount != amount || string.IsNullOrWhiteSpace(result.PaymentLinkId) ||
            !Uri.TryCreate(result.CheckoutUrl, UriKind.Absolute, out var checkoutUri) || checkoutUri.Scheme != Uri.UriSchemeHttps)
        {
            _logger.LogWarning("payOS response did not contain a valid payment link for order {OrderId}.", order.DonHangID);
            throw new InvalidOperationException("payOS không trả về liên kết thanh toán hợp lệ cho đơn hàng.");
        }

        payment.PayOsPaymentLinkId = result.PaymentLinkId;
        payment.PayOsCheckoutUrl = result.CheckoutUrl;
        payment.MaGiaoDich = description;
        return new PayOsCheckoutResult(result.PaymentLinkId, result.CheckoutUrl, result.QrCode, result.Amount);
    }

    public async Task<string> GetPaymentLinkStatusAsync(string paymentLinkId, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("payOS chưa được cấu hình API credentials.");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/v2/payment-requests/{Uri.EscapeDataString(paymentLinkId)}");
        request.Headers.Add("x-client-id", _configuration["PayOS:ClientId"]);
        request.Headers.Add("x-api-key", _configuration["PayOS:ApiKey"]);
        using var response = await _httpClientFactory.CreateClient(nameof(PayOsService)).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Không kiểm tra được trạng thái thanh toán với payOS. Vui lòng thử lại.");
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (!root.TryGetProperty("code", out var code) || code.GetString() != "00" ||
            !root.TryGetProperty("data", out var data) || !root.TryGetProperty("signature", out var signature) ||
            signature.ValueKind != JsonValueKind.String || !VerifySignature(data, signature.GetString() ?? string.Empty) ||
            !data.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Không xác thực được trạng thái thanh toán do payOS trả về.");
        return status.GetString() ?? string.Empty;
    }

    public bool VerifyWebhook(JsonElement data, string signature) => VerifySignature(data, signature);

    private bool VerifySignature(JsonElement data, string signature)
    {
        var checksumKey = _configuration["PayOS:ChecksumKey"];
        if (string.IsNullOrWhiteSpace(checksumKey) || string.IsNullOrWhiteSpace(signature) || data.ValueKind != JsonValueKind.Object)
            return false;

        var fields = data.EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}={SignatureValue(property.Value)}");
        var canonicalData = string.Join('&', fields);
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(checksumKey), Encoding.UTF8.GetBytes(canonicalData));
        try
        {
            var supplied = Convert.FromHexString(signature);
            return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
        }
        catch (FormatException) { return false; }
    }

    private static string SignatureValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Object => string.Join('&', value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => $"{x.Name}={SignatureValue(x.Value)}")),
        JsonValueKind.Array => string.Join(',', value.EnumerateArray().Select(SignatureValue)),
        _ => value.GetRawText()
    };

    private string CreateSignature(string value) => Convert.ToHexString(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(_configuration["PayOS:ChecksumKey"]!), Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class PayOsCreateRequest
    {
        [JsonPropertyName("orderCode")] public int OrderCode { get; init; }
        [JsonPropertyName("amount")] public long Amount { get; init; }
        [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;
        [JsonPropertyName("buyerName")] public string? BuyerName { get; init; }
        [JsonPropertyName("buyerEmail")] public string? BuyerEmail { get; init; }
        [JsonPropertyName("buyerPhone")] public string? BuyerPhone { get; init; }
        [JsonPropertyName("items")] public List<PayOsItem> Items { get; init; } = [];
        [JsonPropertyName("cancelUrl")] public string CancelUrl { get; init; } = string.Empty;
        [JsonPropertyName("returnUrl")] public string ReturnUrl { get; init; } = string.Empty;
        [JsonPropertyName("signature")] public string Signature { get; init; } = string.Empty;
    }

    private sealed class PayOsItem
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("quantity")] public int Quantity { get; init; }
        [JsonPropertyName("price")] public long Price { get; init; }
    }

    private sealed class PayOsApiResponse
    {
        [JsonPropertyName("code")] public string? Code { get; init; }
        [JsonPropertyName("signature")] public string? Signature { get; init; }
        [JsonPropertyName("data")] public PayOsLinkData? Data { get; init; }
    }

    private sealed class PayOsLinkData
    {
        [JsonPropertyName("orderCode")] public int OrderCode { get; init; }
        [JsonPropertyName("amount")] public long Amount { get; init; }
        [JsonPropertyName("paymentLinkId")] public string? PaymentLinkId { get; init; }
        [JsonPropertyName("checkoutUrl")] public string? CheckoutUrl { get; init; }
        [JsonPropertyName("qrCode")] public string? QrCode { get; init; }
    }
}

public sealed record PayOsCheckoutResult(string PaymentLinkId, string CheckoutUrl, string? QrCode, long Amount);
