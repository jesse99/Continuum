// Copyright (C) 2008 Jesse Jones
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

using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Find
{
	[ExportClass("BaseFindController", "NSWindowController", Outlets = "findBox replaceEnabled findEnabled findList replaceList withinList findText replaceText withinText caseSensitive matchWords useRegex")]
	internal abstract class BaseFindController : NSWindowController
	{
		protected BaseFindController(IntPtr instance, string nibName) : base(instance)
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create(nibName), this);	
			
			m_findBox = new IBOutlet<NSComboBox>(this, "findBox");
			m_findEnabled = new IBOutlet<NSNumber>(this, "findEnabled");
			m_findEnabled.Value = NSNumber.Create(false);
			
			m_replaceEnabled = new IBOutlet<NSNumber>(this, "replaceEnabled");
			m_replaceEnabled.Value = NSNumber.Create(false);
			
			m_findText = new IBOutlet<NSString>(this, "findText");
			m_replaceText = new IBOutlet<NSString>(this, "replaceText");
			m_withinText = new IBOutlet<NSString>(this, "withinText");
			
			m_caseSensitive = new IBOutlet<NSNumber>(this, "caseSensitive");
			m_matchWords = new IBOutlet<NSNumber>(this, "matchWords");
			m_useRegex = new IBOutlet<NSNumber>(this, "useRegex");
			
			m_findList = new IBOutlet<NSMutableArray>(this, "findList");
			this.willChangeValueForKey(NSString.Create("findList"));
			m_findList.Value = NSMutableArray.Create();
			this.didChangeValueForKey(NSString.Create("findList"));
			
			m_replaceList = new IBOutlet<NSMutableArray>(this, "replaceList");
			this.willChangeValueForKey(NSString.Create("replaceList"));
			m_replaceList.Value = NSMutableArray.Create();
			this.didChangeValueForKey(NSString.Create("replaceList"));
			
			m_withinList = new IBOutlet<NSMutableArray>(this, "withinList");
			NSMutableArray items = NSMutableArray.Create();
			items.addObject(NSString.Create(Constants.Ellipsis));			// note that we can't do this within IB because we have bound the list
			items.addObject(NSString.Create("\"" + Constants.Ellipsis + "\""));
			items.addObject(NSString.Create("@\"" + Constants.Ellipsis + "\""));
			items.addObject(NSString.Create("(" + Constants.Ellipsis + ")"));
			items.addObject(NSString.Create("//" + Constants.Ellipsis));
			items.addObject(NSString.Create("/*" + Constants.Ellipsis + "*/"));
			items.addObject(NSString.Create("<!--" + Constants.Ellipsis + "-->"));
			this.willChangeValueForKey(NSString.Create("withinList"));
			m_withinList.Value = items;
			this.didChangeValueForKey(NSString.Create("withinList"));
			
			this.willChangeValueForKey(NSString.Create("withinText"));
			m_withinText.Value = NSString.Create(Constants.Ellipsis);
			this.didChangeValueForKey(NSString.Create("withinText"));
			
			this.addObserver_forKeyPath_options_context(
				this, NSString.Create("findText"), 0, IntPtr.Zero);
			this.addObserver_forKeyPath_options_context(
				this, NSString.Create("replaceText"), 0, IntPtr.Zero);
			NSNotificationCenter.defaultCenter().addObserver_selector_name_object(	// note that the controller doesn't go away so we don't bother removing ourself
				this, "mainWindowChanged:", Externs.NSWindowDidBecomeMainNotification, null);
			NSNotificationCenter.defaultCenter().addObserver_selector_name_object(
				this, "mainWindowChanged:", Externs.NSWindowDidResignMainNotification, null);
		}
		
		public void Open(IFind finder)
		{
			m_finder = finder;
			window().makeKeyAndOrderFront(null);
			Unused.Value = window().makeFirstResponder(m_findBox.Value);
			OnEnableButtons();
		}
		
		public string FindText
		{
			get {return !NSObject.IsNullOrNil(m_findText.Value) ? m_findText.Value.description() : string.Empty;}
			set
			{
				if (UseRegex)
				{
					value = Re.Escape(value);
					value = value.Replace("\t", @"\t");
				}
				
				this.willChangeValueForKey(NSString.Create("findText"));
				m_findText.Value = NSString.Create(value);
				this.didChangeValueForKey(NSString.Create("findText"));
				
				OnEnableButtons();
			}
		}
		
		public string ReplaceText
		{
			get
			{
				string result = !NSObject.IsNullOrNil(m_replaceText.Value) ? m_replaceText.Value.description() : string.Empty;
				
				result = result.Replace(@"\'", "'");		// strangely enough Regex deals with escape characters in the find text, but not in the replace text...
				result = result.Replace("\\\"", "\"");
				result = result.Replace("\\\\", "\\");
				result = result.Replace(@"\f", "\f");
				result = result.Replace(@"\n", "\n");
				result = result.Replace(@"\r", "\r");
				result = result.Replace(@"\t", "\t");
				result = result.Replace(@"\v", "\v");
				
				return result;
			}
			set
			{
				value = value.Replace("\\", "\\\\");
				if (UseRegex)
					value = value.Replace("$", "\\$");
				
				this.willChangeValueForKey(NSString.Create("replaceText"));
				m_replaceText.Value = NSString.Create(value);
				this.didChangeValueForKey(NSString.Create("replaceText"));
				
				OnEnableButtons();
			}
		}
		
		public bool CaseSensitive
		{
			get {return !NSObject.IsNullOrNil(m_caseSensitive.Value) ? m_caseSensitive.Value.boolValue() : false;}
		}
		
		public bool UseRegex
		{
			get {return !NSObject.IsNullOrNil(m_useRegex.Value) ? m_useRegex.Value.boolValue() : false;}
		}
		
		public bool MatchWords
		{
			get {return !NSObject.IsNullOrNil(m_matchWords.Value) ? m_matchWords.Value.boolValue() : false;}
		}
		
		public string WithinText
		{
			get {return !NSObject.IsNullOrNil(m_withinText.Value) ? m_withinText.Value.description() : string.Empty;}
		}
		
		public void mainWindowChanged(NSObject data)
		{
			OnEnableButtons();
		}
		
		public void observeValueForKeyPath_ofObject_change_context(NSString keyPath, NSObject obj, NSDictionary change, IntPtr context)
		{
			OnEnableButtons();
		}
		
		public void UpdateFindList()
		{
			NSString name = NSString.Create(FindText);
			if (FindText.Length > 0 && !m_findList.Value.containsObject(name))	
			{
				this.willChangeValueForKey(NSString.Create("findList"));
				m_findList.Value.insertObject_atIndex(name, 0);
				
				while (m_findList.Value.count() > 10)
					m_findList.Value.removeObjectAtIndex(m_findList.Value.count() - 1);
				
				this.didChangeValueForKey(NSString.Create("findList"));
			}
		}
		
		public void UpdateReplaceList()
		{
			NSString name = NSString.Create(ReplaceText);
			if (ReplaceText.Length > 0 && !m_replaceList.Value.containsObject(name))	
			{
				this.willChangeValueForKey(NSString.Create("replaceList"));
				m_replaceList.Value.insertObject_atIndex(name, 0);
				
				while (m_replaceList.Value.count() > 10)
					m_replaceList.Value.removeObjectAtIndex(m_replaceList.Value.count() - 1);
				
				this.didChangeValueForKey(NSString.Create("replaceList"));
			}
		}
		
		#region Protected Methods
		protected IFind Finder
		{
			get {return m_finder;}
		}
		
		protected virtual bool OnFindEnabled()
		{
			return FindText.Length > 0 && Finder != null && Finder.CanFind();
		}
		
		protected virtual bool OnReplaceEnabled()
		{
			return OnFindEnabled() && Finder != null && Finder.CanReplace();	
		}
		
		protected virtual void OnEnableButtons()
		{
			// find button
			this.willChangeValueForKey(NSString.Create("findEnabled"));
			m_findEnabled.Value = NSNumber.Create(OnFindEnabled());
			this.didChangeValueForKey(NSString.Create("findEnabled"));
			
			// replace buttons
			this.willChangeValueForKey(NSString.Create("replaceEnabled"));
			m_replaceEnabled.Value = NSNumber.Create(OnReplaceEnabled());
			this.didChangeValueForKey(NSString.Create("replaceEnabled"));
		}
		
		protected virtual void OnUpdateLists()
		{
			UpdateFindList();
			UpdateReplaceList();
		}
		#endregion
		
		#region Fields
		private IBOutlet<NSComboBox> m_findBox;
		private IBOutlet<NSNumber> m_findEnabled;
		private IBOutlet<NSNumber> m_replaceEnabled;
		
		private IBOutlet<NSString> m_findText;
		private IBOutlet<NSString> m_replaceText;
		private IBOutlet<NSString> m_withinText;
		private IBOutlet<NSNumber> m_caseSensitive;
		private IBOutlet<NSNumber> m_matchWords;
		private IBOutlet<NSNumber> m_useRegex;
		
		private IBOutlet<NSMutableArray> m_findList;
		private IBOutlet<NSMutableArray> m_replaceList;
		private IBOutlet<NSMutableArray> m_withinList;
		private IFind m_finder;
		#endregion
	}
}
