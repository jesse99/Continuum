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

namespace App
{
	[ExportClass("PreferencesController", "NSWindowController", Outlets = "globsTable text_memberButton text_typeButton text_preprocessButton monoRoot lineWell transcriptWell errorsWell pathsController defaultWell spacesWell argsWell tabsWell contents tabs transcript_commandButton transcript_stdoutButton transcript_stderrButton errorsButton globalIgnores text_defaultButton text_keywordButton text_identifierButton text_stringButton text_numberButton text_commentButton text_other1Button text_other2Button")]
	internal sealed class PreferencesController : NSWindowController
	{
		public PreferencesController() : base(NSObject.AllocNative("PreferencesController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("Preferences"), this);	
			Unused.Value = window().setFrameAutosaveName(NSString.Create("preferences window"));
			
			m_tabs = new IBOutlet<NSTabView>(this, "tabs");
			this["monoRoot"].Call("setDoubleAction:", new Selector("changeMonoRoot:"));
			
			m_contents = new IBOutlet<NSTableView>(this, "contents");
			m_contents.Value.setDelegate(this);
			m_contents.Value.setDataSource(this);
			
			NSText text = this["globalIgnores"].To<NSText>();
			text.setDelegate(this);
			
			DoInitFontButton("errors");
			
			DoInitFontButton("transcript command");
			DoInitFontButton("transcript stdout");
			DoInitFontButton("transcript stderr");
			
			DoInitFontButton("text default");
			DoInitFontButton("text keyword");
			DoInitFontButton("text member");
			DoInitFontButton("text type");
			DoInitFontButton("text preprocess");
			DoInitFontButton("text string");
			DoInitFontButton("text number");
			DoInitFontButton("text comment");
			DoInitFontButton("text other1");
			DoInitFontButton("text other2");
			
			DoInitWell("text default");
			DoInitWell("text spaces");
			DoInitWell("text tabs");
			DoInitWell("selected line");
			DoInitWell("transcript");
			DoInitWell("errors");
			DoInitWell("args");
			
			this["globsTable"].Call("reload");		// TODO: for some reason our table isn't created until we call a method on it...
			
			ActiveObjects.Add(this);
		}
		
		public void setDefaultColor(NSObject sender)
		{
			NSColor color = sender.Call("color").To<NSColor>();
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			NSData data = NSArchiver.archivedDataWithRootObject(color);
			defaults.setObject_forKey(data, NSString.Create("text default color"));
			
			Broadcaster.Invoke("text default color changed", null);
		}
		
		public void changeMonoRoot(NSObject sender)
		{
			NSOpenPanel panel = NSOpenPanel.Create();
			panel.setTitle(NSString.Create("Choose Mono Root"));
			panel.setCanChooseDirectories(true);
			panel.setCanChooseFiles(false);
			panel.setAllowsMultipleSelection(false);
			
			int button = panel.runModal();
			if (button == Enums.NSOKButton && panel.filenames().count() > 0)
			{
				NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
				defaults.setObject_forKey(panel.filenames().lastObject(), NSString.Create("mono_root"));
				
				Broadcaster.Invoke("mono_root changed", null);
			}
		}
		
		public void setSpacesColor(NSObject sender)
		{
			NSColor color = sender.Call("color").To<NSColor>();
			DoSetBackColor("text spaces", color, true);
		}
		
		public void setTabsColor(NSObject sender)
		{
			NSColor color = sender.Call("color").To<NSColor>();
			DoSetBackColor("text tabs", color, true);
		}
		
		public void setArgsColor(NSObject sender)
		{
			NSColor color = sender.Call("color").To<NSColor>();
			DoSetBackColor("args", color, false);
		}
		
		public void setLineColor(NSObject sender)
		{
			NSColor color = sender.Call("color").To<NSColor>();
			DoSetBackColor("selected line", color, false);
		}
		
		public void setErrorsFont(NSObject sender)
		{
			DoEditFont("errors", "errors");
		}
		
		public void setTranscriptColor(NSObject sender)
		{
			NSColor color = sender.Call("color").To<NSColor>();
			DoSetBackColor("transcript", color, false);
		}
		
		public void setErrorsColor(NSObject sender)
		{
			NSColor color = sender.Call("color").To<NSColor>();
			DoSetBackColor("errors", color, false);
		}
		
		public void setCommandFont(NSObject sender)
		{
			DoEditFont("transcript", "transcript command");
		}
		
		public void setStdoutFont(NSObject sender)
		{
			DoEditFont("transcript", "transcript stdout");
		}
		
		public void setStderrFont(NSObject sender)
		{
			DoEditFont("transcript", "transcript stderr");
		}
		
		public void setDefaultFont(NSObject sender)
		{
			DoEditFont("text", "text default");
		}
		
		public void setKeywordFont(NSObject sender)
		{
			DoEditFont("text", "text keyword");
		}
		
		public void setTypeFont(NSObject sender)
		{
			DoEditFont("text", "text type");
		}
		
		public void setMemberFont(NSObject sender)
		{
			DoEditFont("text", "text member");
		}
		
		public void setPreprocessFont(NSObject sender)
		{
			DoEditFont("text", "text preprocess");
		}
		
		public void setStringFont(NSObject sender)
		{
			DoEditFont("text", "text string");
		}
		
		public void setNumberFont(NSObject sender)
		{
			DoEditFont("text", "text number");
		}
		
		public void setCommentFont(NSObject sender)
		{
			DoEditFont("text", "text comment");
		}
		
		public void setOther1Font(NSObject sender)
		{			
			DoEditFont("text", "text other1");
		}
		
		public void setOther2Font(NSObject sender)
		{			
			DoEditFont("text", "text other2");
		}
		
		public void changeFont(NSObject sender)
		{
			m_font = NSFontManager.sharedFontManager().convertFont(m_font);
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			defaults.setObject_forKey(m_font.fontName(), NSString.Create(m_styleName + " font name"));
			defaults.setFloat_forKey(m_font.pointSize(), NSString.Create(m_styleName + " font size"));
			
			DoUpdateButtonTitle(m_styleName);
			Broadcaster.Invoke(m_styleName + " font changed-pre", true);
			Broadcaster.Invoke(m_styleName + " font changed", true);
		}
		
		public void changeAttributes(NSObject sender)
		{
			m_attributes = sender.Call("convertAttributes:", m_attributes).To<NSDictionary>();
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			NSData data = NSArchiver.archivedDataWithRootObject(m_attributes);
			defaults.setObject_forKey(data, NSString.Create(m_styleName + " font attributes"));
			
			Broadcaster.Invoke(m_styleName + " font changed-pre", true);
			Broadcaster.Invoke(m_styleName + " font changed", true);
		}
		
		public void restoreDefaults(NSObject sender)
		{
			NSUserDefaultsController.sharedUserDefaultsController().revertToInitialValues(this);
			
			Broadcaster.Invoke("global ignores changed", null);
			
			foreach (string name in m_buttons.Keys)
			{
				Broadcaster.Invoke(name + " font changed-pre", false);
				Broadcaster.Invoke(name + " font changed", false);
				DoUpdateButtonTitle(name);
			}
			
			this["globsTable"].Call("reload");
			Broadcaster.Invoke("language globs changed", null);
			
			Broadcaster.Invoke("text default font changed-pre", true);		// small hack to avoid redoing attributes umpteen times for text documents
			Broadcaster.Invoke("text spaces color changed-pre", true);
			Broadcaster.Invoke("text tabs color changed-pre", true);

			Broadcaster.Invoke("text default font changed", true);
			Broadcaster.Invoke("text default color changed", null);
			Broadcaster.Invoke("transcript default color changed", null);
			Broadcaster.Invoke("text spaces color changed", true);
			Broadcaster.Invoke("text tabs color changed", true);
			Broadcaster.Invoke("transcript color changed", true);
			Broadcaster.Invoke("errors color changed", true);
			
			DoInitWell("text default");
			DoInitWell("text spaces");
			DoInitWell("text tabs");
			DoInitWell("selected line");
			DoInitWell("transcript");
			DoInitWell("errors");
			DoInitWell("args");
		}
		
		public void textDidChange(NSObject sender)
		{
			Broadcaster.Invoke("global ignores changed", null);
		}
		
		public void tabStopsChanged(NSObject sender)
		{
			Broadcaster.Invoke("tab stops changed-pre", true);
			Broadcaster.Invoke("tab stops changed", true);
		}
		
		public void addPreferred(NSObject sender)
		{
			NSOpenPanel panel = NSOpenPanel.Create();
			panel.setTitle(NSString.Create("Choose Directory"));
			panel.setCanChooseDirectories(true);
			panel.setCanChooseFiles(false);
			panel.setAllowsMultipleSelection(true);
			
			int button = panel.runModal();
			if (button == Enums.NSOKButton)
			{
				var controller = this["pathsController"].To<NSArrayController>();
				
				foreach (NSString path in panel.filenames())
				{
					controller.addObject(path);
				}
			}
		}
		
		public void removePreferred(NSObject sender)
		{
			var controller = this["pathsController"].To<NSArrayController>();
			
			NSArray objects = controller.selectedObjects();
			if (objects.count() > 0)
				controller.removeObjects(objects);
		}
		
		public NSObject tableView_objectValueForTableColumn_row(NSTableView table, NSTableColumn col, int tag)
		{			
			switch (tag)			// note that if the number of rows changes numberOfRowsInTableView has to be updated as well
			{
				case 0:
					return NSString.Create("Text Attributes");
				
				case 1:
					return NSString.Create("Text Background");
				
				case 2:
					return NSString.Create("Transcript Attributes");
				
				case 3:
					return NSString.Create("Error Attributes");
				
				case 4:
					return NSString.Create("Ignored Targets");
				
				case 5:
					return NSString.Create("Open Selection");
				
				case 6:
					return NSString.Create("Environment");
				
				case 7:
					return NSString.Create("Language Globs");
				
				default:
					Contract.Assert(false, "bad tag");
					return NSString.Empty;
			}
		}
		
		public int numberOfRowsInTableView(NSTableView table)
		{
			return 8;
		}
		
		public void tableViewSelectionDidChange(NSNotification notification)
		{
			int row = m_contents.Value.selectedRow();
			NSString id = NSString.Create(row.ToString());
			m_tabs.Value.selectTabViewItemWithIdentifier(id);
		}
		
		#region Private Methods
		private void DoSetBackColor(string name, NSColor color, bool setAttrs)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			NSData data = NSArchiver.archivedDataWithRootObject(color);
			defaults.setObject_forKey(data, NSString.Create(name + " color"));
			
			if (setAttrs)
			{
				var attrs = NSDictionary.dictionaryWithObject_forKey(color, Externs.NSBackgroundColorAttributeName);
				data = NSArchiver.archivedDataWithRootObject(attrs);
				defaults.setObject_forKey(data, NSString.Create(name + " color attributes"));
			}
			
			Broadcaster.Invoke(name + " color changed-pre", true);
			Broadcaster.Invoke(name + " color changed", true);
		}
		
