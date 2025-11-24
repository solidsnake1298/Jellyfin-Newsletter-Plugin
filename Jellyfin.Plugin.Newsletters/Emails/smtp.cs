#pragma warning disable 1591
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.Emails.HTMLBuilder;
using Jellyfin.Plugin.Newsletters.NLPLogger;
using Jellyfin.Plugin.Newsletters.Scanner.NLImageHandler;
using Jellyfin.Plugin.Newsletters.Scripts.ENTITIES;
using Jellyfin.Plugin.Newsletters.Shared.DATA;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.Newsletters.Emails.EMAIL;

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
    private SqlLiteDatabase db;
    private Logger logger;

    public Smtp()
    {
        db = new SqlLiteDatabase();
        logger = new Logger();
        config = Plugin.Instance!.Configuration;
    }

    [HttpPost("SendTestMail")]
    public void SendTestMail()
    {
        MailMessage mail;
        SmtpClient smtp;

        try
        {
            logger.Debug("Sending out test mail!");
            mail = new MailMessage();

            mail.From = new MailAddress(config.FromAddr);
            mail.To.Clear();
            mail.Subject = "Jellyfin Newsletters - Test";
            mail.Body = "Success! You have properly configured your email notification settings";
            mail.IsBodyHtml = false;

            foreach (string email in config.ToAddr.Split(','))
            {
                mail.Bcc.Add(email.Trim());
            }

            smtp = new SmtpClient(config.SMTPServer, config.SMTPPort);
            smtp.Credentials = new NetworkCredential(config.SMTPUser, config.SMTPPass);
            smtp.EnableSsl = true;
            smtp.Send(mail);
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
                logger.Debug("Sending out mail!");
                var mail = new MailMessage();
                // Builds email HTML
                HtmlBuilder hb = new HtmlBuilder();
                // Generates initial HTML body
                var body = config.Body;
                // Generates then inserts each entry (series, movie, album) into the body
                var builtString = hb.BuildDataHtmlStringFromNewsletterData();
                builtString = hb.ReplaceBodyWithBuiltString(body, builtString);
                // Adds current date to top of newsletter
                var currDate = DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                builtString = builtString.Replace("{Date}", currDate, StringComparison.Ordinal);
                // Retrieves, resizes, and attaches/embed images into email
                List<string> contentId = hb.BuildContentId();
                var attachmentDir = config.DataPath + "/newsletterImages";
                foreach (var row in contentId)
                {
                    try
                    {
                        // Uses series/movie/album artist itemID as HTML content ID tag key
                        ContentIdJson? contentID = JsonConvert.DeserializeObject<ContentIdJson>(row);
                        var posterPath = contentID!.PosterPath;
                        var itemID = contentID!.ItemID;
                        Directory.CreateDirectory(attachmentDir);
                        // Resizes image 
                        var imageStream = PosterImageHandler.ResizeImage(posterPath);
                        var extension = Path.GetExtension(posterPath);
                        // Writes resized image to disk, attaches to email
                        imageStream.Position = 0;
                        string? attachmentPath = $"{attachmentDir}/{itemID}{extension}";
                        var fileStream = System.IO.File.Create($"{attachmentPath}");
                        imageStream.CopyTo(fileStream);
                        fileStream.Close();
                        Attachment? fileAttachment = new Attachment($"{attachmentPath}");
                        fileAttachment.ContentId = itemID;
                        mail.Attachments.Add(fileAttachment);
                    }
                    catch
                    {
                        logger.Debug("Error generating image attachment.  Null image path?");
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
                SmtpClient smtp = new SmtpClient(config.SMTPServer, config.SMTPPort);
                smtp.Credentials = new NetworkCredential(config.SMTPUser, config.SMTPPass);
                smtp.EnableSsl = true;
                smtp.Send(mail);

                hb.CleanUp();
                // Attachment Image dir cleanup
                System.IO.DirectoryInfo di = new DirectoryInfo($"{attachmentDir}");
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete(); 
                }
                
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    dir.Delete(true); 
                }
            }
            else
            {
                logger.Info("There is no Newsletter data.. Have I scanned or sent out a newsletter recently?");
            }
        }
        catch (Exception e)
        {
            logger.Error("An error has occured: " + e);
        }
        finally
        {
            logger.Debug("Finished sending email!!");
        }
    }

    private bool NewsletterDbIsPopulated()
    {
        db.CreateConnection();
        foreach (var row in db.Query("SELECT COUNT(*) FROM NewsletterData WHERE Emailed = 0;"))
        {
            if (row is not null)
            {
                if (int.TryParse(row[0].ToString(), out var x) && x > 0)
                {
                    db.CloseConnection();
                    return true;
                }
            }
        }

        db.CloseConnection();
        return false;
    }
}