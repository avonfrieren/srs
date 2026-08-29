/**
 * srs: export endpoint for your own copy of the practice sheet.
 *
 * Since 2026-08-29 the sheet template carries this file as srsExport.gs, so a copy made
 * from it has nothing to paste in and only has to deploy. This file stays the source of
 * truth: change it here, then push the same change to the template, or the two drift and
 * a player's copy is whichever version it was made from. Only the header differs there.
 * SETUP.md next to this file is what the player follows, older copies included.
 * Every function here is prefixed "srs" except doGet/doPost, which Apps Script requires.
 * The generated /exec URL is a secret: it grants read and write access to
 * this spreadsheet with the owner's permissions.
 */

// The only writable tabs. Everything else is formula-driven and reads from these.
var SRS_TABS = {
  'a sides': { name: 'A Sides', hasChapter: true },
  'b+c sides': { name: 'B+C Sides', hasChapter: true },
  'farewell': { name: 'Farewell', hasChapter: false }
};
var SRS_TAB_ORDER = ['a sides', 'b+c sides', 'farewell'];

// Where the player records the route they run, one column per category.
var SRS_HOME_TAB = 'Home Page';

// The category tabs whose summary block carries a chapter total. Only these two
// are stable on the sheet; the others lay their summary out differently and
// carry #REF! where a category is not filled in.
var SRS_CATEGORY_TABS = ['Any%', 'True Ending'];

function doGet() {
  try {
    var cache = {};
    var rows = [];
    SRS_TAB_ORDER.forEach(function (key) {
      var table = srsTable(cache, key);
      if (table.error) {
        return; // a broken tab is reported on POST, per row; GET just omits it
      }
      table.rows.forEach(function (r) {
        rows.push({
          tab: table.name,
          chapter: r.chapter,
          cp: r.cp,
          time: r.time,
          standard: r.standard
        });
      });
    });
    // never fatal: the export is what this endpoint is for, and a sheet whose
    // Home Page has moved must still be writable
    var routes = [];
    try {
      routes = srsReadRoutes();
    } catch (err) {
      routes = [];
    }

    // never fatal, for the same reason as the routes: a summary block that
    // moved must not cost the player their export
    var sobs = [];
    try {
      sobs = srsReadSobs();
    } catch (err) {
      sobs = [];
    }

    return srsJson({ rows: rows, routes: routes, sobs: sobs });
  } catch (err) {
    return srsJson({ error: String(err) });
  }
}

function doPost(e) {
  var lock = LockService.getDocumentLock();
  try {
    var body = e && e.postData ? e.postData.contents : '';
    if (!body) {
      return srsJson({ error: 'empty request body' });
    }
    var req = JSON.parse(body);
    var updates = req.updates === undefined ? [] : req.updates;
    if (!Array.isArray(updates)) {
      return srsJson({ error: '"updates" must be an array' });
    }
    lock.waitLock(30000);
    var cache = {};
    var results = updates.map(function (u) {
      return srsWriteOne(cache, u || {});
    });
    SpreadsheetApp.flush();
    return srsJson({ results: results });
  } catch (err) {
    return srsJson({ error: String(err) });
  } finally {
    try {
      lock.releaseLock();
    } catch (ignored) {
      // never held
    }
  }
}

/**
 * NFC, collapsed whitespace, U+FE0F stripped, lowercased. Emoji are NOT stripped:
 * "Depths 📼 RTM" and "Depths 💙+📼 RTM" are distinct rows.
 */
/**
 * The route the player runs, per category, from the Home Page tab: a row
 * labelled "Category" and one labelled "Route", read as columns.
 *
 * The rows are searched for rather than addressed by number, the same lesson
 * srsFindHeader learned: a tab gains a row and everything below it moves.
 * A category with no route in it is left out rather than reported empty.
 */
function srsReadRoutes() {
  var sheet = SpreadsheetApp.getActive().getSheetByName(SRS_HOME_TAB);
  if (!sheet) {
    return [];
  }

  // display values, not raw ones: "100%" is the number 1 with a percent format,
  // and getValues hands back 1, which names no category
  var values = sheet.getDataRange().getDisplayValues();
  var categoryRow = -1;
  var routeRow = -1;
  for (var i = 0; i < values.length; i++) {
    var label = srsNorm(values[i][0]);
    if (label === 'category' && categoryRow < 0) {
      categoryRow = i;
    } else if (label === 'route' && routeRow < 0) {
      routeRow = i;
    }
  }

  if (categoryRow < 0 || routeRow < 0) {
    return [];
  }

  var out = [];
  for (var c = 1; c < values[categoryRow].length; c++) {
    var category = String(values[categoryRow][c] == null ? '' : values[categoryRow][c]).trim();
    var route = c < values[routeRow].length
      ? String(values[routeRow][c] == null ? '' : values[routeRow][c]).trim()
      : '';
    if (category && route) {
      out.push({ category: category, route: route });
    }
  }

  return out;
}

