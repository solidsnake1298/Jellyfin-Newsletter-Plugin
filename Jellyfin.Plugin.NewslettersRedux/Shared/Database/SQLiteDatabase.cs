#pragma warning disable 1591, CA1304
using System;
using System.Collections.Generic;
using System.IO;
using SQLitePCL;
using SQLitePCL.pretty;

namespace Jellyfin.Plugin.NewslettersRedux.Shared.Database;

public class SqLiteDatabase
{
    private readonly string dbFilePath;
    private readonly string dbOldFilePath;
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

        dbFilePath = config.DataPath + "/newslettersRedux.db"; // get directory from config
        dbOldFilePath = config.DataPath + "/newsletters.db"; // get directory from config
        dbLockPath = dbFilePath + ".lock";
    }

    public void InitDatabase()
    {
        // Checks if other newsletter DB files exist and copies to new file name
        if (File.Exists(dbOldFilePath) && !File.Exists(dbFilePath))
        {
            logger.Info("Old newsletter DB file exists, copying to new file name");
            File.Copy(dbOldFilePath, dbFilePath, true);
        }
        
        if (!File.Exists(dbLockPath)) // Database is not locked
        {
            CreateConnection();
            var tableStatus = CheckTables();
            switch (tableStatus)
            {
                case 1:
                    logger.Info("Newsletter table does not exist.  Creating tables and migrating any existing or legacy data...");
                    CreateTables();
                    MigrateLegacyTables();
                    logger.Info("Done database init...");
                    break;
                case 2:
                    logger.Info("Older Newsletter table present.  Adding EndEpisode column...");
                    MigrateTables();
                    break;
                case 0:
                    logger.Debug("Database already initialized...");
                    break;
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

    private int CheckTables()
    {
        foreach (var row in Query("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='NewsletterVersion';"))
        {
            if (int.TryParse(row[0].ToString(), out var x) && x > 0)
            {
                logger.Debug("NewsletterVersion table is present.");
                if (int.TryParse(row[0].ToString(), out var y) && y == 2)
                {
                    logger.Debug("NewsletterDB is the latest version.");
                    return 0;
                }
            }
            else if (x == 0)
            {
                logger.Debug("NewsletterVersion table is not present, checking for NewsletterData table...");
                foreach (var row2 in Query("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='NewsletterData';"))
                {
                    if (int.TryParse(row2[0].ToString(), out var y) && y > 0)
                    {
                        logger.Info("Migrating v1 table to v2 table.");
                        return 2;
                    }
                    else
                    {
                        return 1;
                    }
                }
            }
        }

        return 0;
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
        ExecuteSql("CREATE TABLE IF NOT EXISTS NewsletterVersion (" +
                "ID INTEGER NOT NULL," +
                "TableVersion INTEGER," +
                "PRIMARY KEY (ID));");
        // Initializes table with default value.  Skips if row is already present.
        try
        {
            ExecuteSql("INSERT OR IGNORE INTO PreviousRun (" +
                "ID,LastRun) " +
                "VALUES (0,'12/30/2018 00:00:00 AM');");
            ExecuteSql("INSERT OR IGNORE INTO NewsletterVersion (" +
                "ID,TableVersion) " +
                "VALUES (0, 2);");
        }
        catch
        {
            logger.Debug("PreviousRun already populated.");
        }
    }
    
    private void MigrateLegacyTables()
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
            ExecuteSql("UPDATE NewsletterData SET Emailed = 1 WHERE Emailed IS NULL;");
            logger.Debug("Legacy tables successfully migrated.");
        }
        catch
        {
            logger.Debug("Legacy tables aren't present.");
        }
    }

    private void MigrateTables()
    {
        foreach (var row in Query("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='NewsletterVersion';"))
        {
            if (int.TryParse(row[0].ToString(), out var x) && x == 0)
            {
                    logger.Debug("v1 to v2 migration: Adding EndEpisode column to Newsletter table...");
                    ExecuteSql("ALTER TABLE NewsletterData ADD COLUMN EndEpisode INT;");
                    logger.Debug("Initializing EndEpisode column");
                    ExecuteSql("UPDATE NewsletterData SET EndEpisode = 0 WHERE EndEpisode is null;");
                    try
                    {
                        ExecuteSql("CREATE TABLE IF NOT EXISTS NewsletterVersion (" +
                                "ID INTEGER NOT NULL," +
                                "TableVersion INTEGER," +
                                "PRIMARY KEY (ID));");
                        ExecuteSql("INSERT OR IGNORE INTO NewsletterVersion (" +
                                "ID,TableVersion) " +
                                "VALUES (0, 2);");
                    }
                    catch
                    {
                        logger.Error("Could not initialize NewsletterVersion table.");
                    }
            }

            // Placeholder for future versions
            //else if (x == 1)
            //{
            //    foreach (var row in Query("SELECT TableVersion from NewsletterVersion WHERE ID = 0;"))
            //    {
            //        var tableVersion = row[0].ToString();
            //        if (int.TryParse(row[0].ToString(), out var x) && x == 1)
            //        {
            //            logger.Info("Migrating v2 table to v3 table.");
            //            return 2;
            //        }
            //    }
            //}
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