/* Website.cs - (c) James S Renwick 2014
 * -------------------------------------
 * Version 1.2.0
 */
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Web.SessionState;
using WebMatrix.WebData;


/// <summary>
/// Class containing website-wide helper methods and constants.
/// </summary>
public static partial class Website
{
    public static bool IsChoir(int libraryValue)
    {
        return (libraryValue & Website.Library_Choir) != 0;
    }
    public static bool IsOrchestra(int libraryValue)
    {
        return (libraryValue & Website.Library_Orchestra) != 0;
    }

    public static IDictionary<IEnumerable<Library>, IEnumerable<dynamic>> GroupPiecesByLibraries(
        IEnumerable<dynamic> pieces)
    {
        var temp = new Dictionary<int, List<dynamic>>();

        foreach (var piece in pieces)
        {
            int lib = piece.Libraries;

            if (!temp.ContainsKey(lib)) {
                temp.Add(lib, new List<dynamic>());
            }
            temp[lib].Add(piece);
        }

        var output = new Dictionary<IEnumerable<Library>, IEnumerable<dynamic>>();

        foreach (int key in temp.Keys)
        {
            var libs = new List<Library>();
            foreach (var lib in Website.Libraries)
            {
                if ((key & lib.ID) != 0) libs.Add(lib);
            }

            output.Add(libs, temp[key]);
        }
        return output;
    }

    /// <summary>
    /// Gets a formatted currency string for the given double value.
    /// </summary>
    public static string GetPounds(double f)
    {
        return f.ToString("C2", CultureInfo.CreateSpecificCulture("en-GB"));
    }

    public static HttpSessionState Session
    {
        get { return HttpContext.Current.Session; }
    }

    /// <summary>
    /// Sanitises the input string by replacing symbols and control chars 
    /// with underscores. If input is null, returns null.
    /// </summary>
    public static string Sanitise(string value)
    {
        if (value == null) return null;

        var sb = new System.Text.StringBuilder(value.Length);
        foreach (char c in value)
        {
            if      (c == '@')                              sb.Append(c);
            else if (char.IsControl(c) || char.IsSymbol(c)) sb.Append('_');
            else                                            sb.Append(c);
        }
        return sb.ToString();
    }

    public static string SanitiseFilename(string filename)
    {
        if (filename == null) return null;

        var sb = new System.Text.StringBuilder(filename.Length);

        var badchars = System.IO.Path.GetInvalidFileNameChars();

        foreach (char c in filename) {
            sb.Append(badchars.Contains(c) ? '_' : c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Gets the return date for the loan.
    /// 
    /// If both are defined, takes the closest date from
    /// the next performance and the return-to-uni-library date.
    /// 
    /// If one of those is not null, uses that. Otherwise,
    /// defaults to a loan period of one month.
    /// </summary>
    /// <param name="piece">The piece record for which to get the return date.</param>
    public static DateTime CalculateReturnDate(dynamic piece)
    {
        DateTime? end = piece.LoanEnd;
        DateTime? per = piece.NextPerformance;
        DateTime  def = DateTime.Now.AddMonths(1); // Fallback is 1 month

        if (end != null && per != null) {
            return end.Value < per.Value ? end.Value : per.Value;
        }
        else return (DateTime)(end ?? per ?? def);
    }

    /// <summary>
    /// Converts the row object into a dynamic object whose
    /// fields and values you can edit.
    /// </summary>
    public static dynamic ExpandoFromRow(dynamic row)
    {
        IDictionary<string, object> obj = new ExpandoObject();

        foreach (var column in row.Columns) {
            obj.Add(column, row[column]);
        }
        return (ExpandoObject)obj;
    }
    /// <summary>
    /// Converts the given table into a dynamic object whose
    /// fields and values you can edit.
    /// </summary>
    public static dynamic[] ExpandoFromTable(IEnumerable<dynamic> table)
    {
        ExpandoObject[] output = new ExpandoObject[table.Count()];

        int i = 0;
        foreach (var row in table)
        { 
            IDictionary<string, object> obj = new ExpandoObject();

            foreach (var column in row.Columns) {
                obj.Add(column, row[column]);
            }
            output[i++] = (ExpandoObject)obj;
        }
        return output;
    }

    public static void RedirectToDownload(string filepath, string contentType, HttpResponseBase response)
    {
        FileInfo file = new FileInfo(filepath);
        if (file.Exists)
        {
            response.ClearContent();
            response.AddHeader("Content-Disposition", "attachment; filename=" + file.Name);
            response.AddHeader("Content-Length", file.Length.ToString());
            response.ContentType = contentType;
            response.TransmitFile(file.FullName);
            response.End();
        }
        else throw new FileNotFoundException();
    }

    public static void UploadHiddenFile(string filename, Stream data)
    {
        UploadHiddenFile(filename, "", data);
    }
    public static void UploadHiddenFile(string filename, string subdir, Stream data)
    {
        string path = HttpContext.Current.Server.MapPath(Path.Combine(Website.HiddenDir, subdir));

        // Ensure 'Hidden' directory exists
        Directory.CreateDirectory(HttpContext.Current.Server.MapPath(Website.HiddenDir));
        Directory.CreateDirectory(path);

        // Create or overwrite file
        var file = File.Create(Path.Combine(path, filename));

        file.Seek(0, SeekOrigin.Begin);
        data.CopyTo(file);
        file.Close();
    }

    public static bool DeleteHiddenFile(string filepath)
    {
        // We're intentionally causing exceptions for now
        filepath = Path.Combine(HttpContext.Current.Server.MapPath(Website.HiddenDir),
            filepath);

        // Delete file
        File.Delete(filepath);
        return true;
    }

    public static int ClientIntegerIP
    {
        get
        {
            int ipRaw = 0;
            IPAddress address;

            // Get current IP address as int, or 0 if unavailable
            if (IPAddress.TryParse(HttpContext.Current.Request.UserHostAddress, out address) &&
                address.AddressFamily == AddressFamily.InterNetwork)
            {
#pragma warning disable CS0618 // Only used with IPv4 connections, so okay
                ipRaw = (int)(uint)address.Address;
#pragma warning restore CS0618
            }
            return ipRaw;
        }
    }


    public static string MimeTypeFromFilename(string filename)
    {
        switch (Path.GetExtension(filename).ToLower())
        {
            case ".pdf":
                return System.Net.Mime.MediaTypeNames.Application.Pdf;
            case ".jpg":
            case ".jpeg":
                return System.Net.Mime.MediaTypeNames.Image.Jpeg;
            case ".png":
                return "image/png";
            case ".tiff":
                return System.Net.Mime.MediaTypeNames.Image.Tiff;
            case ".gif":
                return System.Net.Mime.MediaTypeNames.Image.Gif;
            case ".zip":
                return System.Net.Mime.MediaTypeNames.Application.Zip;
            default:
                return System.Net.Mime.MediaTypeNames.Application.Octet;
        }
    }


    public static T WithDatabase<T>(Func<WebMatrix.Data.Database, T> lambda)
    {
        using (var db = WebMatrix.Data.Database.Open(ClientHelper.CurrentClient.StringID)) {
            return lambda.Invoke(db);
        }
    }
    public static void WithDatabase(Action<WebMatrix.Data.Database> lambda)
    {
        using (var db = WebMatrix.Data.Database.Open(ClientHelper.CurrentClient.StringID)) {
            lambda.Invoke(db);
        }
    }
}
