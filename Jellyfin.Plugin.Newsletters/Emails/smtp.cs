#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.Scanner;
using Jellyfin.Plugin.Newsletters.Shared.Database;
using Jellyfin.Plugin.Newsletters.Shared.Entities;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.Newsletters.Emails;

/// <summary>
/// Interaction logic for SendMail.xaml.
/// </summary>
// [Route("newsletters/[controller]")]
[Authorize(Policy = Policies.RequiresElevation)]
[ApiController] 
[Route("Smtp")]
public class Smtp : ControllerBase
{
    private readonly PluginConfiguration config;
    // private readonly string newsletterDataFile;
    private readonly SqLiteDatabase db;
    private readonly Logger logger;

    public Smtp()
    {
        db = new SqLiteDatabase();
        logger = new Logger();
        config = Plugin.Instance!.Configuration;
    }

    [HttpPost("SendTestMail")]
    public void SendTestMail()
    {
        try
        {
            logger.Debug("Sending out test mail!");
            var mail = new MailMessage
            {
                From = new MailAddress(config.FromAddr),
                Subject = "Jellyfin Newsletters - Test",
                Body = "Success! You have properly configured your email notification settings",
                IsBodyHtml = false
            };

            mail.To.Clear();

            foreach (var email in config.ToAddr.Split(','))
            {
                mail.Bcc.Add(email.Trim());
            }

            var smtp = new SmtpClient(config.SMTPServer, config.SMTPPort)
            {
                Credentials = new NetworkCredential(config.SMTPUser, config.SMTPPass),
                EnableSsl = true
            };
            smtp.Send(mail);
            mail.Dispose();
            smtp.Dispose();
        }
        catch (Exception e)
        {
            logger.Error("An error has occured: " + e);
        }
    }

    [HttpPost("SendSmtp")]
    // [ProducesResponseType(StatusCodes.Status201Created)]
    // [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public void SendEmail()
    {
        try
        {
            if (NewsletterDbIsPopulated())
            {
                logger.Info("Generating email...");
                var mail = new MailMessage();
                // Builds email HTML
                var hb = new HtmlBuilder();
                // Generates initial HTML body
                var body = hb.GetDefaultHtmlBody();
                // Generates then inserts each entry (series, movie, album) into the body
                var builtString = hb.BuildDataHtmlStringFromNewsletterData();
                builtString = hb.ReplaceBodyWithBuiltString(body, builtString);
                // Adds current date to top of newsletter
                var currDate = DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                builtString = builtString.Replace("{Date}", currDate, StringComparison.Ordinal);
                // Retrieves, resizes, and attaches/embed images into email
                var contentIdList = hb.BuildContentId();
                var attachmentDir = config.DataPath + "/newsletterImages";
                logger.Info("Resizing images and attaching to email...");
                foreach (var row in contentIdList)
                {
                    try
                    {
                        // Uses series/movie/album artist itemID as HTML content ID tag key
                        var contentId = JsonConvert.DeserializeObject<ContentIdJson>(row);
                        var posterPath = contentId!.PosterPath;
                        var itemId = contentId.ItemId;
                        Directory.CreateDirectory(attachmentDir);
                        // Resizes image 
                        var imageStream = PosterImageHandler.ResizeImage(posterPath);
                        var extension = Path.GetExtension(posterPath);
                        // Writes resized image to disk, attaches to email
                        imageStream.Position = 0;
                        var attachmentPath = $"{attachmentDir}/{itemId}{extension}";
                        var fileStream = System.IO.File.Create($"{attachmentPath}");
                        imageStream.CopyTo(fileStream);
                        fileStream.Close();
                        var fileAttachment = new Attachment($"{attachmentPath}");
                        fileAttachment.ContentId = itemId;
                        mail.Attachments.Add(fileAttachment);
                    }
                    catch
                    {
                        logger.Error("Error generating image attachment.  Null image path?");
                    }
                }
                
                // SMTP header
                mail.From = new MailAddress(config.FromAddr, config.FromAddr);
                mail.To.Clear();
                mail.Headers.Add("MIME-Version", "1.0");
                mail.Headers.Add("Content-Type", "multipart/mixed");
                mail.Headers.Add("Content-Type", "boundary='----blackmoon'" );
                mail.Subject = config.Subject;
                mail.Body = Regex.Replace(builtString, "{[A-za-z]*}", " "); // Final regex replacement
                mail.IsBodyHtml = true;

                foreach (string email in config.ToAddr.Split(','))
                {
                    mail.Bcc.Add(email.Trim());
                }

                // Sends email
                var smtp = new SmtpClient(config.SMTPServer, config.SMTPPort);
                smtp.Credentials = new NetworkCredential(config.SMTPUser, config.SMTPPass);
                smtp.EnableSsl = true;
                smtp.Send(mail);

                logger.Info("Successfully sent email.");

                hb.CleanUp();
                mail.Dispose();
                smtp.Dispose();
                // Attachment Image dir cleanup
                var di = new DirectoryInfo($"{attachmentDir}");
                logger.Info("Cleaning up WIP files...");
                foreach (var file in di.GetFiles())
                {
                    file.Delete(); 
                }
                
                foreach (var dir in di.GetDirectories())
                {
                    dir.Delete(true); 
                }
            }
            else
            {
                logger.Info("There is no Newsletter data.  Was the file scraper job ran prior to generating an email?");
            }
        }
        catch (Exception e)
        {
            logger.Error("An error has occured: " + e);
        }
        finally
        {
            logger.Debug("Successfully completed job.");
        }
    }

    private bool NewsletterDbIsPopulated()
    {
        db.CreateConnection();
        foreach (var row in db.Query("SELECT COUNT(*) FROM NewsletterData WHERE Emailed = 0;"))
        {
            if (int.TryParse(row[0].ToString(), out var x) && x > 0)
            {
                db.CloseConnection();
                logger.Info($"Found {x} items waiting to be emailed.");
                return true;
            }
        }

        db.CloseConnection();
        return false;
    }
}