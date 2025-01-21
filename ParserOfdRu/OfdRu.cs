using RetailCorrector.API;
using RetailCorrector.API.Data;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text;
using System.ComponentModel;
using RetailCorrector.API.Static;
using RetailCorrector.API.Types;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace ParserOfdRu;

[DisplayName("OFD.ru (Токен)")]
public class OfdRu : Parser
{
    private readonly static JsonSerializerOptions jsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic),
    };

    public override async Task<List<Receipt>> ParseReceipts(DateOnly day)
    {
        using var client = new HttpClient();
        var uri = new StringBuilder(
            "https://ofd.ru/api/integration/v2/inn/%vatin%/kkt/%kkt%/receipts-" +
            "info?dateFrom=%date%T00:00:00&dateTo=%date%T23:59:59&AuthToken=%token%"
            );
        uri.Replace("%vatin%", ParserData.Vatin);
        uri.Replace("%kkt%", ParserData.RegId);
        uri.Replace("%date%", $"{day:yyyy'-'MM'-'dd}");
        uri.Replace("%token%", ParserData.Token);
        var resp = await client.GetAsync(uri.ToString());
        var body = await resp.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonNode>(body, jsonSerializerOptions)!;
        var receipts = new List<Receipt>();
        foreach (var e in json["Data"]!.AsArray())
        {
            if (e is null) continue;
            var operation = e["OperationType"]!.GetValue<string>().ToLower();
            var items = e["Items"]!.AsArray();
            var cdata = new CorrectionData
            {
                DocId = " ", // todo fix
                FiscalSign = e["DecimalFiscalSign"]!.GetValue<string>(),
                CreatedDate = DateTime.ParseExact(e["DocDateTime"]!.GetValue<string>(), "yyyy'-'MM'-'dd'T'HH':'mm':'ss", CultureInfo.InvariantCulture),
            };
            var op = new Operation
            {
                IsIncome = operation.EndsWith("income"),
                IsRefund = operation.StartsWith("refund"),
            };
            var pay = new Payment
            {
                CashSum = e["CashSumm"]!.GetValue<int>(),
                EcashSum = e["ECashSumm"]!.GetValue<int>(),
                PostpaidSum = e["CreditSumm"]!.GetValue<int>(),
                PrepaidSum = e["PrepaidSumm"]!.GetValue<int>(),
            };
            var receipt = new Receipt
            {
                DocId = e["DocNumber"]!.GetValue<int>(),
                Total = e["TotalSumm"]!.GetValue<int>(),
                Correction = cdata,
                Operation = op,
                Payment = pay,
                Items = new Position[items.Count],
            };

            for (var i = 0; i < items.Count; i++)
            {
                var rawMeasure = items[i]!["ProductUnitOfMeasure"]?.GetValue<string>();
                var measure = rawMeasure is null
                    ? new MeasureUnit()
                    : MeasureUnit.Parse((byte)int.Parse(rawMeasure));
                receipt.Items[i] = new Position
                {
                    Name = items[i]!["Name"]!.GetValue<string>(),
                    Price = items[i]!["Price"]!.GetValue<int>(),
                    Quantity = items[i]!["Quantity"]!.GetValue<double>(),
                    Sum = items[i]!["Total"]!.GetValue<int>(),
                    ItemType = items[i]!["SubjectType"]!.GetValue<byte>(),
                    MeasureUnit = measure,
                    PayMethod = items[i]!["CalculationMethod"]!.GetValue<byte>(),
                    TaxRate = TaxRate.Parse(items[i]!["NDS_Rate"]!.GetValue<byte>()),
                };
            }

            receipts.Add(receipt);
        }

        return receipts;
    }
}