/**
 * The chapter totals from each category tab's summary block: the "Checkpoints"
 * table, whose "Segment" column names a chapter and whose "Chapter Time" column
 * holds that chapter's sum of best.
 *
 * The block is found by its header rather than addressed by number, like
 * srsFindHeader and srsReadRoutes: the rows move with the route.
 *
 * A row whose Segment is blank is a chapter the route does not visit -- the
 * cells are formulas keyed on the route in A2, and they render empty. So a
 * blank row is skipped rather than read as the end of the table. What ends it
 * is the next section title ("IL's"), which is a Segment with no Chapter Time.
 *
 * The route in A2 comes back with the totals on purpose: the whole block is
 * computed for that route, so a caller showing another one must not use them.
 */
function srsReadSobs() {
  var out = [];
  SRS_CATEGORY_TABS.forEach(function (name) {
    var sheet = SpreadsheetApp.getActive().getSheetByName(name);
    if (!sheet) {
      return;
    }

    // display values for the same reason as everywhere else: these cells are
    // formatted durations, and the raw value is a fraction of a day
    var values = sheet.getDataRange().getDisplayValues();
    var header = -1;
    var segCol = -1;
    for (var i = 0; i < values.length && header < 0; i++) {
      for (var c = 0; c < values[i].length - 1; c++) {
        if (srsNorm(values[i][c]) === 'segment' && srsNorm(values[i][c + 1]) === 'chapter time') {
          header = i;
          segCol = c;
          break;
        }
      }
    }

    if (header < 0) {
      return;
    }

    var chapters = [];
    for (var r = header + 1; r < values.length; r++) {
      var segment = String(values[r][segCol] == null ? '' : values[r][segCol]).trim();
      var time = String(values[r][segCol + 1] == null ? '' : values[r][segCol + 1]).trim();
      if (!segment) {
        continue;   // a chapter this route does not visit
      }
      if (!time) {
        break;      // a section title: the end of the Checkpoints table
      }
      chapters.push({ segment: segment, time: time });
    }

    out.push({
      category: name,
      route: String(values.length > 1 && values[1][0] != null ? values[1][0] : '').trim(),
      chapters: chapters
    });
  });

  return out;
}

function srsNorm(value) {
  return String(value == null ? '' : value)
    .normalize('NFC')
    .replace(/\uFE0F/g, '')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();
}

function srsTable(cache, key) {
  if (!cache[key]) {
    cache[key] = srsReadTab(key);
  }
  return cache[key];
}

function srsReadTab(key) {
  var spec = SRS_TABS[key];
  var out = { name: spec.name, hasChapter: spec.hasChapter, rows: [], error: '' };

  var sheet = SpreadsheetApp.getActive().getSheetByName(spec.name);
  if (!sheet) {
    out.error = 'tab "' + spec.name + '" not found';
    return out;
  }
  // Display values: a Time cell may hold a duration, and we want what the sheet shows.
  var values = sheet.getDataRange().getDisplayValues();
  var col = srsFindHeader(values, spec);
  if (!col) {
    out.error = 'no header row with a Time and a Date column in tab "' + spec.name + '"';
    return out;
  }
  out.sheet = sheet;
  out.col = col;

  var chapter = '';
  for (var r = col.header + 1; r < values.length; r++) {
    if (spec.hasChapter) {
      // Chapter cells are merged: only the first row of a group carries the label.
      var rawChapter = String(values[r][col.chapter] || '').trim();
      if (rawChapter) {
        chapter = rawChapter;
      }
    }
    var cp = String(values[r][col.cp] || '').trim();
    if (!cp) {
      continue;
    }
    out.rows.push({
      chapter: spec.hasChapter ? chapter : '',
      cp: cp,
      time: String(values[r][col.time] || ''),
      standard: col.standard < 0 ? '' : String(values[r][col.standard] || ''),
      row: r + 1 // 1-based sheet row
    });
  }
  return out;
}

