using GenericParsing;
using System.Data;

namespace MLAgent.Helpers;
public class CsvHelpers
{
    public static async Task<DataTable> ToDataTable(string CsvPath)
    {
        try
        {
            using var csv = Sylvan.Data.Csv.CsvDataReader.Create(CsvPath);
            var dt = await csv.GetSchemaTableAsync();
            return dt;

        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine(ex);
        }
        return default;
        /*
        while (await csv.ReadAsync())
        {
            var id = csv.GetInt32(0);
            var name = csv.GetString(1);
            var date = csv.GetDateTime(2);
        }*/
    }

    public static DataTable ConvertCSVtoDataTable(string Content)
    {
        try
        {
            DataTable dataTable = new DataTable();
            using (TextReader sr = new StringReader(Content))
            {
                using GenericParserAdapter dataParser = new GenericParserAdapter();
                dataParser.SetDataSource(sr);
                dataParser.ColumnDelimiter = ',';
                dataParser.FirstRowHasHeader = true;
                dataParser.SkipStartingDataRows = 0;
                dataParser.MaxBufferSize = 4096;
                dataParser.MaxRows = 1000000;
                dataParser.TextQualifier = '\"';

                dataTable = dataParser.GetDataTable();
                return dataTable;
            }
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine(ex);
        }
        return default;
    }

    public static async Task<DataTable> CSVToDataTable(string Content)
    {
        try
        {
            using (TextReader sr = new StringReader(Content))
            {
                using var csv = Sylvan.Data.Csv.CsvDataReader.Create(sr);
                var dt = new DataTable();
                dt.Load(csv);
                return dt;
            }
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine(ex);
        }
        return default;
        /*
        while (await csv.ReadAsync())
        {
            var id = csv.GetInt32(0);
            var name = csv.GetString(1);
            var date = csv.GetDateTime(2);
        }*/
    }
   
}