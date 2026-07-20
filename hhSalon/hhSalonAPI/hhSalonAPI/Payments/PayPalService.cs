using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace hhSalonAPI.Payments
{
	public class PayPalService : IPayPalService
	{
		private readonly HttpClient _httpClient;
		private readonly IConfiguration _configuration;
		private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

		public PayPalService(HttpClient httpClient, IConfiguration configuration)
		{
			_httpClient = httpClient;
			_configuration = configuration;
			var environment = configuration["PayPal:Environment"];
			_httpClient.BaseAddress = new Uri(string.Equals(environment, "Live", StringComparison.OrdinalIgnoreCase)
				? "https://api-m.paypal.com"
				: "https://api-m.sandbox.paypal.com");
		}

		public string ClientId => _configuration["PayPal:ClientId"] ?? string.Empty;
		public string Currency => (_configuration["PayPal:Currency"] ?? "USD").ToUpperInvariant();
		public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(_configuration["PayPal:ClientSecret"]);

		public async Task<PayPalProviderOrder> CreateOrder(decimal amount, string clientId, string localTransactionId, CancellationToken cancellationToken)
		{
			var request = await CreateAuthorizedRequest(HttpMethod.Post, "/v2/checkout/orders", cancellationToken);
			request.Headers.Add("PayPal-Request-Id", localTransactionId);
			request.Headers.Add("Prefer", "return=representation");
			request.Content = JsonContent.Create(new
			{
				intent = "CAPTURE",
				purchase_units = new[]
				{
					new
					{
						custom_id = clientId,
						invoice_id = localTransactionId,
						description = "hhSalon appointment payment",
						amount = new
						{
							currency_code = Currency,
							value = amount.ToString("0.00", CultureInfo.InvariantCulture)
						}
					}
				},
				application_context = new { shipping_preference = "NO_SHIPPING", user_action = "PAY_NOW" }
			}, options: _jsonOptions);

			return await SendAndParse(request, cancellationToken);
		}

		public async Task<PayPalProviderOrder> CaptureOrder(string orderId, CancellationToken cancellationToken)
		{
			var request = await CreateAuthorizedRequest(HttpMethod.Post, $"/v2/checkout/orders/{Uri.EscapeDataString(orderId)}/capture", cancellationToken);
			request.Headers.Add("PayPal-Request-Id", $"capture-{orderId}");
			request.Headers.Add("Prefer", "return=representation");
			request.Content = JsonContent.Create(new { }, options: _jsonOptions);
			return await SendAndParse(request, cancellationToken);
		}

		public async Task<PayPalProviderOrder> GetOrder(string orderId, CancellationToken cancellationToken)
		{
			var request = await CreateAuthorizedRequest(HttpMethod.Get, $"/v2/checkout/orders/{Uri.EscapeDataString(orderId)}", cancellationToken);
			return await SendAndParse(request, cancellationToken);
		}

		private async Task<HttpRequestMessage> CreateAuthorizedRequest(HttpMethod method, string path, CancellationToken cancellationToken)
		{
			if (!IsConfigured)
				throw new InvalidOperationException("PayPal server credentials are not configured.");

			var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
			var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{_configuration["PayPal:ClientSecret"]}"));
			tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
			tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });

			using var tokenResponse = await _httpClient.SendAsync(tokenRequest, cancellationToken);
			var tokenBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
			if (!tokenResponse.IsSuccessStatusCode)
				throw new HttpRequestException($"PayPal authentication failed with status {(int)tokenResponse.StatusCode}.");

			using var tokenDocument = JsonDocument.Parse(tokenBody);
			var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString();
			if (string.IsNullOrWhiteSpace(accessToken))
				throw new HttpRequestException("PayPal did not return an access token.");

			var request = new HttpRequestMessage(method, path);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
			return request;
		}

		private async Task<PayPalProviderOrder> SendAndParse(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			using var response = await _httpClient.SendAsync(request, cancellationToken);
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
				throw new HttpRequestException($"PayPal request failed with status {(int)response.StatusCode}.");

			using var document = JsonDocument.Parse(body);
			var root = document.RootElement;
			var purchaseUnit = root.TryGetProperty("purchase_units", out var units) && units.GetArrayLength() > 0
				? units[0]
				: default;
			var amount = ExtractAmount(purchaseUnit);

			return new PayPalProviderOrder
			{
				Id = GetString(root, "id"),
				Status = GetString(root, "status"),
				Amount = amount.Value,
				Currency = amount.Currency,
				CustomId = GetString(purchaseUnit, "custom_id"),
				InvoiceId = GetString(purchaseUnit, "invoice_id"),
				PayerId = root.TryGetProperty("payer", out var payer) ? GetString(payer, "payer_id") : null,
				PayerEmail = root.TryGetProperty("payer", out payer) ? GetString(payer, "email_address") : null
			};
		}

		private static (decimal Value, string Currency) ExtractAmount(JsonElement purchaseUnit)
		{
			if (purchaseUnit.ValueKind == JsonValueKind.Undefined)
				return (0, null);

			JsonElement amount;
			if (purchaseUnit.TryGetProperty("payments", out var payments)
				&& payments.TryGetProperty("captures", out var captures)
				&& captures.GetArrayLength() > 0
				&& captures[0].TryGetProperty("amount", out amount))
			{
				return ParseAmount(amount);
			}

			return purchaseUnit.TryGetProperty("amount", out amount) ? ParseAmount(amount) : (0, null);
		}

		private static (decimal Value, string Currency) ParseAmount(JsonElement amount)
		{
			var valueText = GetString(amount, "value");
			return (decimal.TryParse(valueText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0,
				GetString(amount, "currency_code"));
		}

		private static string GetString(JsonElement element, string property)
			=> element.ValueKind != JsonValueKind.Undefined && element.TryGetProperty(property, out var value) ? value.GetString() : null;
	}
}
