using System.Text.RegularExpressions;
using CocApi.Rest.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZenBotCS.Entities.Models;


namespace ZenBotCS.Services;

public class GspreadService
{
    private const string UrlTemplate = "https://docs.google.com/spreadsheets/d/{0}#gid={1}";
    private readonly SheetsService _sheetsService;
    private readonly DriveService _driveService;
    private readonly IConfiguration _config;
    private readonly ILogger<GspreadService> _logger;

    public GspreadService(IConfiguration congig, ILogger<GspreadService> logger)
    {
        _config = congig;
        _logger = logger;

        string credPath = _config["PathToGspreadToken"]!;

#if !DEBUG
        if (!System.IO.File.Exists(Path.Combine(credPath, "Google.Apis.Auth.OAuth2.Responses.TokenResponse-user")))
        {
            _logger.LogError("Goole OAuth2 Token gone again. FUCK MY LIFE");
            return;
        }
#endif

        UserCredential credential;
        using (var stream = new FileStream(_config["PathToGspreadCredentialsOAuth2"]!, FileMode.Open, FileAccess.Read))
        {

            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                new[] { SheetsService.Scope.Spreadsheets, DriveService.Scope.DriveFile, DriveService.Scope.Drive },
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true)).Result;
        }

        _sheetsService = new SheetsService(new SheetsService.Initializer
        {
            HttpClientInitializer = credential,
        });

        _driveService = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
        });
    }

    public string WriteCwlData(object[][] data, string clanname, string? spreadsheetId = null)
    {
        string sheetName = clanname + " " + DateTime.Now.ToString("dd/MM/yyyy, HH:mm:ss");
        int columns = data.Length > 0 ? data[0].Length : 0;
        int rows = data.Length;
        string startCell = "A1";
        string endCell = $"{GetColumnLetter(columns)}{rows}";
        string cellRange = $"{startCell}:{endCell}";

        Spreadsheet spreadsheet;

        if (string.IsNullOrEmpty(spreadsheetId))
        {
            spreadsheet = new Spreadsheet { Properties = new SpreadsheetProperties { Title = "Zen Bot CWL Data" } };
            spreadsheetId = _sheetsService.Spreadsheets.Create(spreadsheet).Execute().SpreadsheetId;

            // Give write permissions to everyone
            ShareSpreadsheet(spreadsheetId);
        }
        else
        {
            spreadsheet = _sheetsService.Spreadsheets.Get(spreadsheetId).Execute();
        }

        var request = new AddSheetRequest
        {
            Properties = new SheetProperties
            {
                Title = sheetName,
                GridProperties = new GridProperties
                {
                    RowCount = rows,
                    ColumnCount = columns
                }
            }
        };

        var addSheetRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request>
            {
                new Request { AddSheet = request }
            }
        };

        var response = _sheetsService.Spreadsheets.BatchUpdate(addSheetRequest, spreadsheetId).Execute();
        var sheetId = response.Replies[0].AddSheet.Properties.SheetId;

        var updateRequestData = new ValueRange { Values = data };
        var updateRequest = _sheetsService.Spreadsheets.Values.Update(updateRequestData, spreadsheetId, sheetName + "!" + cellRange);
        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        updateRequest.Execute();


        // Sort by TH, then player name
        var sortRange = new SortRangeRequest
        {
            Range = new GridRange
            {
                SheetId = sheetId,
                StartRowIndex = 2,  // Start from the 3rd row (index 2)
                EndRowIndex = rows,
                StartColumnIndex = 0,
                EndColumnIndex = columns
            },
            SortSpecs = new List<SortSpec>
            {
                new SortSpec { DimensionIndex = 1, SortOrder = "ASCENDING" },
                new SortSpec { DimensionIndex = 0, SortOrder = "ASCENDING" }
            }
        };

        _sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request>
            {
                new Request { SortRange = sortRange }
            }
        }, spreadsheetId).Execute();

        // Merge day cells
        for (int i = 0; i < 7; i++)
        {
            string mergeStartCell = GetColumnLetter(3 + 3 * i) + "1";
            string mergeEndCell = GetColumnLetter(5 + 3 * i) + "1";
            var update = _sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>
                {
                    new Request
                    {
                        MergeCells = new MergeCellsRequest
                        {
                            MergeType = "MERGE_ALL",
                            Range = new GridRange
                            {
                                SheetId = sheetId,
                                StartRowIndex = 0,
                                EndRowIndex = 0,
                                StartColumnIndex = (3 + 3 * i),
                                EndColumnIndex = (5 + 3 * i)
                            }
                        }
                    }
                }
            }, spreadsheetId);
            update.Execute();
        }

        return string.Format(UrlTemplate, spreadsheetId, sheetId);
    }

    public async Task<string> WriteCwlRosterData(object?[][] data, Clan clan, ClanSettings? clanSettings, bool isChampStyle)
    {
        // Select the appropriate template ID based on the isChampStyle flag
        var templateId = isChampStyle
            ? _config["CwlRosterChampStyleTemplateSpreadsheetId"]!
            : _config["CwlRosterTemplateSpreadsheetId"]!;

        var endColumn = isChampStyle ? 18 : 13;
        (var spreadsheetId, var sheetName) = await CopyCwlRosterSpreadsheet(clan, clanSettings, templateId, endColumn);

        // Determine the range based on the size of the data array
        var numRows = data.Length;
        var numCols = data.Max(row => row?.Length ?? 0);
        var range = $"A3:{endColumn}{2 + numRows}"; // Data will be written starting from A3

        // Create the update request with the provided data
        var updateRequestData = new ValueRange { Values = data };
        var updateRequest = _sheetsService.Spreadsheets.Values.Update(updateRequestData, spreadsheetId, $"{sheetName}!{range}");
        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        updateRequest.Execute();

        // Create the requests to delete empty rows
        var requests = new List<Request>();

        // Delete all rows from after the data to row 102
        if (numRows < 100)
        {
            requests.Add(new Request
            {
                DeleteDimension = new DeleteDimensionRequest
                {
                    Range = new DimensionRange
                    {
                        SheetId = 0, // Assuming the sheet ID is 0 for "Roster" sheet
                        Dimension = "ROWS",
                        StartIndex = numRows + 2, // Row index to start deleting (0-based index, +2 for 1-based to 0-based and header rows)
                        EndIndex = 102 // Row index to end deleting (exclusive)
                    }
                }
            });
        }

        var batchUpdateRequest = new BatchUpdateSpreadsheetRequest { Requests = requests };
        var batchUpdate = _sheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId);
        batchUpdate.Execute();

        return string.Format(UrlTemplate, spreadsheetId, "0");
    }



    private string GetColumnLetter(int number)
    {
        int dividend = number;
        string columnLetter = "";

        while (dividend > 0)
        {
            int modulo = (dividend - 1) % 26;
            columnLetter = Convert.ToChar(65 + modulo) + columnLetter;
            dividend = (dividend - modulo) / 26;
        }

        return columnLetter;
    }

    public async Task<(string spreadsheetId, string? sheetName)> CopyCwlRosterSpreadsheet(Clan clan, ClanSettings? clanSettings, string templateSpreadsheetId, int endColumnIndex)
    {
        try
        {
            // Create a copy of the template spreadsheet
            var copyRequest = new Google.Apis.Drive.v3.Data.File
            {
                Name = clan.Name + " Roster " + DateTime.Now.ToString("yyyy-MM-dd")
            };
            var request = _driveService.Files.Copy(copyRequest, templateSpreadsheetId);
            var copiedFile = await request.ExecuteAsync();

            ShareSpreadsheet(copiedFile.Id);

            if (clanSettings?.ColorHex is not null)
            {
                var batchUpdateSpreadsheetRequest = new BatchUpdateSpreadsheetRequest { Requests = [] };

                var recolorRange = new GridRange()
                {
                    SheetId = 0,
                    StartRowIndex = 0, // Start row index of the range
                    EndRowIndex = 2, // End row index of the range
                    StartColumnIndex = 0, // Start column index of the range
                    EndColumnIndex = endColumnIndex // End column index of the range
                };

                batchUpdateSpreadsheetRequest.Requests.Add(BuildRecolorRangeRequest(copiedFile.Id, recolorRange, clanSettings.ColorHex));

                recolorRange = new GridRange()
                {
                    SheetId = 0,
                    StartRowIndex = 102, // Start row index of the range
                    EndRowIndex = 103, // End row index of the range
                    StartColumnIndex = 0, // Start column index of the range
                    EndColumnIndex = endColumnIndex // End column index of the range
                };

                batchUpdateSpreadsheetRequest.Requests.Add(BuildRecolorRangeRequest(copiedFile.Id, recolorRange, clanSettings.ColorHex));

                var batchUpdateRequest = _sheetsService.Spreadsheets.BatchUpdate(batchUpdateSpreadsheetRequest, copiedFile.Id);
                batchUpdateRequest.Execute();
            }

            var firstSheetName = await GetSheetNameAsync(copiedFile.Id, "0");

            var valueRange = new ValueRange { Values = [[clan.Name]] };
            var updateRequest = _sheetsService.Spreadsheets.Values.Update(valueRange, copiedFile.Id, $"{firstSheetName}" + "!" + "A1:A1");
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            updateRequest.Execute();

            // Get the ID of the copied spreadsheet
            return (copiedFile.Id, firstSheetName);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    public string CreateDbDumpSpreadsheet(string sheetName, object[][] data)
    {
        var spreadsheet = new Spreadsheet { Properties = new SpreadsheetProperties { Title = sheetName } };
        var spreadsheetId = _sheetsService.Spreadsheets.Create(spreadsheet).Execute().SpreadsheetId;

        // Give write permissions to everyone
        ShareSpreadsheet(spreadsheetId);

        var valueRange = new ValueRange();

        var rows = new List<IList<object>>();
        foreach (var row in data)
        {
            rows.Add(new List<object>(row));
        }

        valueRange.Values = rows;

        var appendRequest = _sheetsService.Spreadsheets.Values.Append(valueRange, spreadsheetId, "Sheet1!A1");
        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        var appendResponse = appendRequest.Execute();

        return string.Format(UrlTemplate, spreadsheetId, "0");
    }

    private void ShareSpreadsheet(string spreadsheetId)
    {
        var perms = new Permission();
        perms.Role = "writer";
        perms.Type = "anyone";
        _driveService.Permissions.Create(perms, spreadsheetId).Execute();
    }

    private Request BuildRecolorRangeRequest(string spreadsheetId, GridRange range, string colorHex)
    {
        var color = new Color
        {
            Alpha = 1,
            Red = Convert.ToInt32(colorHex.Substring(1, 2), 16) / 255f,
            Green = Convert.ToInt32(colorHex.Substring(3, 2), 16) / 255f,
            Blue = Convert.ToInt32(colorHex.Substring(5, 2), 16) / 255f
        };

        // Create the cell format with the background color
        var cellFormat = new CellFormat
        {
            BackgroundColor = color
        };

        // Create the repeat cell request
        var repeatCellRequest = new RepeatCellRequest
        {
            Range = range,
            Cell = new CellData()
            {
                UserEnteredFormat = cellFormat
            },
            Fields = "UserEnteredFormat.BackgroundColor"
        };

        return new Request { RepeatCell = repeatCellRequest };
    }

    public async Task<List<string>> GetPlayerTags(string spreadsheetUrl)
    {
        (string? spreadsheetId, string? sheetGid) = ExtractSpreadsheetInfo(spreadsheetUrl);
        if (string.IsNullOrEmpty(spreadsheetId))
        {
            throw new ArgumentException("Invalid spreadsheet URL.");
        }

        string sheetName = "Sheet1"; // Default sheet name
        if (!string.IsNullOrEmpty(sheetGid))
        {
            sheetName = (await GetSheetNameAsync(spreadsheetId, sheetGid)) ?? sheetName;
        }

        // Define the range for column C
        string range = $"{sheetName}!B:B";

        // Fetch the data from the specified range
        SpreadsheetsResource.ValuesResource.GetRequest request =
            _sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);

        ValueRange response = request.Execute();
        IList<IList<object>> values = response.Values;

        // Prepare a list to hold the results
        List<string> result = [];

        // Check if there are values in the response
        if (values != null && values.Count > 0)
        {
            // Process the values, excluding the first two and the last one
            foreach (var value in values)
            {
                if (value.Count <= 0)
                    continue;

                var valueString = value[0].ToString();

                if (valueString?.StartsWith('#') ?? false)
                    result.Add(valueString);
            }
        }

        return result;
    }

    private string? ExtractSpreadsheetId(string url)
    {
        // Use a regular expression to extract the spreadsheet ID
        var match = Regex.Match(url, @"/spreadsheets/d/([a-zA-Z0-9-_]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    public (string? spreadsheetId, string? sheetGid) ExtractSpreadsheetInfo(string url)
    {
        // Use a regular expression to extract the spreadsheet ID and the sheet GID
        var match = Regex.Match(url, @"/spreadsheets/(?:u/\d+/)?d/([a-zA-Z0-9-_]+)/?(?:edit|htmlview)?(?:.*?[#&]gid=([0-9]+))?");
        if (match.Success)
        {
            var spreadsheetId = match.Groups[1].Value;
            var sheetGid = match.Groups[2].Success ? match.Groups[2].Value : null;
            return (spreadsheetId, sheetGid);
        }
        return (null, null);
    }

    private async Task<string?> GetSheetNameAsync(string spreadsheetId, string sheetGid)
    {
        var request = _sheetsService.Spreadsheets.Get(spreadsheetId);
        var response = await request.ExecuteAsync();

        foreach (var sheet in response.Sheets)
        {
            if (sheet.Properties.SheetId.ToString() == sheetGid)
            {
                return sheet.Properties.Title;
            }
        }
        return null;
    }

    internal string GetUrl(PinnedRoster pinnedRoster)
    {
        return string.Format(UrlTemplate, pinnedRoster.SpreadsheetId, pinnedRoster.Gid);
    }
}

