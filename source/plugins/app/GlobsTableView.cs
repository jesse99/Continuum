// Copyright (C) 2009-2010 Jesse Jones
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

using Gear;
using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace App
{
	[ExportClass("GlobsTableView", "NSTableView", Outlets = "col2")]
	internal sealed class GlobsTableView : NSTableView, IObserver
	{
		private GlobsTableView(IntPtr instance) : base(instance)
		{
			// Get the menu used by the popup button cells.
			m_popup = this["col2"].Call("dataCell").To<NSPopUpButtonCell>();
			m_popup.retain();
			
			// Initialize the menu.
			DoPopulateMenu();
			
			// Hookup the table.
			setDataSource(this);
			
			setTarget(this);
			setAction("clicked:");
			
			Broadcaster.Register("languages changed", this);
			ActiveObjects.Add(this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "languages changed":
					DoPopulateMenu();
					reload();
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public void reload()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			var dict = defaults.objectForKey(NSString.Create("language globs2")).To<NSDictionary>();
			
			m_globs.Clear();
			foreach (var entry in dict)
			{
				string glob = entry.Key.description();
				NSString lang = entry.Value.To<NSString>();
				
				int index = Array.IndexOf(m_languages, lang.ToString());
				if (index < m_languages.Length)
					m_globs.Add(Tuple.Create(glob, index));
				else
					Console.Error.WriteLine("Couldn't find a language for glob: {0}, lang: {1:D}", glob, lang);
			}
			
			DoSortByGlobs();
			reloadData();
		}
		
		public void addGlob(NSObject sender)
		{
			var getter = new GetString{Title = "New Glob", Label = "Glob:"};
			string glob = getter.Run();
			if (!string.IsNullOrEmpty(glob))
			{
				if (!m_globs.Exists(g => g.Item1 == glob))
				{
					m_globs.Add(Tuple.Create(glob, 0));
					reloadData();
					
					int row = m_globs.Count - 1;
					var index = NSIndexSet.indexSetWithIndex((uint) row);
					selectRowIndexes_byExtendingSelection(index, false);
					scrollRowToVisible(row);
					
					DoSyncPref();
				}
				else
					Functions.NSBeep();
			}
		}
		
		public void removeGlob(NSObject sender)
		{
			NSIndexSet indexes = selectedRowIndexes();
			if (indexes.count() > 0)
			{
				uint index = indexes.lastIndex();
				while (index < Enums.NSNotFound)
				{
					m_globs.RemoveAt((int) index);
					index = indexes.indexLessThanIndex(index);
				}
				
				reloadData();
				DoSyncPref();
			}
		}
		
		public void clicked(NSObject sender)
		{
			if (clickedRow() < 0)
			{
				int col = clickedColumn();
				
				if (col == 0)
					DoSortByGlobs();
				else
					DoSortByLanguages();
				
				reloadData();
			}
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSTableColumn col, int row)
		{
			Contract.Requires(row >= 0, "row is negative");
			Contract.Requires(row < m_globs.Count, "row is too big");
			
			switch (col.identifier().description())
			{
				case "1":
					return NSString.Create(m_globs[row].Item1);
				
				case "2":
					return NSNumber.Create(m_globs[row].Item2);
				
				default:
					Contract.Assert(false, "bad col: " + col.identifier());
					return NSString.Empty;
			}
		}
		
		public void tableView_setObjectValue_forTableColumn_row(NSTableView table, NSObject value, NSTableColumn col, int row)
		{
			Contract.Requires(row >= 0, "row is negative");
			Contract.Requires(row < m_globs.Count, "row is too big");
			
			switch (col.identifier().description())
			{
				case "1":
					m_globs[row] = Tuple.Create(value.description(), m_globs[row].Item2);
					break;
				
				case "2":
					m_globs[row] = Tuple.Create(m_globs[row].Item1, value.Call("intValue").To<int>());
					break;
				
				default:
					Contract.Assert(false, "bad col: " + col.identifier());
					break;
			}
			
			DoSyncPref();
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return m_globs.Count;
		}
		
		#region Private Methods
		private void DoPopulateMenu()
		{
			Boss boss = ObjectModel.Create("Stylers");
			var finder = boss.Get<IFindLanguage>();
			m_languages = finder.GetFriendlyNames().ToArray();
			Array.Sort(m_languages);
			
			m_popup.removeAllItems();
			foreach (string language in m_languages)
			{
				m_popup.addItemWithTitle(NSString.Create(language));
			}
		}
		
		private void DoSyncPref()
		{
			NSMutableDictionary dict = NSMutableDictionary.Create();
			
			foreach (var entry in m_globs)
			{
				string language = DoGetLanguage(entry.Item2);
				if (entry.Item1.Length > 0)
					dict.setObject_forKey(NSString.Create(language), NSString.Create(entry.Item1));
			}
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			defaults.setObject_forKey(dict, NSString.Create("language globs2"));
			
			Broadcaster.Invoke("language globs changed", null);
		}
				
		// We sort using both fields because it looks a bit better and because it prevents the
		// fields in the other column from jumping around a bit when the same column is
		// resorted.
		private void DoSortByGlobs()
		{
			m_globs.Sort((lhs, rhs) =>
			{
				int result = 0;
				
				if (result == 0)
					result = lhs.Item1.CompareTo(rhs.Item1);
				
				if (result == 0)
					result = DoGetLanguage(lhs.Item2).CompareTo(DoGetLanguage(rhs.Item2));
					
				return result;
			});
		}
		
		private void DoSortByLanguages()
		{
			m_globs.Sort((lhs, rhs) =>
			{
				int result = 0;
				
				if (result == 0)
					result = DoGetLanguage(lhs.Item2).CompareTo(DoGetLanguage(rhs.Item2));
					
				if (result == 0)
					result = lhs.Item1.CompareTo(rhs.Item1);
				
				return result;
			});
		}
		
		private string DoGetLanguage(int row)
		{
			NSString item = m_popup.itemTitleAtIndex(row);
			return item.description();
		}
		#endregion
		
		#region Fields
		private List<Tuple<string, int>> m_globs = new List<Tuple<string, int>>();
		private NSPopUpButtonCell m_popup;
		private string[] m_languages;
		#endregion
	}
}
