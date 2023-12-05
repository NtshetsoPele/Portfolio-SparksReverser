
try
{
    var config = GetConfiguration();
    Log.Logger = GetLogger(config);

    await CoordinateReversalsAsync(args, config["SparksUrl"]!);
}
catch (Exception ex)
{
    Log.Fatal(messageTemplate: $"Error processing reversals: {ex.Message}.");
}
finally
{
    Log.CloseAndFlush();
}

#region Config

static IConfigurationRoot GetConfiguration()
{
    return new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: true)
        .Build();
}

static Logger GetLogger(IConfiguration config)
{
    return new LoggerConfiguration()
        .ReadFrom.Configuration(config)
        .CreateLogger();
}

#endregion

static async Task CoordinateReversalsAsync(ICollection<string> args, string cashFlowUrl)
{
    if (args?.Count > 0)
    {
        XmlDocument xmlDoc = new();

        foreach (var logFileCashFlow in await File.ReadAllLinesAsync(Resrc.LogFile))
        {
            xmlDoc.LoadXml(logFileCashFlow);

            TransformAmountTag(xmlDoc);

            TransformSpxAttribute(xmlDoc);

            await PostToApiAsync(cashFlow: new SparksMoneyFlow(xmlDoc.OuterXml), cashFlowUrl);

            xmlDoc.RemoveAll();
        }
    }
}

static void TransformSpxAttribute(XmlDocument xmlDoc)
{
    XmlNode? trackingNode = xmlDoc.SelectSingleNode(xpath: "//Tracking")!;
    XmlAttribute? apfoTranIdAttribute = GetApfoTranIdAttribute(trackingNode);

    if (apfoTranIdAttribute != null)
    {
        apfoTranIdAttribute.Value = GenerateFoTranId();

        Log.Information($"Modified XML: \n{GetPrettifiedXml(xmlDoc)}.\n");
    }
}

static XmlAttribute? GetApfoTranIdAttribute(XmlNode trackingNode) =>
    trackingNode?.Attributes?["APFOTRANID"];

static void TransformAmountTag(XmlDocument xmlDoc)
{
    XmlNode amountNode = xmlDoc.SelectSingleNode(xpath: "//Amount")!;

    if (amountNode != null && 
        decimal.TryParse(amountNode.InnerText, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
    {
        decimal newAmount = amount * (-1);
        amountNode.InnerText = newAmount.ToString();

        Log.Information($"Modified XML: \n{GetPrettifiedXml(xmlDoc)}.\n");
    }
}

static string GenerateFoTranId() =>
    string.Concat("SPX_", Guid.NewGuid().ToString().AsSpan(start: 0, length: 16));

static string GetPrettifiedXml(XmlNode xmlNode)
{
    using StringWriter stringWriter = new();
    XmlWriterSettings xmlWriterSettings = new()
    {
        Indent = true,
        IndentChars = "    ",
        NewLineChars = "\n",
        NewLineHandling = NewLineHandling.Replace
    };

    using (var xmlWriter = XmlWriter.Create(stringWriter, xmlWriterSettings))
    {
        xmlNode.WriteTo(xmlWriter);
    }

    return stringWriter.ToString();
}

static async Task PostToApiAsync(SparksMoneyFlow cashFlow, string cashFlowsUrl) =>
    await MoneyFlowDispatch.PostToApiAsync(cashFlow, cashFlowsUrl);

class MoneyFlowDispatch : IDisposable
{
    private static readonly HttpClient _httpClient;

    static MoneyFlowDispatch() => _httpClient = new();

    static internal async Task PostToApiAsync(SparksMoneyFlow cashFlow, string cashFlowsUrl)
    {
        StringContent content = new(JsonSerializer.Serialize(cashFlow), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _httpClient.PostAsync(cashFlowsUrl, content);

        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}