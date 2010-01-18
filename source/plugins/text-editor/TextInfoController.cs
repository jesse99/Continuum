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

using Gear;
using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;

namespace TextEditor
{
	[ExportClass("TextInfoController", "NSWindowController", Outlets = "endian encoding format language")]
	internal sealed class TextInfoController : NSWindowController
	{
		public TextInfoController(TextDocument doc) : base(NSObject.AllocAndInitInstance("TextInfoController"))
		{
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("text-info"), this);
			
			m_endian = new IBOutlet<NSPopUpButton>(this, "endian").Value;
			m_endian.selectItemAtIndex((int) doc.Endian);
			
			NSPopUpButton format = new IBOutlet<NSPopUpButton>(this, "format").Value;
			format.selectItemAtIndex((int) doc.Format);
			
			m_encoding = new IBOutlet<NSPopUpButton>(this, "encoding").Value;
			m_encoding.selectItemAtIndex(-1);			// selectItemWithTag won't reset the selection if the tag isn't found
			m_encoding.selectItemWithTag(unchecked((int) doc.Encoding));
			
			Boss boss = ObjectModel.Create("Stylers");
			var finder = boss.Get<IFindLanguage>();
			NSPopUpButton language = new IBOutlet<NSPopUpButton>(this, "language").Value;
			NSMenu menu = language.menu();
			foreach (string name in finder.GetFriendlyNames())
			{
				menu.addItemWithTitle_action_keyEquivalent(
					NSString.Create(name), "languageChanged:", NSString.Empty);
			}
			if (doc.Controller.Language != null)
				language.selectItemWithTitle(NSString.Create(doc.Controller.Language.FriendlyName));
			else
				language.selectItem(null);
			
			m_doc = doc;
			NSURL url = doc.fileURL();
			if (!NSObject.IsNullOrNil(url))
			{
				NSString title = url.path().lastPathComponent() + " Info";
				window().setTitle(title);
			}
			DoEnableButtons();
			
			window().setDelegate(this);
			window().makeKeyAndOrderFront(this);
			
			ms_controllers.Add(this);
			ActiveObjects.Add(this);
		}
		
		public void windowWillClose(NSObject notification)
		{
			window().release();
			
			ms_controllers.Remove(this);
			autorelease();
		}
		
		public void endianChanged(NSPopUpButton sender)
		{
			m_doc.Endian = (LineEndian) sender.indexOfSelectedItem();
			DoEnableButtons();
		}
		
		public void formatChanged(NSPopUpButton sender)
		{
			m_doc.Format = (TextFormat) sender.indexOfSelectedItem();
			DoEnableButtons();
		}
		
		public void encodingChanged(NSPopUpButton sender)
		{
			m_doc.Encoding = unchecked((uint) sender.selectedItem().tag());
			DoEnableButtons();
		}
		
		public void languageChanged(NSMenuItem sender)
		{
			string name = sender.title().description();
			
			Boss boss = ObjectModel.Create("Stylers");
			var finder = boss.Get<IFindLanguage>();
			ILanguage language = finder.FindByFriendlyName(name);
			
			m_doc.Controller.Language = language;
		}
		
		#region Private Methods
		private void DoEnableButtons()
		{
			m_endian.setEnabled(m_doc.Format == TextFormat.PlainText);
			m_encoding.setEnabled(m_doc.Format == TextFormat.PlainText);
		}
		#endregion
		
		#region Fields
		private TextDocument m_doc;
		private NSPopUpButton m_endian;
		private NSPopUpButton m_encoding;
		
		private static List<TextInfoController> ms_controllers = new List<TextInfoController>();
		#endregion
	}
}
