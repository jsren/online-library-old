using System;
using System.Linq;
using System.Collections.Specialized;
using System.Collections.Generic;
using WebMatrix.Data;
using WebMatrix.WebData;
using System.Web;
using System.Web.WebPages;

/// <summary>
/// Summary description for UserHelper
/// </summary>
public static class UserHelper
{
    public static bool IsGuest()
    {
        return IsGuest(WebSecurity.CurrentUserName);
    }
    public static bool IsGuest(string studentID)
    {
        return studentID == Website.GuestUUN;
    }
    public static bool IsMember()
    {
        return IsMember(WebSecurity.CurrentUserName);
    }
    public static bool IsMember(string studentID)
    {
        // Open the database & look for a member
        var res = Website.WithDatabase((db) => db.Query(
            "SELECT * FROM Members WHERE UUN=@0", studentID));

        int count = res.Count();
        if (count == 0) return false;
        if (count == 1) return (bool)(res.First().IsMember);

        else throw new Exception("More than one member matches the given UUN");
    }

    public static dynamic GetMembers(bool showInactive = false)
    {
        return Website.WithDatabase((db) => db.Query(
            "SELECT * from Members WHERE UUN!='s0000000'" + (!showInactive ? " AND IsMember='1'" : " ")));
    }

    public static bool IsAdmin()
    {
        return IsAdmin(WebSecurity.CurrentUserName);
    }
    public static bool IsAdmin(string studentID)
    {
        return Website.WithDatabase((db) => (int)db.QueryValue(
            "SELECT COUNT(1) FROM Members WHERE UUN=@0 AND IsAdmin='1'", studentID) == 1);
    }

    public static bool IsMaster()
    {
        return IsMaster(WebSecurity.CurrentUserId);
    }
    public static bool IsMaster(int id)
    {
        return Website.WithDatabase((db) => (int)db.QueryValue(
            "SELECT COUNT(1) FROM [master].Masters WHERE [User]=@0", id) == 1);
    }

    public static Tuple<string, string> SplitFullName(string fullName)
    {
        fullName = fullName.Trim();

        // Split into first & last names
        string firstname = "", lastname = "";

        int index = fullName.LastIndexOf(' ');
        if (index != -1)
        {
            lastname = fullName.Substring(index + 1);
            firstname = fullName.Substring(0, index);
        }
        else lastname = fullName;

        return new Tuple<string, string>(firstname, lastname);
    }

    public static dynamic GetUser()
    {
        string user = String.IsNullOrEmpty(WebSecurity.CurrentUserName) ? 
            Website.GuestUUN : WebSecurity.CurrentUserName;

        // Open the database & look for the user
        return Website.WithDatabase((db) => db.QuerySingle(
            "SELECT * FROM Members WHERE UUN=@0", user));
    }
    public static dynamic GetUser(string studentID)
    {
        // Open the database & look for the user
        return Website.WithDatabase((db) => db.QuerySingle(
            "SELECT * FROM Members WHERE UUN=@0", studentID));
    }
    public static dynamic GetUser(string firstName, string lastName)
    {
        return Website.WithDatabase((db) => db.QuerySingle(
            "SELECT * FROM Members WHERE FirstName=@0 AND LastName=@1 AND IsMember='1'", firstName, lastName));
    }

    public static int ConfirmUser(string studentID, NameValueCollection form)
    {
        if (studentID == Website.GuestUUN) throw new Exception("Invalid operation on guest account.");

        string firstName = Website.Sanitise(form["firstName"]);
        string lastName  = Website.Sanitise(form["lastName"]);

        // Trim strings to match db requirement
        if (firstName.Length > 50) firstName = firstName.Remove(50);
        if (lastName.Length > 50)  lastName  = lastName.Remove(50);

        // Update member record
        return Website.WithDatabase((db) => db.Execute(
            "UPDATE Members SET FirstName=@0,LastName=@1,Email=@2 WHERE UUN=@3",
            firstName, lastName, form["email"], studentID));
    }

    public static int CountRequests()
    {
        var user = GetUser();

        if (!user.IsAdmin) throw new Exception("Administrative privileges requried for this operation");
        if (!user.IsOrchestra && !user.IsChoir) return 0;

        return Website.WithDatabase((db) =>
        {
            if (user.IsOrchestra && user.IsChoir) {
                return db.QueryValue("SELECT COUNT(*) FROM [New Loan Requests - Simple]");
            }
            else if (user.IsOrchestra) {
                return db.QueryValue("SELECT COUNT(*) FROM [New Loan Requests - Simple] WHERE [Libraries] & @0 != 0", Website.Library_Orchestra);
            }
            else if (user.IsChoir) {
                return db.QueryValue("SELECT COUNT(*) FROM [New Loan Requests - Simple] WHERE [Libraries] & @0 != 0", Website.Library_Choir);
            }
            else return 0;
        });
    }

