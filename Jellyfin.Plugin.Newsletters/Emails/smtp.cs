#pragma warning disable 1591
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.Emails.HTMLBuilder;
using Jellyfin.Plugin.Newsletters.NLPLogger;
using Jellyfin.Plugin.Newsletters.Scanner.NLImageHandler;
using Jellyfin.Plugin.Newsletters.Scripts.Entities;
using Jellyfin.Plugin.Newsletters.Shared.Efcore;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.Newsletters.Emails.Email;

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
    private Logger logger;

    public Smtp()
    {
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
            //db.CreateConnection();

            if (NewsletterDbIsPopulated())
            {
                logger.Debug("Sending out mail!");
                MailMessage mail = new MailMessage();
                string smtpAddress = config.SMTPServer;
                int portNumber = config.SMTPPort;
                bool enableSSL = true;
                string emailFromAddress = config.FromAddr;
                string username = config.SMTPUser;
                string password = config.SMTPPass;
                string emailToAddress = config.ToAddr;
                string subject = config.Subject;

                // Builds email HTML
                HtmlBuilder hb = new HtmlBuilder();

                // Generates initial HTML body
                string body = hb.GetDefaultHTMLBody();
                // Generates then inserts each entry (series, movie, album) into the body
                string builtString = hb.BuildDataHtmlStringFromNewsletterData();
                builtString = hb.ReplaceBodyWithBuiltString(body, builtString);
                // Adds current date to top of newsletter
                string currDate = DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                builtString = builtString.Replace("{Date}", currDate, StringComparison.Ordinal);
                // Retrieves, resizes, and attaches/embed images into email
                List<string> contentId = hb.BuildContentId();
                string attachmentDir = config.DataPath + "/newsletterImages";
                foreach (var row in contentId)
                {
                    try
                    {
                        // Uses series/movie/album artist itemID as HTML content ID tag key
                        ContentIdJson? contentID = JsonConvert.DeserializeObject<ContentIdJson>(row);
                        string posterPath = contentID!.PosterPath;
                        string itemID = contentID!.ItemID;
                        string extension = string.Empty;
                        Directory.CreateDirectory(attachmentDir);
                        Stream imageStream;
                        // Resizes image 
                        imageStream = PosterImageHandler.ResizeImage(posterPath);
                        extension = Path.GetExtension(posterPath);
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
                mail.From = new MailAddress(emailFromAddress, emailFromAddress);
                mail.To.Clear();
                mail.Headers.Add("MIME-Version", "1.0");
                mail.Headers.Add("Content-Type", "multipart/mixed");
                mail.Headers.Add("Content-Type", "boundary='----blackmoon'" );
                mail.Subject = subject;
                mail.Body = Regex.Replace(builtString, "{[A-za-z]*}", " "); // Final cleanup
                mail.IsBodyHtml = true;

                foreach (string email in emailToAddress.Split(','))
                {
                    mail.Bcc.Add(email.Trim());
                }

                // Sends email
                SmtpClient smtp = new SmtpClient(smtpAddress, portNumber);
                smtp.Credentials = new NetworkCredential(username, password);
                smtp.EnableSsl = enableSSL;
                smtp.Send(mail);

                hb.CleanUp(builtString);
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
            logger.Debug("Finished!!");
        }
    }

    private bool NewsletterDbIsPopulated()
    {
        using (var db = new NLPContext())
        {
            int count = db.NewsletterData.Where(n => n.Emailed == 0).Count();
            if (count > 0)
            {
                return true;                    
            }
            else
            {
                return false;
            }
        }
    }
}