/**
 * Locates the header by content, never by a hardcoded row index. A candidate is
 * accepted only once Time and Date are found to its right: the Farewell tab
 * carries a rank tally whose own header holds a "CP" cell six columns before the
 * real one, and returning on the first match locked onto it and reported the
 * whole tab as broken. Standard stays optional, srsReadTab tolerates its absence.
 */
function srsFindHeader(values, spec) {
  for (var i = 0; i < values.length; i++) {
    for (var j = 0; j < values[i].length; j++) {
      var cpCol = -1;
      var chapterCol = -1;
      if (spec.hasChapter) {
        if (srsNorm(values[i][j]) !== 'chapter' || srsNorm(values[i][j + 1]) !== 'checkpoint') {
          continue;
        }
        chapterCol = j;
        cpCol = j + 1;
      } else {
        if (srsNorm(values[i][j]) !== 'cp') {
          continue;
        }
        cpCol = j;
      }
      var col = { header: i, chapter: chapterCol, cp: cpCol, time: -1, standard: -1, date: -1 };
      for (var k = cpCol + 1; k < values[i].length; k++) {
        var name = srsNorm(values[i][k]);
        if (name === 'time' && col.time < 0) col.time = k;
        if (name === 'standard' && col.standard < 0) col.standard = k;
        if (name === 'date' && col.date < 0) col.date = k;
      }
      if (col.time >= 0 && col.date >= 0) {
        return col;
      }
    }
  }
  return null;
}

function srsWriteOne(cache, update) {
  var out = {
    tab: String(update.tab == null ? '' : update.tab),
    chapter: String(update.chapter == null ? '' : update.chapter),
    cp: String(update.cp == null ? '' : update.cp),
    status: 'refused',
    reason: ''
  };
  var time = String(update.time == null ? '' : update.time).trim();

  var key = srsNorm(out.tab);
  if (!SRS_TABS[key]) {
    out.reason = 'unknown tab "' + out.tab + '"; expected A Sides, B+C Sides or Farewell';
    return out;
  }
  var table = srsTable(cache, key);
  out.tab = table.name;
  if (table.error) {
    out.reason = table.error;
    return out;
  }
  if (!out.cp.trim()) {
    out.reason = 'empty checkpoint';
    return out;
  }
  if (!time) {
    out.reason = 'empty time';
    return out;
  }
  if (table.hasChapter && !out.chapter.trim()) {
    out.reason = 'missing chapter';
    return out;
  }

  var wantChapter = srsNorm(out.chapter);
  var wantCp = srsNorm(out.cp);
  var matches = table.rows.filter(function (r) {
    // Farewell has no chapter column: matched on CP alone.
    return srsNorm(r.cp) === wantCp && (!table.hasChapter || srsNorm(r.chapter) === wantChapter);
  });

  // not out.chapter: on Farewell the caller may send one and the match ignores
  // it, so naming it in the refusal points at the wrong thing
  var label = srsLabel(table.hasChapter ? out.chapter : '', out.cp);
  if (matches.length === 0) {
    out.status = 'notFound';
    out.reason = 'no row matching ' + label + ' in tab "' + table.name + '"';
    return out;
  }
  if (matches.length > 1) {
    out.status = 'ambiguous';
    out.reason = matches.length + ' rows match ' + label + ' in tab "' + table.name + '"';
    return out;
  }

  var row = matches[0].row;
  if (table.sheet.getRange(row, table.col.time + 1).getFormula()) {
    out.reason = 'Time cell of ' + label + ' holds a formula; overwriting it would break the sheet';
    return out;
  }

  // Write the time as typed: plain setValue is exactly what a player entering it would get.
  // Standard is a formula and is never touched.
  table.sheet.getRange(row, table.col.time + 1).setValue(time);
  table.sheet.getRange(row, table.col.date + 1).setValue(srsToday());
  out.status = 'written';
  return out;
}

function srsLabel(chapter, cp) {
  return chapter ? chapter + ' / ' + cp : cp;
}

/** The sheet's own auto-fill is an onEdit trigger and does not fire for script writes. */
// A real Date, not a formatted string: the cell keeps whatever date format the
// sheet already gives it, and anything reading it as a date still can. The
// sheet's own auto-fill cannot do this for us — an onEdit trigger, simple or
// installable, never fires for a write made by a script.
function srsToday() {
  var now = new Date();
  return new Date(now.getFullYear(), now.getMonth(), now.getDate());
}

function srsJson(payload) {
  return ContentService
    .createTextOutput(JSON.stringify(payload))
    .setMimeType(ContentService.MimeType.JSON);
}
