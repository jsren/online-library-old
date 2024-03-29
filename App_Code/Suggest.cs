﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Web;
using WebMatrix.Data;

public static class Suggestions
{
    public static bool ToggleEndorse(int suggestionID)
    {
        var user  = UserHelper.GetUser();
        int ipRaw = Website.ClientIntegerIP;

        return Website.WithDatabase((db) =>
        { 
            if (UserHelper.IsGuest(user.UUN))
            {
                // Check if existing for this IP as we're guest
                int existing = db.QueryValue("SELECT COUNT(*) FROM [Endorsements2] WHERE UUN=@0 AND IP=@1 AND Suggestion=@2", 
                                                user.UUN, ipRaw, suggestionID);
                // Add or delete to toggle
                if (existing != 0) {
                    db.Execute("DELETE FROM [Endorsements2] WHERE UUN=@0 AND IP=@1 AND Suggestion=@2", user.UUN, ipRaw, suggestionID);
                }
                else db.Execute("INSERT INTO [Endorsements2] (IP, Suggestion) VALUES (@0, @1)", ipRaw, suggestionID);
            }
            else
            {
                // Check if existing for this user
                int existing = db.QueryValue("SELECT COUNT(*) FROM [Endorsements2] WHERE UUN=@0 AND Suggestion=@1",
                                                user.UUN, suggestionID);
                // Add or delete to toggle
                if (existing != 0) {
                    db.Execute("DELETE FROM [Endorsements2] WHERE UUN=@0 AND Suggestion=@1", user.UUN, suggestionID);
                }
                else db.Execute("INSERT INTO [Endorsements2] (UUN, Suggestion) VALUES (@0, @1)", user.UUN, suggestionID);
            }
            return true; 
        });
    }

    public static bool Remove(int suggestionID)
    {
        // Check if current user is admin - currently only Admins can remove suggestions
        if (!UserHelper.GetUser().IsAdmin) return false;

        return Website.WithDatabase((db) =>
        {
            // Delete all associated endorsements
            db.Execute("DELETE FROM [Endorsements2] WHERE Suggestion=@0", suggestionID);

            // Delete suggestion
            return db.Execute("DELETE FROM Suggestions WHERE Suggestion=@0", suggestionID) == 1;
        });
    }

    public static int Create(string title, int groupID)
    {
        var user = UserHelper.GetUser();
        var res = Website.WithDatabase((db) => db.QueryValue(
            "INSERT INTO Suggestions(Title,[Group],CreatorUUN) OUTPUT INSERTED.Suggestion VALUES (@0, @1, @2)",
            title.Length <= 120 ? title : title.Substring(0, 120), groupID, user.UUN));

        // Endorse your suggestion
        ToggleEndorse((int)res);
        return (int)res;
    }

    public static IEnumerable<dynamic> GetAll()
    {
        return Website.WithDatabase((db) =>
        {
            // Order by votes
            var rows = db.Query(@"SELECT Suggestions.Suggestion, Title, [Group] FROM Suggestions 
                             JOIN Endorsements2 ON Suggestions.Suggestion=Endorsements2.Suggestion
                             WHERE Suggestions.Active='1'
                             GROUP BY Suggestions.Suggestion, Title, [Group]
                             ORDER BY count(Suggestions.Suggestion) DESC");

            return Website.ExpandoFromTable(rows).Select((r) => {
                r.Group = db.QuerySingle("SELECT * FROM Groups WHERE ID=@0", r.Group); return r;
            });
        });
    }

    public static IEnumerable<int> GetEndorsed()
    {
        return GetEndorsed(UserHelper.GetUser().UUN);
    }
    public static IEnumerable<int> GetEndorsed(string userID)
    {
        int ipRaw = Website.ClientIntegerIP;

        var res = Website.WithDatabase((db) =>
        {
            if (UserHelper.IsGuest(userID))
            {
                return db.Query(@"SELECT DISTINCT Suggestions.Suggestion FROM Suggestions 
                             JOIN Endorsements2 ON Suggestions.Suggestion=Endorsements2.Suggestion
                             WHERE UUN=@0 AND Endorsements2.IP=@1", userID, ipRaw);
            }
            else
            {
                return db.Query(@"SELECT DISTINCT Suggestions.Suggestion FROM Suggestions 
                             JOIN Endorsements2 ON Suggestions.Suggestion=Endorsements2.Suggestion
                             WHERE UUN=@0", userID);
            }
        });
        
        // Convert to int array
        int[] output = new int[res.Count()];

        int i = 0;
        foreach (var entry in res) {
            output[i++] = entry.Suggestion;
        }
        return output;
    }
}
