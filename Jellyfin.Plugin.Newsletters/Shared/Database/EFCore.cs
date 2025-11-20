#pragma warning disable 1591, CA1304
using System;
using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.LOGGER;
using Jellyfin.Plugin.Newsletters.Scripts.ENTITIES;
using MediaBrowser.Common.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SQLite;

namespace Jellyfin.Plugin.Newsletters.Shared.DATA;

public class SQLiteDatabase : DbContext
{
    public DbSet<Newsletter> NewsletterData { get; set; }
    public DbSet<Previous> PreviousRun { get; set; }

    private string dbFilePath = config.DataPath + "/newsletters.db"; // get directory from config

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={dbFilePath}"); 
    }
}

public class Newsletter
{
    public string Filename { get; set; }
    public string Title { get ; set; }
    public string Album { get; set; }
    public int Season { get; set; }
    public int Episode { get; set; }
    public string Overview { get; set; }
    public string ItemID { get; set; }
    public string PosterPath { get; set; }
    public string Type { get; set; }
    public int Emailed { get; set; }
}

public class Previous
{
    public int ID { get; set; }
    public string LastRun { get; set; }
}