# Jellyfin Newsletter Plugin
<p align='center'>
    <img src='https://github.com/solidsnake1298/Jellyfin-Newsletter-Plugin/blob/master/logo.png?raw=true'/><br>
</p>
This is my first end-to-end C# project, but I hope you enjoy! (This is also true for this fork :D )

# Description
This plugin automacially scans a user\'s library (default every hour), populates a list of *recently added (not previously scanned)* media, converts that data into HTML format, and sends out emails to a provided list of recipients.  Emails are sent out weekly, by default.

<p align='center'>
    <img src='https://github.com/solidsnake1298/Jellyfin-Newsletter-Plugin/blob/master/NewsletterExample.png?raw=true'/><br>
</p>

# File Structure
To ensure proper images are being pulled from Jellyfin's database, ensure you follow the standard Organization Scheme for naming and organizing your files. https://jellyfin.org/docs/general/server/media/shows

If this format isn't followed properly, Jellyfin may have issue correctly saving the item's data in the proper database (the database that this plugin uses).

```
Shows
├── Series (2010)
│   ├── Season 00
│   │   ├── Some Special.mkv
│   │   ├── Episode S00E01.mkv
│   │   └── Episode S00E02.mkv
│   ├── Season 01
│   │   ├── Episode S01E01-E02.mkv
│   │   ├── Episode S01E03.mkv
│   │   └── Episode S01E04.mkv
│   └── Season 02
│       ├── Episode S02E01.mkv
│       ├── Episode S02E02.mkv
│       ├── Episode S02E03 Part 1.mkv
│       └── Episode S02E03 Part 2.mkv
└── Series (2018)
    ├── Episode S01E01.mkv
    ├── Episode S01E02.mkv
    ├── Episode S02E01-E02.mkv
    └── Episode S02E03.mkv

Movies
├── Film (1990).mp4
├── Film (1994).mp4
├── Film (2008)
│   └── Film.mkv
└── Film (2010)
    ├── Film-cd1.avi
    └── Film-cd2.avi

Music
├── Some Artist
│   ├── Album A
│   │   ├── Song 1.flac
│   │   ├── Song 2.flac
│   │   └── Song 3.flac
│   └── Album B
│       ├── Disc 1  // See below for other multi-disc naming options
│       │    ├── Track 1.m4a
│       │    ├── Track 2.m4a
│       │    └── Track 3.m4a
│       └── Disc 2
│            ├── Track 1.m4a
│            ├── Track 2.m4a
│            └── Track 3.m4a
└── Album X
    ├── Whatever You.mp3
    ├── Like To.mp3
    ├── Name Your.mp3
    └── Music Files.mp3
```

Valid naming options for multi-disc albums is listed <a href=https://github.com/jellyfin/jellyfin/blob/release-10.11.z/Emby.Naming/Common/NamingOptions.cs#L183>here</a>.

```
"cd",
"digital media",
"disc",
"disk",
"vol",
"volume",
"part",
"act"
```

# Testing/Run Frequency

Testing and Frequency can be managed through your Dashboard > Scheduled Tasks

- There are 2 scheduled tasks:
    - Email Newsletter (weekly): Which generates and sends out the newsletters via email from the data scanned from the task below
    - Filesystem Scraper (hourly):  Which scans your library, parses the data, and gets it ready for the email

# Installation

Manifest is up an running! You can now import the manifest in Jellyfin and this plugin will appear in the Catalog!
- Go to "Plugins" on your "Dashboard"
- Go to the "Repositories" tab
- Click the '+' to add a new Repository
    - Give it a name (i.e. Newsletters)
    - In "Repository URL," put "https://raw.githubusercontent.com/solidsnake1298/Jellyfin-Newsletter-Plugin/master/manifest.json"
    - Click "Save"
- You should now see Jellyfin Newsletters in Catalog under the Category "Newsletters"
- Once installed, restart Jellyfin to activate the plugin and configure your settings for the plugin

# Configuration

## General Config

### To Addresses:
- Recipients of the newsletter. Add add as many emails as you'd like, separated by commas.
    - All emails will be sent out via BCC

### From Address
- The address recipients will see on emails as the sender
    - Defaults to JellyfinNewsletter@donotreply.com

### Subject
- The subject of the email

### Library Selection
- Select the item types you want to scan
    - NOTE: this is Item types, not libraries

## Newsletter HTML Format
Allows for use of custom HTML formatting for emails! Defaults to original formatting, but can be modified now!

For defaults, see `Jellyfin.Plugin.Newsletters/Templates/`

### Body HTML
- The main body of your email

### EntryData HTML
- The formatting for each individual entry/series/movie that was found and will be sent out

## Scraper/Scanner Config

### Poster Hosting Type
- Obsolete.  All images are resized and attached to the outgoing email.  ImageURL now uses the content ID of the attachment.

### Hostname
- Obsolete.  No longer used.

## SMTP Config

### Smtp Server Address
- The email server address you want to use. 
    - Defaults to smtp.gmail.com

### Smtp Port
- The port number used by the email server above
    - Defaults to gmail's port (587)

### Smtp Username
- Your username/email to authenticate to the SMTP server above

### Smtp Password
- Your password to authenticate to the SMTP server above
    - I'm not sure about other email servers, but google requires a Dev password to be created.
        - For gmail specific instructions, you can visit https://support.google.com/mail/answer/185833?hl=en for details

# Issues
Please leave a ticket in the Issues on this GitHub page and I will get to it as soon as I can. 
Please be patient with me, since I did this on the side of my normal job. But I will try to fix any issues that come up to the best of my ability and as fast as I can!

# Available HTML Data Tags
Some of these may not interest that average user (if anyone), but I figured I would have any element in the Newsletters.db be available for use! <br>
**NOTE:** *Examples of most tags can be found in the default Templates (template_modern_body.html AND template_modern_entry.html)*

## Required Tags
```
- {EntryData} - Needs to be inside of the 'Body' html
```
## Recommended Tags
```
- {Date} - Auto-generated date of Newsletter email generation
- {TitleInfo} - This tag is the Plugin-generated Season/Episode data
- {Title} - Title of Movie/Series
- {Overview} - Movie/Series overview
- {ImageURL} - Base64 encoded image string.
- {Type} - Item type (Movie or Series)
```
## Non-Recommended Tags
These tags are ***available*** but not recommended to use. Untested behavior using these.
```
- {Filename} - File path of the Movie/Episode (NOT RECOMMENDED TO USE)
- {Season} - Season number of Episode (NOT RECOMMENDED TO USE)
- {Episode} - Episode number (NOT RECOMMENDED TO USE)
- {ItemID} - Jellyfin's assigned ItemID (NOT RECOMMENDED TO USE)
- {PosterPath} - Jellyfin's assigned Poster Path (NOT RECOMMENDED TO USE)
```
## Known Issues
See 'issues' tab in GitHub with the label 'bug'

# Contribute
If you would like to collaborate/contribute, feel free! Make all PR's to the 'development' branch and please note clearly what was added/fixed, thanks!
