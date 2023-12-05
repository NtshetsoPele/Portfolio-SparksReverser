
try
{
    var config = GetConfiguration();
    Log.Logger = GetLogger(config);

    await ExtractSettlementIdsAsync();
}
catch (Exception ex)
{
    Log.Fatal(messageTemplate: $"Error extracting settlement id: {ex.Message}.");
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

// Add logging as necessary.
static async Task ExtractSettlementIdsAsync()
{
    await foreach (string pattern in File.ReadLinesAsync(path: "Logs/List_Of_PNumbers.txt"))
    {
        IEnumerable<string> matchingLines = SearchLinesWithPattern(filePath: "Logs/Sparks_LogFile_05-12-2023.txt", pattern, linesBefore: 10);

        IEnumerable<string> extractedPatterns = ExtractPatternsFromLines(matchingLines, @"[A-Z0-9]{24}_1201");

        File.AppendAllLines("Logs/DuplicateSettlementIds.txt", extractedPatterns);
    }
}

static IEnumerable<string> SearchLinesWithPattern(string filePath, string pattern, int linesBefore)
{
    List<string> scratchPad = [];

    using StreamReader reader = new(filePath);
    string line;
    while ((line = reader.ReadLine()!) != null)
    {
        if (Regex.IsMatch(line, pattern))
        {
            yield return scratchPad[^linesBefore];
        }
        scratchPad.Add(line);
    }
}

static IEnumerable<string> ExtractPatternsFromLines(IEnumerable<string> lines, string pattern)
{
    List<string> extractedPatterns = [];

    foreach (string line in lines)
    {
        Match match = Regex.Match(line, pattern);
        if (match.Success)
        {
            extractedPatterns.Add(match.Value);
        }
    }

    return extractedPatterns;
}

Log.Information("Processing complete.");