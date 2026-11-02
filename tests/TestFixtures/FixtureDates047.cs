using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;

// Test-only preparation of a disposable raw seed copy, before constructing Store.
// Never call this on a user database or after a test has created its own dated shows.
public static class FixtureDates047
{
    public static void PrepareSeedCopy(string copiedSeedPath)
    {
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder
        {DataSource=copiedSeedPath,Mode=SqliteOpenMode.ReadWrite,Pooling=false}.ToString());
        connection.Open();
        var now=DateTime.Now;
        var past=new List<(long Id,DateTime Date)>();
        using(var read=connection.CreateCommand())
        {
            read.CommandText="SELECT id,starts_at FROM event";
            using var rows=read.ExecuteReader();
            while(rows.Read())
            {
                var date=DateTime.Parse(rows.GetString(1),CultureInfo.InvariantCulture);
                if(date<now)past.Add((rows.GetInt64(0),date));
            }
        }
        if(past.Count==0)return;
        // The imported seed spans 1807–2025. One common shift keeps its ordering,
        // spacing and distinct event slots intact, unlike normalizing each year separately.
        // Multiples of four also retain ordinary leap-day fixtures.
        var years=Math.Max(4,((now.Year+2-past.Min(x=>x.Date.Year)+3)/4)*4);
        while(past.Any(x=>x.Date.AddYears(years).Day!=x.Date.Day))years+=4;
        using var transaction=connection.BeginTransaction();
        foreach(var item in past.OrderByDescending(x=>x.Date))
        {
            var date=item.Date.AddYears(years);
            using var update=connection.CreateCommand();update.Transaction=transaction;
            update.CommandText="UPDATE event SET starts_at=$start,legacy_date=$display WHERE id=$id";
            update.Parameters.AddWithValue("$start",date.ToString("yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture));
            update.Parameters.AddWithValue("$display",date.ToString("dd.MM.yyyy HH:mm:ss",CultureInfo.InvariantCulture));
            update.Parameters.AddWithValue("$id",item.Id);update.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
