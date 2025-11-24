#pragma warning disable 1591, CA1304
using System;
using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.NLPLogger;
using Jellyfin.Plugin.Newsletters.Scripts.ENTITIES;
using MediaBrowser.Common.Configuration;
using SQLitePCL;
using SQLitePCL.pretty;

namespace Jellyfin.Plugin.Newsletters.Shared.DATA;

public class SqlLiteDatabase
{
    private readonly PluginConfiguration config;
    private string dbFilePath;
    private string dbLockPath;
    private Logger logger;
    private SQLiteDatabaseConnection? _db;

    public SqlLiteDatabase()
    {
        logger = new Logger();
        config = Plugin.Instance!.Configuration;
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
                logger.Debug("Database not initialized.  Creating tables and migrating any existing or legacy data...");
                CreateTables();
                MigrateTables();
                logger.Debug("Done Init of tables");
            }
            else
            {
                logger.Debug("Database already initialized...");
            }

            CloseConnection();
        }
        else
        {
            logger.Debug("Database lock file shows database is in use: " + dbLockPath);
        }
    }

    public void CreateConnection()
    {
        if (!File.Exists(dbLockPath)) // Database is not locked
        {
            logger.Debug("Opening Database: " + dbFilePath);
            _db = SQLite3.Open(dbFilePath);
            File.WriteAllText(dbLockPath, string.Empty);
        }
        else
        {
            logger.Debug("Database lock file shows database is in use: " + dbLockPath);
        }
    }

    private bool CheckTables()
    {
        List<string> nlpColumns = new List<string> { "Filename", "Title", "Album", "Season", "Episode", "Overview", "ItemID", "PosterPath", "Type", "Emailed" };
        List<string> prColumns = new List<string> { "ID", "LastRun" };
        foreach (var row in Query("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='NewsletterData';"))
        {
            logger.Debug($"nlpPresent:: {row[0].ToString()}");
            if (int.TryParse(row[0].ToString(), out var x) && x > 0)
            {
                foreach (var column in Query("SELECT name FROM pragma_table_info('NewsletterData');"))
                {
                    logger.Debug($"column:: {column[0]}");
                    var isColumnPresent = nlpColumns.Contains(column[0]!.ToString());
                    if (isColumnPresent)
                    {
                        continue;
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
                    var isColumnPresent = prColumns.Contains(column[0]!.ToString());
                    if (isColumnPresent)
                    {
                        continue;
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
        // Initalizes table with default value.  Skips if row is already present.
        try
        {
            ExecuteSql("INSERT OR IGNORE INTO PreviousRun (" +
                "ID,LastRun) " +
                "VALUES (0,'12/30/2018 00:00:00 AM');");
        }
        catch
        {
            logger.Debug("PreviousRun already populated.");
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

    public IEnumerable<IReadOnlyList<ResultSetValue>> Query(string query)
    {
        logger.Debug("Running Query: " + query);
        return _db.Query(query);
    }

    public void ExecuteSql(string query)
    {
        logger.Debug("Executing SQL Statement: " + query);
        _db.Execute(query);
    }

    public void CloseConnection()
    {
        if (File.Exists(dbLockPath)) // Database is locked
        {
            logger.Debug("Disposing DB connection: " + dbFilePath);
            _db!.Dispose();
            logger.Debug("Removing database lock file: " + dbLockPath);
            File.Delete(dbLockPath);
        }
        else
        {
            logger.Debug("Database lock file does not exist. Database is not use: " + dbLockPath);
        }
    }
}