    public static IEnumerable<dynamic> GetRequests()
    {
        var user = GetUser();

        if (!user.IsAdmin) throw new Exception("Administrative privileges requried for this operation");
        if (!user.IsOrchestra && !user.IsChoir) return new List<Object>();

        return Website.WithDatabase((db) =>
        { 
            if (user.IsOrchestra && user.IsChoir) {
                return db.Query("SELECT * FROM [New Loan Requests - Simple]");
            }
            else if (user.IsOrchestra) {
                return db.Query("SELECT * FROM [New Loan Requests - Simple] WHERE [Libraries] & @0 != 0", Website.Library_Orchestra);
            }
            else if (user.IsChoir) {
                return db.Query("SELECT * FROM [New Loan Requests - Simple] WHERE [Libraries] & @0 != 0", Website.Library_Choir);
            }
            // Return empty set
            else return db.Query("SELECT * FROM [New Loan Requests - Simple] WHERE 1 = 0");
        });
    }

    public static dynamic GetCurrentLoans(string studentID)
    {
        // Open the database & update the user
        return Website.WithDatabase((db) =>
        {
            // Perform the query
            var Loans = Website.ExpandoFromTable(db.Query("SELECT * FROM Loans WHERE Member=@0 AND Returned='0'", studentID));

            // Query for relations
            foreach (var row in Loans)
            {
                row.Part = Website.ExpandoFromRow(db.QuerySingle("SELECT * FROM Parts WHERE ID=@0", row.Part));
                row.Part.Piece = db.QuerySingle("SELECT * FROM Pieces WHERE ID=@0", row.Part.Piece);
            }
            return Loans;
        });
    }    

    public static dynamic GetPreviousLoans(string studentID)
    {
        // Open the database & update the user
        return Website.WithDatabase((db) =>
        {
            // Perform the query
            var q = db.Query("SELECT * FROM Loans WHERE Member=@0 AND Returned='1'", studentID);

            var Loans = Website.ExpandoFromTable(q);

            // Query for relations
            foreach (var row in Loans)
            {
                row.Part = Website.ExpandoFromRow(db.QuerySingle("SELECT * FROM Parts WHERE ID=@0", row.Part));
                row.Part.Piece = db.QuerySingle("SELECT * FROM Pieces WHERE ID=@0", row.Part.Piece);
            }
            return Loans;
        });
    }

    public static int AddMember(string uun, string fname, string lname, string email,
                bool member, bool orchestra, bool choir, bool admin)
    {
        if (email != null) email = email.Replace(" ","");
        if (String.Empty == email) email = null;

        return Website.WithDatabase((db) => db.Execute(
            @"INSERT INTO Members (UUN, FirstName, LastName, Email, IsMember, 
            JoinDate, IsOrchestra, IsChoir, IsAdmin) VALUES (@0,@1,@2,@3,@4,GETDATE(),@5,@6,@7)",
            
            uun, fname, lname, email, (member?"1":"0"),(orchestra?"1":"0"),(choir?"1":"0"),(admin?"1":"0")
        ));
    }

    public static int SetMemberFlags(string uun, bool isActive, bool isOrchestra, bool isChoir, bool isAdmin)
    {
        return Website.WithDatabase((db) => db.Execute(
            "UPDATE Members SET IsMember=@0, IsOrchestra=@1, IsChoir=@2, IsAdmin=@3 WHERE UUN=@4",
            (isActive ? "1" : "0"), (isOrchestra ? "1" : "0"), (isChoir ? "1" : "0"), (isAdmin ? "1" : "0"), uun));
    }


    public class MemberFullNameValidator : RequestFieldValidatorBase
    {
        protected bool optional;

        public MemberFullNameValidator(bool optional = false) 
            : base("The given full name is either not present " 
            + "or is ambiguous. Use the student number.")
        {
            this.optional = optional;
        }

        protected override bool IsValid(HttpContextBase httpContext, string value)
        {
            // Get field value
            if (optional && String.IsNullOrWhiteSpace(value)) return true;
            var name = UserHelper.SplitFullName(value);

            // Check if present in database
            try { UserHelper.GetUser(name.Item1, name.Item2).UUN.ToString(); return true; }
            catch { return false; }
        }
    }

    public class MemberUUNValidator : RequestFieldValidatorBase
    {
        protected bool optional;

        public MemberUUNValidator(bool optional = false) 
            : base("A member with the given student number could not be found.")
        {
            this.optional = optional;
        }

        protected override bool IsValid(HttpContextBase httpContext, string value)
        {
            if (optional && String.IsNullOrWhiteSpace(value)) return true;

            // Check if present in database
            try { UserHelper.GetUser(value).UUN.ToString(); return true; }
            catch { return false; }
        }
    }

    public static void ResetPassword(string uun)
    {
        Website.WithDatabase((db) =>
        {
            // Delete the user entry - it will be re-created 
            db.Execute(@"
                BEGIN TRANSACTION;
                    DECLARE @uid int;
                    SET @uid = (SELECT UserID FROM UserProfile WHERE Email=@0);
                    DELETE FROM UserProfile WHERE UserID=@uid;
                    DELETE FROM [webpages_Membership] WHERE UserID=@uid;
                COMMIT;
            ", uun);
        });
    }
}
