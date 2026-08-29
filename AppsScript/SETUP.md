# Connecting srs to your practice sheet

You need **your own copy** of the practice sheet template, and srs's endpoint inside it.
Deploying that endpoint is what gives srs a private URL it can write your times to.

`Code.gs` in this folder is that endpoint. **A copy made from the current template already
carries it**, as `srsExport.gs`, so there is nothing to paste in: you only deploy it. A copy
made before 2026-08-29 does not have it, and step 2 below tells you how to add it.

Nothing in it runs until you deploy. It adds no trigger, and its two entry points are
reachable only through a deployment URL, so an undeployed copy behaves exactly as before.

## What it reads and writes

It writes times to `A Sides`, `B+C Sides` and `Farewell`, the three tabs the sheet expects you
to fill. It never touches a category tab: those read from the three above, and writing into one
would turn the cell manual and break its auto-fill.

It also reads the `Route` row of your `Home Page` tab, so the export screen opens on the route
you actually run rather than the first one in its list. Nothing depends on it: a sheet whose
Home Page has moved, or has no route in a column, simply gets the first route instead.

It reads one more thing, and writes none of it: the `Segment` / `Chapter Time` summary block of
your `Any%` and `True Ending` tabs, which is where the export screen's *Sum of Best* line comes
from. That total is your sheet's, never a sum srs does itself. Same rule as the route: a block
that has moved or is missing just leaves the line showing `-`.

## Deploy

1. Open your copy of the sheet, then **Extensions > Apps Script**.
2. Look for `srsExport.gs` in the file list on the left. If it is there, skip to step 3.
   If it is not, your copy predates it: add `Code.gs` from this folder as a **new** script
   file, next to the sheet's own scripts. Do not replace them: the sheet needs them, and
   this endpoint does not conflict with them (every function in it is prefixed `srs`,
   except `doGet` and `doPost`, whose names Apps Script fixes).
3. **Deploy > New deployment**. Click the gear next to *Select type* and pick **Web app**,
   then set the two fields that decide everything:
   - *Execute as*: **Me**
   - *Who has access*: **Anyone**
4. Authorise the script when Google asks. This is where the two warning screens appear;
   the section below says what they are asking for and why.
5. Copy the generated `/exec` URL. It ends in `/exec`, never `/dev`: the `/dev` URL is the
   editor's own test address, it requires you to be logged in, and srs cannot use it.
6. In Celeste, with the URL still in your clipboard: **Mod Options > Speedrun Sheet >
   Set Sheet URL from clipboard**. It works from a paused level as well as from the title
   screen, and it refuses anything that is not https ending in `/exec`, then asks the
   endpoint whether it answers as srs's own. Once one is set, the button reads *Replace
   Sheet URL from clipboard*.

If the script is ever updated, putting the new code in your sheet is not enough on its own:
the deployed URL keeps serving the old code until you publish a new version. **Deploy >
Manage deployments >** pencil icon **> Version: New version > Deploy**. The URL does not
change.

## The two screens Google shows you, and what they mean

**"Google hasn't verified this app."** Correct, and it cannot be otherwise. Verification
applies to one OAuth client, and your copy of the sheet is its own script project with its
own client. Even a verified template would not carry that over to a single copy: the app
you are authorising is yours, not somebody else's. Click **Advanced**, then **Go to ...
(unsafe)**. It is your own script, in your own document.

**The permission list.** It names Google Sheets, and it is worth reading precisely, because
it asks for more than this endpoint uses:

- *See, edit, create, and delete all your Google Sheets spreadsheets.* This one is the
  **sheet's own** scripts, not srs. They read the shared reference workbook to refresh your
  standards, which is a second document, and they write formatting through an interface that
  cannot be narrowed to one file. That is where the breadth comes from, and it was already
  there before this endpoint existed.
- *Display and run third-party web content in prompts and sidebars.* The sheet's own menus
  and dialogs.

If the screen shows a checkbox next to each line, **leave them all ticked**. Unticking the
Sheets one does not trim this endpoint down to what it uses: permissions are granted per
project, and that single line is the one it runs on too, so the export would fail with a
permission error that names nothing you would recognise.

Permissions in Apps Script are granted per project, never per function, so this endpoint
inherits the list rather than adding to it. What **it** actually touches is the document it
lives in: `SpreadsheetApp.getActive()`, and nothing else. It opens no other spreadsheet, and
it makes no outbound request of any kind.

## "Who has access" must be "Anyone"

"Only myself" sounds like the private, safer choice. It is not compatible with this mod:
Google then demands an OAuth token on every call and answers a plain HTTP request with a
login page, so every sync fails. Only "Anyone" makes the URL itself sufficient.

Note that "Execute as: Me" means the script writes with *your* permissions — the sheet
itself never has to be shared with anyone.

## Your /exec URL is a password

It grants read and write access to this sheet, with your permissions, to anyone holding
it. **Never write it inside the sheet itself** — not in a cell, not in a comment, not in
the description. It belongs only in the mod's settings.

Making your copy of the sheet publicly viewable is safe: deployment URLs are not exposed
through the sheet's sharing settings. Revoking a leaked URL means creating a new
deployment (**Deploy > Manage deployments >** archive the old one) and pasting the new URL
into the mod.

## What the script is allowed to touch

Only three tabs: **A Sides**, **B+C Sides** and **Farewell**. Every other tab is
formula-driven and reads from those three, so writing into one would turn its cells manual
and break the sheet's auto-fill.

On a matched row the script writes the **Time** and **Date** cells, and nothing else. The
**Standard** column is a formula and is never written; the Date is stamped by the script
because the sheet's own auto-fill only reacts to edits made by hand.

The summary block it reads for the *Sum of Best* line is found the same way, by its `Segment`
and `Chapter Time` headers, so the rows moving with your route costs nothing.

Rows are found by their labels, not by position: inserting rows above a table is fine, and
so is inserting rows inside it. Renaming a tab, renaming a checkpoint, or renaming the
headers is not: srs then reports the affected rows as not found and leaves them alone.
The headers it looks for are `Chapter` and `Checkpoint` on the two side tabs, `CP` on
Farewell, and `Time` and `Date` on all three. Times already in the sheet are overwritten
without asking, so review the ticked rows before confirming.
