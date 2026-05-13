#pragma warning disable 1591, CA1304
using System.Collections.Generic;
using System.IO;
using SQLitePCL;
using SQLitePCL.pretty;

namespace Jellyfin.Plugin.Newsletters.Shared.Database;

public class SqLiteDatabase
{
    private readonly string dbFilePath;
    private readonly string dbLockPath;
    private readonly Logger logger;
    private SQLiteDatabaseConnection? db;

    public SqLiteDatabase()
    {
        logger = new Logger();
        var config = Plugin.Instance!.Configuration;
        SQLite3.EnableSharedCache = false;

        _ = raw.sqlite3_config(raw.SQLITE_CONFIG_MEMSTATUS, 0);

        _ = raw.sqlite3_config(raw.SQLITE_CONFIG_MULTITHREAD, 1);

        _ = raw.sqlite3_enable_shared_cache(1);

        dbFilePath = config.DataPath + "/newsletters.db"; // get directory from config
        dbLockPath = dbFilePath + ".lock";
    }

    public void InitDatabase()
    {
        if (!File.Exists(dbLockPath)) // Database is not locked
        {
            CreateConnection();
            if (CheckTables())
            {
                logger.Info("Database not initialized.  Creating tables and migrating any existing or legacy data...");
                CreateTables();
                MigrateTables();
                InitializeEndEpisode();
                logger.Info("Done database init...");
            }
            else
            {
                logger.Debug("Database already initialized...");
            }

            CloseConnection();
        }
        else
        {
            logger.Error("Database lock file shows database is in use: " + dbLockPath);
        }
    }

    public void CreateConnection()
    {
        if (!File.Exists(dbLockPath)) // Database is not locked
        {
            logger.Info("Opening database connection...");
            logger.Debug("Opening Database: " + dbFilePath);
            db = SQLite3.Open(dbFilePath);
            File.WriteAllText(dbLockPath, string.Empty);
        }
        else
        {
            logger.Error("Database lock file shows database is in use: " + dbLockPath);
        }
    }

