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

using Gear;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TextEditor
{
	[ExportClass("TextDocument", "NSDocument")]
	internal sealed class TextDocument : NSDocument
	{
		private TextDocument(IntPtr instance) : base(instance)
		{
			ActiveObjects.Add(this);
		}
		
		public new void makeWindowControllers()
		{
			m_controller = new TextController();
			addWindowController(m_controller);
			m_controller.OnPathChanged();
			m_controller.Text = m_data ?? string.Empty;		// will be null if we're opening a new doc
			m_controller.Open();
			
			m_data = null;
		}
		
		public bool HasChangedOnDisk()
		{
			bool changed = false;
			
			NSURL url = fileURL();
			if (!NSObject.IsNullOrNil(url))
			{
				NSDate docTime = fileModificationDate();
				
				NSDictionary attrs = NSFileManager.defaultManager().fileAttributesAtPath_traverseLink(url.path(), true);
				NSDate fileTime = attrs.objectForKey(Externs.NSFileModificationDate).To<NSDate>();
				
				changed = fileTime != null && fileTime.compare(docTime) == Enums.NSOrderedDescending;
			}
			
			return changed;
		}
		
		public void Reload()
		{
			NSString type = fileType();
			NSURL url = fileURL();
			
			NSError err;
			bool read = revertToContentsOfURL_ofType_error(url, type, out err);
			if (!read)
			{
				Boss boss = ObjectModel.Create("Application");
				var transcript = boss.Get<ITranscript>();
				transcript.WriteLine(Output.Error, "Couldn't reload {0:D}:\n{1:D}.", url, err.localizedFailureReason());
			}
			
			m_data = null;
		}
		
		// This is called every time the document is saved...
		public new void setFileURL(NSURL url)
		{
			Unused.Value = SuperCall("setFileURL:", url);
			
			if (m_controller != null && url != m_url)
			{
				if (m_url != null)
				{
					m_controller.OnPathChanged();
					m_url.release();
				}
				
				m_url = url;
				
				if (m_url != null)
					m_url.retain();
			}
		}
		
		// Used to read the document.
		public bool readFromData_ofType_error(NSData data, NSString typeName, IntPtr outError)
		{
			bool read = true;
			
			try
			{
				Boss boss = ObjectModel.Create("TextEditorPlugin");
				var encoding = boss.Get<ITextEncoding>();
				var result = encoding.Decode(data);
				
				m_data = result.First.description();
				m_encoding = result.Second;
				
				DoCheckForControlChars(m_data);
				
				if (m_controller != null)			// will be null for initial open, but non-null for revert
				{
					m_controller.Text = m_data ?? string.Empty;
					m_data = null;
				}
			}
			catch (Exception e)
			{
				NSMutableDictionary userInfo = NSMutableDictionary.Create();
				userInfo.setObject_forKey(NSString.Create("Couldn't read the document data."), Externs.NSLocalizedDescriptionKey);
				userInfo.setObject_forKey(NSString.Create(e.Message), Externs.NSLocalizedFailureReasonErrorKey);
				
				NSObject error = NSError.errorWithDomain_code_userInfo(Externs.Cocoa3Domain, 1, userInfo);
				Marshal.WriteIntPtr(outError, error);
				
				read = false;
			}
			
			return read;
		}
		
		// Used to write the document.
		public NSData dataOfType_error(NSString typeName, IntPtr outError)
		{
			NSData data = null;
			
			try
			{
				DoCheckForControlChars(m_controller.Text);
				NSString s = NSString.Create(m_controller.Text);
				
				Boss boss = ObjectModel.Create("TextEditorPlugin");
				var encoding = boss.Get<ITextEncoding>();
				data = encoding.Encode(s, m_encoding);
			}
			catch (Exception e)
			{
				NSMutableDictionary userInfo = NSMutableDictionary.Create();
				userInfo.setObject_forKey(NSString.Create("Couldn't convert the document to NSData."), Externs.NSLocalizedDescriptionKey);
				userInfo.setObject_forKey(NSString.Create(e.Message), Externs.NSLocalizedFailureReasonErrorKey);
				
				NSObject error = NSError.errorWithDomain_code_userInfo(Externs.Cocoa3Domain, 1, userInfo);
				Marshal.WriteIntPtr(outError, error);
			}
			
			return data;
		}
		
		// Used to reload a document for which the file changed.
		public void reloadSheetDidEnd_returnCode_contextInfo(NSWindow sheet, int returnCode, IntPtr context)
		{
			if (returnCode == Enums.NSAlertDefaultReturn )
			{
				Reload();
			}
		}
		
		#region Private Methods
		// It is fairly rare for control characters to wind up in text files, but 
		// when it does happen it can be quite annoying, especially because they
		// cannot ordinarily be seen. So, if this happens we'll write a message 
		// to the transcript window to alert the user.
		private void DoCheckForControlChars(string text)
		{
			Dictionary<char, int> chars = DoFindControlChars(text);
				
			if (chars.Count > 0)
			{
				string path = fileURL().path().ToString();
				
				string mesg;
				if (chars.Count <= 2)
					mesg = string.Format("Found {0} in '{1}'.", DoCharsToString(chars), path);
				else
					mesg = string.Format("Found {0} control characters of {1} different types in '{2}'.", chars.Values.Sum(), chars.Count, path);
				
				Boss boss = ObjectModel.Create("Application");
				var transcript = boss.Get<ITranscript>();
				transcript.WriteLine(Output.Error, mesg);
			}
		}
		
		private string DoCharsToString(Dictionary<char, int> chars)
		{
			string[] strs = new string[chars.Count];
			
			bool plural = chars.Count > 1;
			
			int i = 0;
			foreach (var entry in chars)
			{
				strs[i++] = string.Format("{0} '\\x{1:X2}' ({2})", entry.Value, (int) entry.Key, ms_controlNames[entry.Key]);
			
				if (entry.Value > 1)
					plural = true;
			}
			
			return string.Join(" and ", strs) + (plural ? " characters" : " character");
		}
		
		private Dictionary<char, int> DoFindControlChars(string text)
		{
			var chars = new Dictionary<char, int>();
			
			foreach (char ch in text)
			{
				if ((int) ch < 0x20)
				{
					if (ch != '\t' && ch != '\n' && ch != '\r')
					{
						if (chars.ContainsKey(ch))
							chars[ch] = chars[ch] + 1;
						else
							chars[ch] = 1;
					}
				}
			}
			
			return chars;
		}
		#endregion
		
		#region Fields
		private TextController m_controller;
		private string m_data;
		private uint m_encoding = Enums.NSUTF8StringEncoding;		// TODO: should allow this to be set so that the file can be written to other encodings (and store this in a pref?)
		private NSURL m_url;
		
		private static Dictionary<char, string> ms_controlNames = new Dictionary<char, string>
		{
			{'\x00', "nul"},
			{'\x01', "soh"},
			{'\x02', "stx"},
			{'\x03', "etx"},
			{'\x04', "eot"},
			{'\x05', "enq"},
			{'\x06', "ack"},
			{'\x07', "bel"},
			{'\x08', "bs"},
			{'\x09', "ht"},
			{'\x0A', "nl"},
			{'\x0B', "vt"},
			{'\x0C', "np"},
			{'\x0D', "cr"},
			{'\x0E', "so"},
			{'\x0F', "si"},
			{'\x10', "dle"},
			{'\x11', "dc1"},
			{'\x12', "dc2"},
			{'\x13', "dc3"},
			{'\x14', "dc4"},
			{'\x15', "nak"},
			{'\x16', "syn"},
			{'\x17', "etb"},
			{'\x18', "can"},
			{'\x19', "em"},
			{'\x1A', "sub"},
			{'\x1B', "esc"},
			{'\x1C', "fs"},
			{'\x1D', "gs"},
			{'\x1E', "rs"},
			{'\x1F', "us"},
		};
		#endregion
	}
}
