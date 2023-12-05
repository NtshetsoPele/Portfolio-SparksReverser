// See https://aka.ms/new-console-template for more information
using System.Text;
using System.Text.Json;
using System.Xml;

if (args?.Length > 0)
{
    string logFilePath = "Logs/CashFlowExtracts.txt";
    string[] logFileCashFlows = await File.ReadAllLinesAsync(logFilePath);

    foreach (var logFileCashFlow in logFileCashFlows)
    {
        XmlDocument xmlDoc = new();
        xmlDoc.LoadXml(logFileCashFlow);

        XmlNode amountNode = xmlDoc.SelectSingleNode("//Amount")!;
        if (amountNode != null)
        {
            string amountAsText = amountNode.InnerText;
            Console.WriteLine("Original Amount: " + amountAsText);

            if (decimal.TryParse(amountAsText, out decimal amount))
            {
                decimal newAmount = amount * -1;
                amountNode.InnerText = newAmount.ToString();
                Console.WriteLine("Updated Amount: " + newAmount);
            }

            Console.WriteLine("Modified XML:");
            PrettyPrintXml(xmlDoc);
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("Amount node not found.");
        }

        XmlNode trackingNode = xmlDoc.SelectSingleNode("//Tracking")!;
        if (trackingNode != null)
        {
            XmlAttribute apfoTranIdAttribute = trackingNode.Attributes?["APFOTRANID"]!;
            if (apfoTranIdAttribute != null)
            {
                Console.WriteLine("Original APFOTRANID: " + apfoTranIdAttribute.Value);
                apfoTranIdAttribute.Value = GenerateFoTranId();
                Console.WriteLine("New APFOTRANID: " + apfoTranIdAttribute.Value);

                Console.WriteLine("Modified XML:");
                PrettyPrintXml(xmlDoc);
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("APFOTRANID attribute not found in the Tracking node.");
            }
        }
        else
        {
            Console.WriteLine("Tracking node not found.");
        }

        await PostToApiAsync(new SparksMoneyFlow(xmlDoc.OuterXml));
    }
}
else
{
    Console.WriteLine("No cash flow identifiers received.");
}

static string GenerateFoTranId() =>
    string.Concat("SPX_", Guid.NewGuid().ToString().AsSpan(start: 0, length: 16));

static void PrettyPrintXml(XmlDocument xmlDoc)
{
    XmlWriterSettings settings = new()
    {
        Indent = true,
        IndentChars = "\t", 
        NewLineChars = "\n",  
        NewLineHandling = NewLineHandling.Replace
    };

    using var writer = XmlWriter.Create(Console.Out, settings);

    xmlDoc.WriteTo(writer);
}

async Task PostToApiAsync(SparksMoneyFlow cashFlow)
{
    const string apiUrl = "sparks_api_url";

    using HttpClient httpClient = new();
    StringContent content = new(JsonSerializer.Serialize(cashFlow), Encoding.UTF8, "application/xml");
    HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content);

    if (response.IsSuccessStatusCode)
    {
        Console.WriteLine("POST request successful");
    }
    else
    {
        Console.WriteLine($"POST request failed with status code {response.StatusCode}");
    }
}

public record SparksMoneyFlow(string Message);