    private bool CheckTables()
    {
        List<string> nlpColumns =
            ["Filename", "Title", "Album", "Season", "Episode", "EndEpisode", "Overview", "ItemID", "PosterPath", "Type", "Emailed"];
        List<string> prColumns = 
            ["ID", "LastRun"];
        foreach (var row in Query("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='NewsletterData';"))
        {
            logger.Debug($"nlpPresent:: {row[0].ToString()}");
            if (int.TryParse(row[0].ToString(), out var x) && x > 0)
            {
                foreach (var column in Query("SELECT name FROM pragma_table_info('NewsletterData');"))
                {
                    logger.Debug($"column:: {column[0]}");
                    var isColumnPresent = nlpColumns.Contains(column[0].ToString());
                    if (isColumnPresent)
                    {
                        logger.Debug($"Column {column} is present.");
                    }
                    else
                    {
                        return true;
                    }   
                }
            }
            else if (x == 0)
            {
                return true;
            }
        }

        foreach (var row in Query("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='PreviousRun';"))
        {
            logger.Debug($"prPresent:: {row[0].ToString()}");
            if (int.TryParse(row[0].ToString(), out var x) && x > 0)
            {
                foreach (var column in Query("SELECT name FROM pragma_table_info('PreviousRun');"))
                {
                    logger.Debug($"column:: {column[0]}");
                    var isColumnPresent = prColumns.Contains(column[0].ToString());
                    if (isColumnPresent)
                    {
                        logger.Debug($"Column {column} is present.");
                    }
                    else
                    {
                        return true;
                    }   
                }
            }
            else if (x == 0)
            {
                return true;
            }
        }

        foreach (var row in Query("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND (name='ArchiveData' OR name='CurrRunData' OR name='CurrNewsletterData');"))
        {
            logger.Debug($"legacyPresent:: {row[0].ToString()}");
            if (int.TryParse(row[0].ToString(), out var x) && x > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void CreateTables()
    {
        ExecuteSql("CREATE TABLE IF NOT EXISTS NewsletterData (" +
                "Filename TEXT NOT NULL," +
                "Title TEXT," +
                "Album TEXT," +
                "Season INT," +
                "Episode INT," +
                "EndEpisode INT," +
                "Overview TEXT," +
                "ItemID TEXT," +
                "PosterPath TEXT," +
                "Type TEXT," +
                "Emailed INT," +
                "PRIMARY KEY (Filename));");
        ExecuteSql("CREATE TABLE IF NOT EXISTS PreviousRun (" +
                "ID INTEGER NOT NULL," +
                "LastRun TEXT," +
                "PRIMARY KEY (ID));");
        ExecuteSql("CREATE TRIGGER IF NOT EXISTS PreviousRunNoInsert " + 
                "BEFORE INSERT ON PreviousRun " +
                "WHEN (SELECT COUNT(*) FROM PreviousRun) >= 1 " +
                "BEGIN " +
                "SELECT RAISE(FAIL, 'Only one row allowed!'); " +
                "END;");
        // Initializes table with default value.  Skips if row is already present.
        try
        {
            ExecuteSql("INSERT OR IGNORE INTO PreviousRun (" +
                "ID,LastRun) " +
                "VALUES (0,'12/30/2018 00:00:00 AM');");
        }
        catch
        {
            logger.Debug("PreviousRun already populated.");
        }

        // Adds new columns for existing tables
        logger.Info($"Altering DB table: NewsletterData");
        // <TABLE_NAME, DATA_TYPE>
        Dictionary<string, string> new_cols = new Dictionary<string, string>();
        new_cols.Add("EndEpisode", "INT");

        foreach (KeyValuePair<string, string> col in new_cols)
        {
            try
            {
                logger.Debug($"Adding Table Columns for DB updates...");
                ExecuteSql($"ALTER TABLE NewsletterData ADD COLUMN {col.Key} {col.Value};");
            }
            catch (SQLiteException sle)
            {
                logger.Debug(sle);
            }
        }
    }
    
    private void MigrateTables()
    {
        try
        {
            ExecuteSql("INSERT INTO NewsletterData (" +
                            "Filename," +
                            "Title," +
                            "Season," +
                            "Episode," +
                            "Overview," +
                            "ItemID," +
                            "PosterPath," +
                            "Type) " + 
                       "SELECT " +
                            "Filename," +
                            "Title," +
                            "Season," +
                            "Episode," +
                            "SeriesOverview," +
                            "ItemID," +
                            "PosterPath," +
                            "Type " +
                            "FROM ArchiveData;");
            ExecuteSql("DROP TABLE IF EXISTS CurrRunData");
            ExecuteSql("DROP TABLE IF EXISTS CurrNewsletterData");
            ExecuteSql("DROP TABLE IF EXISTS ArchiveData");
            logger.Debug("Legacy tables successfully migrated.");
        }
        catch
        {
            logger.Debug("Legacy tables aren't present.");
        }
    }

    private void InitializeEndEpisode()
    {
        try
        {
            ExecuteSql("UPDATE NewsletterData SET EndEpisode = 0 WHERE EndEpisode is null;");
        }
        catch
        {
            logger.Debug("EndEpisode column not present.");
        }
    }

    public IEnumerable<IReadOnlyList<ResultSetValue>> Query(string query)
    {
        logger.Debug("Running Query: " + query);
        return db.Query(query);
    }

    public void ExecuteSql(string query)
    {
        logger.Debug("Executing SQL Statement: " + query);
        db.Execute(query);
    }

    public void CloseConnection()
    {
        if (File.Exists(dbLockPath)) // Database is locked
        {
            logger.Info("Closing DB Connection");
            logger.Debug("Disposing DB connection: " + dbFilePath);
            db!.Dispose();
            logger.Debug("Removing database lock file: " + dbLockPath);
            File.Delete(dbLockPath);
        }
        else
        {
            logger.Debug("Database lock file does not exist. Database is not use: " + dbLockPath);
        }
    }
}