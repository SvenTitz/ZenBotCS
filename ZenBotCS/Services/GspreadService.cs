using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;


namespace ZenBotCS.Services;

public class GspreadService
{
    private const string UrlTemplate = "https://docs.google.com/spreadsheets/d/{0}#gid={1}";
    private readonly SheetsService _sheetsService;
    private readonly DriveService _driveService;
    private readonly IConfiguration _config;

    public GspreadService(IConfiguration congig)
    {
        _config = congig;
        var credential = GoogleCredential.FromFile(_config["PathToGspreadCredentials"])
            .CreateScoped(SheetsService.Scope.Spreadsheets, DriveService.Scope.DriveFile);
        _sheetsService = new SheetsService(new SheetsService.Initializer
        {
            HttpClientInitializer = credential
        });
        _driveService = new DriveService(new BaseClientService.Initializer()
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
            var perms = new Permission();
            perms.Role = "writer";
            perms.Type = "anyone";
            _driveService.Permissions.Create(perms, spreadsheetId).Execute();
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
            string mergeStartCell = GetColumnLetter(2 + 5 * i) + "1";
            string mergeEndCell = GetColumnLetter(6 + 5 * i) + "1";
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
                                StartColumnIndex = (2 + 5 * i),
                                EndColumnIndex = (6 + 5 * i)
                            }
                        }
                    }
                }
            }, spreadsheetId);
            update.Execute();
        }

        return string.Format(UrlTemplate, spreadsheetId, sheetId);
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

    private int GetColumnIndex(string columnLetter)
    {
        int result = 0;

        foreach (char c in columnLetter)
        {
            result *= 26;
            result += c - 'A' + 1;
        }

        return result;
    }

}

