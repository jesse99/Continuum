// Copyright (C) 2009 Jesse Jones
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using MCocoa;
using Shared;
using System;
using System.Diagnostics;
using System.IO;

namespace TextEditor
{
	// Used to persist the frame and scroll bar position for text windows.
	// (Normally something like setFrameAutosaveName would be used
	// instead, but we don't want to pollute the prefs file with potentially
	// thousands of entries).
	internal static class WindowDatabase
	{
		static WindowDatabase()
		{
			string path = Path.Combine(Paths.SupportPath, "databases");
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			
			path = Path.Combine(path, "Windows.db");
			ms_database = new Database(path, "windows");
			
			// TODO: might want to add an access time field and then use a 
			// thread to prune rows which have not been accessed in a long time.
			ms_database.Update("create tables", () =>
			{
				ms_database.Update(@"
					CREATE TABLE IF NOT EXISTS Windows(
						path TEXT NOT NULL PRIMARY KEY
							CONSTRAINT absolute_path CHECK(substr(path, 1, 1) = '/'),
						length INTEGER NOT NULL
							CONSTRAINT non_negative_length CHECK(length >= 0),
						frame TEXT NOT NULL
							CONSTRAINT no_empty_frame CHECK(length(frame) > 0),
						scrollers TEXT NOT NULL
							CONSTRAINT no_empty_scrollers CHECK(length(scrollers) > 0),
						selection TEXT NOT NULL
							CONSTRAINT no_empty_selection CHECK(length(selection) > 0)
					)");
			});
		}
		
		public static NSRect GetFrame(string path)
		{
			string sql = string.Format(@"
				SELECT frame
					FROM Windows
				WHERE path = '{0}'", path.Replace("'", "''"));
			string[][] rows = ms_database.QueryRows(sql);
			
			return rows.Length > 0 ? NSRect.Parse(rows[0][0]) : NSRect.Empty;
		}
		
		public static bool GetViewSettings(string path, ref int length, ref NSPoint origin, ref NSRange selection)
		{
			string sql = string.Format(@"
				SELECT length, scrollers, selection
					FROM Windows
				WHERE path = '{0}'", path.Replace("'", "''"));
			string[][] rows = ms_database.QueryRows(sql);
			
			if (rows.Length > 0)
			{
				length = int.Parse(rows[0][0]);
				origin = NSPoint.Parse(rows[0][1]);
				selection = NSRange.Parse(rows[0][2]);
			}
			
			return rows.Length > 0;
		}
		
		public static void Set(string path, NSRect frame, int length, NSPoint pos, NSRange selection)
		{
			ms_database.Update("update frame for " + path, () =>
			{
				string sql = string.Format(@"
					INSERT OR REPLACE INTO Windows VALUES ('{0}', '{1}', '{2}', '{3}', '{4}')",
						path.Replace("'", "''"),
						length.ToString(),
						frame.ToString("R"),
						pos.ToString("R"),
						selection.ToString("R"));
				
				ms_database.Update(sql);
			});
		}
		
		#region Fields
		private static Database ms_database;
		#endregion
	}
}