		private void DoEditFont(string textName, string styleName)
		{
//			m_textName = textName;
			m_styleName = styleName;
			
			// Save the font info.
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			NSString fname = defaults.stringForKey(NSString.Create(styleName + " font name"));
			float ptSize = defaults.floatForKey(NSString.Create(styleName + " font size"));
			NSFont font = NSFont.fontWithName_size(fname, ptSize);
			
			if (m_font != null)
				m_font.release();
			
			m_font = font.Retain();
			
			// Save the attribute info.
			var data = defaults.objectForKey(NSString.Create(styleName + " font attributes")).To<NSData>();
			NSDictionary attrs;
			if (!NSObject.IsNullOrNil(data))
				attrs = NSUnarchiver.unarchiveObjectWithData(data).To<NSDictionary>();
			else
				attrs = NSDictionary.Create();
			
			if (m_attributes != null)
				m_attributes.release();
			
			m_attributes = attrs.Retain();
			
			// Bring up the font panel.	
			NSFontManager.sharedFontManager().setSelectedFont_isMultiple(m_font, false);
			NSFontManager.sharedFontManager().setSelectedAttributes_isMultiple(m_attributes, false);	// TODO: this doesn't appear to work (the color in the panel isn't correct for example)
			NSFontManager.sharedFontManager().orderFrontFontPanel(this);
		}
		
