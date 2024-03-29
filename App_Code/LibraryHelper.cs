﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using WebMatrix.Data;
using WebMatrix.WebData;

/// <summary>
/// Summary description for LoanHelper
/// </summary>
public static class LibraryHelper
{
    public static IEnumerable<dynamic> GetInstrumentList()
    {
        return Website.WithDatabase((db) => db.Query(
            "SELECT DISTINCT Instrument FROM Parts WHERE Parts.IsPresent='1'"));
    }

    public static IEnumerable<dynamic> GetPieces()
    {
        var q = Website.WithDatabase((db) => db.Query(
            "SELECT * FROM Pieces WHERE IsPresent='1' ORDER BY NextPerformance DESC, Title"));

        dynamic user = UserHelper.GetUser();

        // Filter pieces by whether the user is a member of that library
        return q.Where((p) => (Website.IsOrchestra(p.Libraries) && user.IsOrchestra)
            || (Website.IsChoir(p.Libraries) && user.IsChoir));
    }
    public static IEnumerable<dynamic> GetLoanablePieces()
    {
        var q = Website.WithDatabase((db) => db.Query(
            "SELECT * FROM Pieces WHERE IsPresent='1' AND CanLendParts='1' ORDER BY Title"));

        dynamic user = UserHelper.GetUser();

        // Filter pieces by whether the user is a member of that library
        return q.Where((p) => (Website.IsOrchestra(p.Libraries) && user.IsOrchestra)
            || (Website.IsChoir(p.Libraries) && user.IsChoir));
    }

    public static IEnumerable<dynamic> GetCurrentLoans(string partID)
    {
        return Website.WithDatabase((db) => db.Query(
            "SELECT * FROM Loans WHERE Returned='0' AND Part=@0", partID));
    }

    public static IEnumerable<dynamic> GetAllCurrentLoans()
    {
        var user = UserHelper.GetUser();

        if (!user.IsAdmin) throw new Exception("Administrative privileges requried for this operation");
        if (!user.IsOrchestra && !user.IsChoir) return new List<Object>();

        return Website.WithDatabase((db) =>
        { 
            if (user.IsOrchestra && user.IsChoir) {
                return db.Query("SELECT * FROM [Loans - Simple] ORDER BY [LoanStart] DESC");
            }
            else if (user.IsOrchestra) {
                return db.Query("SELECT * FROM [Loans - Simple] WHERE [Libraries] & @0 != 0 ORDER BY [LoanStart] DESC", Website.Library_Orchestra);
            }
            else if (user.IsChoir) {
                return db.Query("SELECT * FROM [Loans - Simple] WHERE [Libraries] & @0 != 0 ORDER BY [LoanStart] DESC", Website.Library_Choir);
            }
            // Return empty set
            return db.Query("SELECT * FROM [Loans - Simple] WHERE 1 = 0");
        });
    }

    public static bool IsRenting(IEnumerable<dynamic> loans)
    {
        return loans.Where((l) => l.Member == WebSecurity.CurrentUserName).Any();
    }

    public static IEnumerable<dynamic> GetParts(string pieceID, string orderBy="Instrument")
    {
        int.Parse(pieceID); // Validate input

        return Website.WithDatabase((db) =>
        {
            var Parts = Website.ExpandoFromTable(db.Query(
                "SELECT * FROM Parts WHERE Piece=@0 AND IsPresent='1' ORDER BY " + orderBy, pieceID));

            foreach (var part in Parts) {
                part.Piece = db.QuerySingle("SELECT * FROM Pieces WHERE ID=@0", part.Piece);
            }
            return Parts;
        });
    }

    public static bool CanLoan(dynamic part)
    {
        var loans = GetCurrentLoans(part.ID.ToString());

        int libraries = (UserHelper.GetUser().IsOrchestra ? Website.Library_Orchestra : 0) |
            (UserHelper.GetUser().IsChoir ? Website.Library_Choir : 0);

        // Check if user's available libraries contain the part
        if ((libraries & part.Piece.Libraries) == 0) {
            return false;
        }
        // Check if the user already has the part on loan and it's not public domain
        if (part.Count != null && IsRenting(loans)) return false;

        // Check if there are any copies left (null indicates infinite)
        return part.Count == null || (part.Count - loans.Count) > 0;
    }

