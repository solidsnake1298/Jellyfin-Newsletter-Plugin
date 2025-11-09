#pragma warning disable 1591, CA1304
using System;
using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.LOGGER;
using Jellyfin.Plugin.Newsletters.Scripts.ENTITIES;
using MediaBrowser.Common.Configuration;
using SQLitePCL;
using SQLitePCL.pretty;

namespace Jellyfin.Plugin.Newsletters.Shared.DATA;

public class SQLiteDatabase
{
    private readonly PluginConfiguration config;
    private string dbFilePath;
    private string dbLockPath;
    private Logger logger;
    private SQLiteDatabaseConnection? _db;
    // private bool writeLock;

    public SQLiteDatabase()
    {
        logger = new Logger();
        config = Plugin.Instance!.Configuration;
        SQLite3.EnableSharedCache = false;

        _ = raw.sqlite3_config(raw.SQLITE_CONFIG_MEMSTATUS, 0);

        _ = raw.sqlite3_config(raw.SQLITE_CONFIG_MULTITHREAD, 1);

        _ = raw.sqlite3_enable_shared_cache(1);

        ThreadSafeMode = raw.sqlite3_threadsafe();
        dbFilePath = config.DataPath + "/newsletters.db"; // get directory from config
        dbLockPath = dbFilePath + ".lock";
    }

    internal static int ThreadSafeMode { get; set; }

    public void CreateConnection()
    {
        if (!File.Exists(dbLockPath)) // Database is not locked
        {
            logger.Debug("Opening Database: " + dbFilePath);
            _db = SQLite3.Open(dbFilePath);
            File.WriteAllText(dbLockPath, string.Empty);
            InitDatabaase();
            // writeLock = true;
        }
        else
        {
            logger.Debug("Database lock file shows database is in use: " + dbLockPath);
        }
    }

    private void InitDatabaase()
    {
        // Filename = string.Empty;
        // Title = string.Empty;
        // Album = string.Empty;
        // Season = 0;
        // Episode = 0;
        // Overview = string.Empty;
        // ImageURL = string.Empty;
        // ItemID = string.Empty;
        // PosterPath = string.Empty;
        // Type = string.Empty;
        // Emailed = 0;

       logger.Debug("Creating Tables...");
       CreateTables();
       MigrateTables();
       logger.Debug("Done Init of tables");
    }

    private void CreateTables()
    {
        ExecuteSQL("CREATE TABLE IF NOT EXISTS NewsletterData (" +
                "Filename TEXT NOT NULL," +
                "Title TEXT," +
                "Album TEXT," +
                "Season INT," +
                "Episode INT," +
                "Overview TEXT," +
                "ImageURL TEXT," +
                "ItemID TEXT," +
                "PosterPath TEXT," +
                "Type TEXT," +
                "Emailed INT," +
                "PRIMARY KEY (Filename));");
    }
    
    private void MigrateTables()
    {
        try
        {
            ExecuteSQL("INSERT INTO NewsletterData (" +
                            "Filename," +
                            "Title," +
                            "Season," +
                            "Episode," +
                            "Overview," +
                            "ImageURL," +
                            "ItemID," +
                            "PosterPath," +
                            "Type) " + 
                       "SELECT " +
                            "Filename," +
                            "Title," +
                            "Season," +
                            "Episode," +
                            "SeriesOverview," +
                            "ImageURL," +
                            "ItemID," +
                            "PosterPath," +
                            "Type " +
                            "FROM ArchiveData;");
            ExecuteSQL("DROP TABLE IF EXISTS CurrRunData");
            ExecuteSQL("DROP TABLE IF EXISTS CurrNewsletterData");
            ExecuteSQL("DROP TABLE IF EXISTS ArchiveData");
        }
        catch (Exception e)
        {
            logger.Debug("Legacy tables aren't present.");
        }
    }

    public IEnumerable<IReadOnlyList<ResultSetValue>> Query(string query)
    {
        logger.Debug("Running Query: " + query);
        return _db.Query(query);
    }

    public void ExecuteSQL(string query)
    {
        logger.Debug("Executing SQL Statement: " + query);
        _db.Execute(query);
    }

    public void CloseConnection()
    {
        if (File.Exists(dbLockPath)) // Database is locked
        {
            logger.Debug("Closing Database: " + dbFilePath);
            // _db.Close();
            File.Delete(dbLockPath);
            // logger.Debug("TYPE: " + conn.GetType());
            // writeLock = true;
        }
        else
        {
            logger.Debug("Database lock file does not exist. Database is not use: " + dbLockPath);
        }
    }
}