		private void DoInitWell(string name)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			var data = defaults.objectForKey(NSString.Create(name + " color")).To<NSData>();
			var color = NSUnarchiver.unarchiveObjectWithData(data).To<NSColor>();
			
			if (name.StartsWith("text "))
				name = name.Substring("text ".Length);
			else if (name.StartsWith("selected "))
				name = name.Substring("selected ".Length);
			
			var well = new IBOutlet<NSColorWell>(this, name + "Well");
			well.Value.setColor(color);
		}
		
		private void DoUpdateButtonTitle(string styleName)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			var fname = defaults.stringForKey(NSString.Create(styleName + " font name"));
			var ptSize = defaults.floatForKey(NSString.Create(styleName + " font size"));
			var str = NSString.Create((int) ptSize + "-pt " + fname.description());
			
			NSMutableDictionary dict = NSMutableDictionary.Create();
			var data = defaults.objectForKey(NSString.Create(styleName + " font attributes")).To<NSData>();
			if (!NSObject.IsNullOrNil(data))
				dict.addEntriesFromDictionary(NSUnarchiver.unarchiveObjectWithData(data).To<NSDictionary>());
				
			var style = NSMutableParagraphStyle.Alloc().init().To<NSMutableParagraphStyle>();
			style.autorelease();
			style.setParagraphStyle(NSParagraphStyle.defaultParagraphStyle());
			style.setAlignment(Enums.NSCenterTextAlignment);
			dict.setObject_forKey(style, Externs.NSParagraphStyleAttributeName);
			
			var title = NSAttributedString.Alloc().initWithString_attributes(str, dict);
			title.autorelease();
			m_buttons[styleName].Value.setAttributedTitle(title);
		}
		
		private void DoInitFontButton(string name)
		{
			// Save a reference to the button.
			m_buttons.Add(name, new IBOutlet<NSButton>(this, name.Replace(' ', '_') + "Button"));
			
			// Initialize the button's title.
			DoUpdateButtonTitle(name);
		}
		#endregion
		
		#region Fields
		private IBOutlet<NSTableView> m_contents;
		private IBOutlet<NSTabView> m_tabs;
		private Dictionary<string, IBOutlet<NSButton>> m_buttons = new Dictionary<string, IBOutlet<NSButton>>();
//		private string m_textName;
		private string m_styleName;
		private NSFont m_font;
		private NSDictionary m_attributes;
		#endregion
	}
}
