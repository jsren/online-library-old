using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

public class Library
{
    public int ID { get; private set; }
    public string Name { get; private set; }

    public Library(int id, string name)
    {
        this.ID   = id;
        this.Name = name;
    }

    public override string ToString()
    {
        return Locale.Messages[this.Name];
    }
}

public static partial class Website
{
    public const int Library_Orchestra = 1;
    public const int Library_Choir     = 2;

    public const int Format_Original    = 0;
    public const int Format_PaperCopy   = 1;
    public const int Format_DigitalCopy = 2;

    public static readonly string DBName    = "musicDB";
    public static readonly string HiddenDir = "~/Hidden/";
    public static readonly string Webmaster = "jsrenwick3@gmail.com";
    public static readonly string GuestUUN  = "s0000000";

    public static readonly string UUNRegex = @"(^[se]\d{7}$)|([vV]?\d{8})";

    private static readonly Library[] libraries = new Library[] 
    { 
        new Library(Library_Orchestra, "Orchestra"),
        new Library(Library_Choir, "Choir") 
    };
    /// <summary>
    /// Get objects giving info on the available libraries.
    /// </summary>
    public static IEnumerable<Library> Libraries { get { return libraries; } }

    /// <summary>
    /// Gets the string name describing the format with
    /// the given ID.
    /// </summary>
    public static string GetLoanFormatName(int id)
    {
        switch (id)
        {
            case 0:  return Locale.Messages["Original"];
            case 1:  return Locale.Messages["PaperCopy"];
            case 2:  return Locale.Messages["Digital"];
            case 3:  return Locale.Messages["Recording"];
            default: return Locale.Messages["Other"];
        }
    }

    /// <summary>
    /// Gets the status string describing the loan status of the
    /// given loan record.
    /// </summary>
    public static string GetStatusText(dynamic loan)
    {
        if (loan.LoanStart.Date > DateTime.Today || !loan.Fulfilled)
        {
            if (!loan.Returned) return Locale.Messages["Preparing"];
            else                return Locale.Messages["Cancelled"];
        }
        if (loan.LoanEnd.Date >= DateTime.Today)
        {
            if (!loan.Returned) return Locale.Messages["Loaned"];
            else                return Locale.Messages["Returned"];
        }
        if (loan.Fees > 0)
        {
            if (!loan.Returned)            return Locale.Messages["LatewithFee"];
            if (loan.FeesPaid < loan.Fees) return Locale.Messages["FeesDue"];
            else                           return Locale.Messages["FeesPaid"];
        }
        else
        {
            if (!loan.Returned) return Locale.Messages["Overdue"];
            else                return Locale.Messages["Returned"];
        }
    }
}