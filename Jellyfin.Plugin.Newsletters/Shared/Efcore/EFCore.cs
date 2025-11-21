//#pragma warning disable 1591, CA1304
//using System;
//using System.Collections.Generic;
//using System.ComponentModel.DataAnnotations;
//using System.IO;
//using System.Linq;
//using Jellyfin.Plugin.Newsletters.Configuration;
//using Jellyfin.Plugin.Newsletters.NLPLogger;
//using Jellyfin.Plugin.Newsletters.Scripts.Entities;
//using MediaBrowser.Common.Configuration;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Infrastructure;
//using Microsoft.EntityFrameworkCore.Migrations;
//using Microsoft.EntityFrameworkCore.Sqlite;
//using Microsoft.EntityFrameworkCore.Tools;
//using Microsoft.Extensions.DependencyInjection;
//
//namespace Jellyfin.Plugin.Newsletters.Shared.Efcore;
//
//internal class Program
//{
//    private static async Task Main()
//    {
//        using (var db = new NLPContext())
//        {
//            // Remove these lines if you are running migrations from the command line
//            await db.Database.EnsureCreatedAsync();
//            await db.Database.MigrateAsync();
//        }
//
//        #region Querying
//        using (var db = new NLPContext())
//        {
//            var nlp = await db.NewsletterData
//                .Where(b => b.Rating > 3)
//                .OrderBy(b => b.Url)
//                .ToListAsync();
//        }
//        #endregion
//
//        #region SavingData
//        using (var db = new BloggingContext())
//        {
//            var blog = new Blog { Url = "http://sample.com" };
//            db.Blogs.Add(blog);
//            await db.SaveChangesAsync();
//        }
//        #endregion
//    }
//}