    public static string GetDownloadPath(string partID)
    {
        int.Parse(partID); // Validate input

        return Website.WithDatabase((db) => {

            var tokens = db.Query("SELECT * FROM DlTokens WHERE Part=@0 AND Member=@1",
                partID, WebSecurity.CurrentUserName);

            if (!tokens.Any()) {
                throw new Exception("No valid download token for this part");
            }
            var token = tokens.First();
            db.Execute("DELETE FROM DlTokens WHERE Token=@0", token.Token);

            return HttpContext.Current.Server.MapPath(System.IO.Path.Combine(Website.HiddenDir, 
                (string)db.QueryValue("SELECT DigitalCopyPath FROM Parts WHERE ID=@0", partID)));
        });
    }

    public static int CreateLoan(string partID, string format, DateTime returnDate)
    {
        return CreateLoan(WebSecurity.CurrentUserName, partID, format, returnDate);
    }
    public static int CreateLoan(string member, string partID, string format, DateTime returnDate)
    {
        // Validate input
        int.Parse(partID); int.Parse(format); member = Website.Sanitise(member);

        DateTime ret = returnDate;

        return Website.WithDatabase((db) => {

            char fulfilled = '0';

            if (format == Website.Format_DigitalCopy.ToString())
            {
                fulfilled = '1'; // Digital copies are automatically fulfilled

                // Add digital download token for that type and user
                int i = db.Execute("INSERT INTO DlTokens (Member, Part) VALUES (@0, @1)", member, partID);
            }

            return db.Execute(String.Format
            (
                @"INSERT INTO Loans (Part,Member,Format,LoanStart,LoanEnd,Fulfilled)
                        VALUES (@0,@1,@2,GETDATE(),'{0}-{1}-{2}',@3)", ret.Year, ret.Month, ret.Day), 

                partID, member, format, fulfilled
            );
        });
    }

    public static int CancelRequest(int requestID)
    {
        return Website.WithDatabase((db) => 
            db.Execute("DELETE FROM Loans WHERE ID=@0 AND Fulfilled='0'", requestID));
    }

    public static int AddPiece(string title, string author, int library, bool owned, 
        string arranger, string opus, string edition, DateTime? loanEnd, DateTime? performance,
        string notes)
    {
        // Pre-format dates because Database.Execute is pretty bad
        string dloan = null, dperf = null;

        if (loanEnd != null) {
            dloan = String.Format("'{0}-{1}-{2}'", loanEnd.Value.Year, loanEnd.Value.Month, loanEnd.Value.Day);
        }
        else if (performance != null) {
            dperf = String.Format("'{0}-{1}-{2}'", performance.Value.Year, performance.Value.Month,
                performance.Value.Day);
        }

        if (String.IsNullOrWhiteSpace(arranger)) arranger = null;
        if (String.IsNullOrWhiteSpace(opus))     opus     = null;
        if (String.IsNullOrWhiteSpace(edition))  edition  = null;
        if (String.IsNullOrWhiteSpace(notes))    notes    = null;

        
        return Website.WithDatabase((db) => (int)db.QueryValue("INSERT INTO Pieces "+
            "(Title,Author,ArrangedBy,Opus,Edition,Libraries,IsPresent,IsOwned,LoanEnd,NextPerformance,NotesUponLoan) "+
            "OUTPUT INSERTED.ID "+
            "VALUES (@0,@1,@2,@3,@4,@5,1,@6,@7,@8,@9)",
            
            title, author, arranger, opus, edition, library.ToString(), (owned?"1":"0"), dloan, dperf, notes));
    }

    public static int SetPieceFlags(string id, bool loanable, bool present)
    {
        return Website.WithDatabase((db) => db.Execute(
            "UPDATE Pieces SET CanLendParts=@0, IsPresent=@1 WHERE ID=@2",
            (loanable ? "1" : "0"), (present ? "1" : "0"), id));
    }

    public static int EditPart(string id, bool canLendOrig, bool canLendCopy, bool canLendDigital, 
        float deposit)
    {
        return Website.WithDatabase((db) => db.Execute("UPDATE Parts SET CanLendOriginal=@0, " +
            "CanLendCopy=@1, CanLendDigital=@2, Deposit=@3 WHERE ID=@4",

            (canLendOrig ? "1" : "0"), (canLendCopy ? "1" : "0"), (canLendDigital ? "1" : "0"), deposit, id));
    }

    public static int HidePart(string partID)
    {
        return Website.WithDatabase((db) => db.Execute("UPDATE Parts SET IsPresent='0' WHERE ID=@0", partID));
    }

    public static string GetPartFilePath(string partID) {
        return Website.WithDatabase((db) => db.QueryValue("SELECT DigitalCopyPath FROM Parts WHERE ID=@0", partID) as string);
    }

    public static bool ReplacePartFile(string partID, string filepath)
    {
        return Website.WithDatabase((db) => { 
            // Get previous filename
            string previous = db.QueryValue(
                "SELECT DigitalCopyPath FROM Parts WHERE ID=@0", partID) as string;

            // Change filename
            var count = db.Execute("UPDATE Parts SET DigitalCopyPath=@0 WHERE ID=@1", filepath, partID);
            if (count == 0) return false;

            // Now remove old file (if not the same as new one)
            if (!String.IsNullOrWhiteSpace(previous) && 
                    (String.IsNullOrWhiteSpace(filepath) ||  
                        System.IO.Path.GetFullPath(previous) != System.IO.Path.GetFullPath(filepath)))
            {
                return Website.DeleteHiddenFile(previous);
            }
            return true;
        });
    }

    public static int AddPart(int pieceID, string instrument, string designation, int? count, bool original,
        bool paper, bool digital, string filepath, float deposit)
    {
        if (String.IsNullOrWhiteSpace(instrument)) instrument = null;
        if (String.IsNullOrWhiteSpace(designation)) designation = null;

        return Website.WithDatabase((db) =>
            db.Execute("INSERT INTO Parts " +
                "(Piece,Instrument,Designation,Count,CanLendOriginal,CanLendCopy,CanLendDigital,DigitalCopyPath,Deposit) " +
                "VALUES (@0,@1,@2,@3,@4,@5,@6,@7,@8)",

                pieceID, instrument, designation, count, original ? "1" : "0", paper ? "1" : "0", digital ? "1" : "0",
                filepath, deposit)
        );
    }

    public static int AddGroup(string name, string description, string creator, bool active, bool canSuggest)
    {
        if (String.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Invalid argument: group name is empty");
        if (String.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Invalid argument: group description is empty");

        return Website.WithDatabase((db) => 
            db.Execute("INSERT INTO Groups (Name, Description, CreatorUUN, Active, CanSuggest) VALUES (@0, @1, @2, @3, @4)",
                name, description, creator, active ? "1" : "0", canSuggest ? "1" : "0")
        );
    }

    public static IEnumerable<dynamic> GetGroups()
    {
        return Website.WithDatabase((db) =>
        {
            var rows = db.Query("SELECT * FROM Groups WHERE IsPresent='1'");
            // Join with Members to get creator
            return Website.ExpandoFromTable(rows).Select((r) => {
                r.Creator = Website.ExpandoFromRow(db.QuerySingle("SELECT * FROM Members WHERE UUN=@0", r.CreatorUUN));
                return r;
            });
        });
    }

    public static int CountGroupMembers(int groupID)
    {
        return Website.WithDatabase((db) => {
            return (int)db.QueryValue("SELECT COUNT(1) FROM GroupMembers WHERE [Group]=@0", groupID);
        });
    }

    public static int SetGroupFlags(string id, bool active, bool canSuggest, bool present)
    {
        return Website.WithDatabase((db) => {
            return db.Execute("UPDATE Groups SET Active=@0, CanSuggest=@1, IsPresent=@2 WHERE ID=@3",
                (active ? "1" : "0"), (canSuggest ? "1" : "0"),(present ? "1" : "0"), id);
        });
    }

    public static int HideGroup(string id)
    {
        return Website.WithDatabase((db) => {
            return db.Execute("UPDATE Groups SET IsPresent='0' WHERE ID=@0", id);
        });
    }

    public static int AddMemberToGroup(int groupID, string memberUUN)
    {
        return Website.WithDatabase((db) => {
            return (int)db.Execute("INSERT INTO GroupMembers ([Group], Member) VALUES (@0, @1)", groupID, memberUUN);
        });
    }

    public static int RemoveMemberFromGroup(int groupID, string memberUUN)
    {
        return Website.WithDatabase((db) => {
            return (int)db.Execute("DELETE FROM GroupMembers WHERE [Group]=@0 AND Member=@1", groupID, memberUUN);
        });
    }

    public static IEnumerable<dynamic> GetGroupMembers(int groupID)
    {
        return Website.WithDatabase((db) => {
            return db.Query("SELECT M.* FROM GroupMembers GM JOIN Members M ON GM.Member=M.UUN WHERE [Group]=@0", groupID);
        });
    }
}
