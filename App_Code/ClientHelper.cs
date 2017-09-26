using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using WebMatrix.WebData;


public static class ClientHelper
{
    public static readonly int DefaultClientID = 0;

    /// <summary>
    /// Gets the ID of the current library client
    /// </summary>
    public static int ClientID
    {
        get
        {
            // Cache client ID for session
            if (HttpContext.Current.Session["clientID"] == null)
            {
                // Query DB or use default if not a known user
                using (var db = WebMatrix.Data.Database.Open("default"))
                {
                    if (WebSecurity.HasUserId && !string.IsNullOrWhiteSpace(WebSecurity.CurrentUserName))
                    {
                        var v = db.QueryValue("SELECT [Client] FROM [master].ClientMembers WHERE [Member]=@0", WebSecurity.CurrentUserName);
                        HttpContext.Current.Session["clientID"] = (int)v;

                        // Clear any existing client
                        try { HttpContext.Current.Items.Remove("client"); } catch { }
                    }
                    else return DefaultClientID;
                }
            }
            // Return cached value
            return (int)HttpContext.Current.Session["clientID"];
        }
    }

    /// <summary>
    /// Gets the record for the current library client.
    /// </summary>
    public static dynamic CurrentClient
    {
        get
        {
            // Cache client for request
            if (!HttpContext.Current.Items.Contains("client"))
            {
                using (var db = WebMatrix.Data.Database.Open("default"))
                {
                    var client = db.QuerySingle(
                        "SELECT * FROM [master].Clients WHERE ID=@0", ClientID);
                    HttpContext.Current.Items["client"] = client;
                }
            }
            // Return cached client record
            return HttpContext.Current.Items["client"];
        }
    }

    public static string GetDirectory()
    {
        return "/Content/clients/" + CurrentClient.StringID + "/";
    }

    public static string GetRealDirectory()
    {
        return HttpContext.Current.Server.MapPath(GetDirectory());
    }
    
    public static string GetFavicon() {
        return GetDirectory() + "favicon.ico";
    }

    public static string GetCSS() {
        return GetDirectory() + "style.css";
    }

    public static string GetBigImage()
    {
        foreach (string file in System.IO.Directory.EnumerateFiles(GetRealDirectory())) {
            string name = System.IO.Path.GetFileName(file);
            if (name.StartsWith("bigimg.")) return GetDirectory() + name;
        }
        return String.Empty;
    }

    public static string GetSmallImage()
    {
        foreach (string file in System.IO.Directory.EnumerateFiles(GetRealDirectory())) {
            string name = System.IO.Path.GetFileName(file);
            if (name.StartsWith("smallimg.")) return GetDirectory() + name;
        }
        return String.Empty;
    }


    public static void AddClient(string name, string shortname, string password, string website)
    {
        // Check that shortname is valid
        string stringID = shortname.Trim().ToLower();

        if (!stringID.All((c) => char.IsLetterOrDigit(c))) {
            throw new ArgumentException("Invalid shortname: must only be alphannumeric.");
        }

        // Add user to DB (not to web.config)
        AddClientUser(stringID, password);

        // Create data directory
        string datadir = HttpContext.Current.Server.MapPath("/Content/clients/" + stringID + "/");
        System.IO.Directory.CreateDirectory(datadir);

        // Null website if not present
        if (string.IsNullOrWhiteSpace(website)) website = null;

        // Add reecord to Client table
        using (var db = WebMatrix.Data.Database.Open(Website.DBName))
        {
            db.Execute("INSERT INTO [master].Clients (Name, Shortname, StringID, Website)" +
                "VALUES (@0, @1, @2, @3)", name, shortname, stringID, website);
        }
    }

    public static void AddClientUser(string stringID, string password)
    {
        // Perform HTTPS check
        //if (!HttpContext.Current.Request.IsSecureConnection)
        //    throw new InvalidOperationException("AddClientUser must be conducted over HTTPS.");

        // Add login to master database
        //using (var db = WebMatrix.Data.Database.Open("master"))
        //{
        //    db.Execute("CREATE LOGIN @0 WITH PASSWORD = '@1';".Replace("@0", stringID).Replace("@1", password));
        //}

        // Add user to library database
        using (var db = WebMatrix.Data.Database.Open(Website.DBName))
        {
            db.Execute(@"
                IF (NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '@0')) 
                BEGIN
                    EXEC ('CREATE SCHEMA [@0]')
                END;

                IF (NOT EXISTS (SELECT * FROM sys.sysusers WHERE name = '@0'))
                BEGIN
                CREATE USER [@0] FOR LOGIN [@0]  
                    WITH DEFAULT_SCHEMA = [@0];  
                END"
            .Replace("@0", stringID));

            db.Execute("GRANT SELECT ON SCHEMA :: [master] TO [@0];".Replace("@0", stringID));
            ActivateClient(stringID);
        }
    }

    public static void RegisterMember(string clientID, string username, WebMatrix.Data.Database db)
    {
        db.Execute(@"INSERT INTO [master].ClientMembers VALUES (@0, @1)", clientID, username);
    }
    public static void UnregisterMember(string clientID, string username, WebMatrix.Data.Database db)
    {
        db.Execute(@"DELETE FROM [master].ClientMembers WHERE [Client]=@0 AND [Member]=@1", clientID, username);
    }

    public static IEnumerable<dynamic> ClientsForMember(string username)
    {
        using (var db = WebMatrix.Data.Database.Open(Website.DBName))
        {
            return db.Query(@"SELECT [Client] FROM [master].ClientMembers WHERE [Member]=@0", username);
        }
    }

    public static void ActivateClient(string stringID)
    {
        using (var db = WebMatrix.Data.Database.Open(Website.DBName))
        {
            db.Execute("GRANT DELETE, INSERT, SELECT, UPDATE ON SCHEMA :: [@0] TO [@0]".Replace("@0", stringID));
            db.Execute("GRANT DELETE, INSERT, SELECT, UPDATE ON [master].[ClientMembers] TO [@0]".Replace("@0", stringID));
        }
    }

    public static void DeactivateClient(string stringID)
    {
        using (var db = WebMatrix.Data.Database.Open(Website.DBName))
        {
            db.Execute("REVOKE DELETE, INSERT, SELECT, UPDATE ON SCHEMA :: [@0] TO [@0]".Replace("@0", stringID));
            db.Execute("REVOKE DELETE, INSERT, SELECT, UPDATE ON [master].[ClientMembers] TO [@0]".Replace("@0", stringID));
        }
    }

    public static IEnumerable<dynamic> GetAllClients()
    {
        return Website.WithDatabase((db) => db.Query("SELECT * FROM [master].Clients"));
    }
}
