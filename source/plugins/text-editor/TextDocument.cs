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
			
			DoResetURL(fileURL());
			m_controller.OnPathChanged();
			
			if (NSObject.IsNullOrNil(m_text))
			{
				m_controller.Text = string.Empty;		// will be null if we're opening a new doc
			}
			else
			{
				m_controller.RichText = m_text;
				m_text.release();
				m_text = null;
			}
			m_controller.Open();
		}
		
		public bool HasChangedOnDisk()
		{
			Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
			
			bool changed = false;
			
			NSURL url = fileURL();
			if (!NSObject.IsNullOrNil(url))
			{
				NSDate docTime = fileModificationDate();
				
				NSDictionary attrs = NSFileManager.defaultManager().fileAttributesAtPath_traverseLink(url.path(), true);
				NSDate fileTime = attrs.objectForKey(Externs.NSFileModificationDate).To<NSDate>();
				
				if (fileTime != null && fileTime.compare(docTime) == Enums.NSOrderedDescending)
				{
					changed = true;
				}
				else
				{
					// The modification date is a bit too coarse to work properly if the file is being
					// written to as we are trying to reload it so we need to also check the file size.
					NSNumber size = attrs.objectForKey(Externs.NSFileSize).To<NSNumber>();
					if (size != null && m_size != size.unsignedIntValue())
						changed = true;
				}
			}
			
			if (changed)
				Log.WriteLine(TraceLevel.Info, "App", "{0:D} changed on disk", url);
			
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
			
			if (!NSObject.IsNullOrNil(m_text))
			{
				m_text.release();
				m_text = null;
			}
			m_controller.Open();
		}
		
		// This is called every time the document is saved...
		public new void setFileURL(NSURL url)
		{
			Unused.Value = SuperCall("setFileURL:", url);
			
			if (m_controller != null && url != m_url)
			{
				DoResetURL(url);
				m_controller.OnPathChanged();
			}
		}
		
		public bool readFromData_ofType_error(NSData data, NSString typeName, IntPtr outError)
		{
			bool read = true;
			
			try
			{
				Contract.Assert(NSObject.IsNullOrNil(m_text), "m_text is not null");
				
				switch (typeName.description())
				{
					// Note that this does not mean that the file is utf8, instead it means that the
					// file is our default document type which means we need to deduce the encoding.
					case "Plain Text, UTF8 Encoded":
					case "HTML":
						Boss boss = ObjectModel.Create("TextEditorPlugin");
						var encoding = boss.Get<ITextEncoding>();
						m_text = ApplyStyles.GetDefaultStyledString(encoding.Decode(data).description());
						break;
					
					// These types are based on the file's extension so we can (more or less) trust them.
					case "Rich Text Format (RTF)":
						m_text = DoReadWrapped(data, Externs.NSRTFTextDocumentType);
						break;
						
					case "Word 97 Format (doc)":
						m_text = DoReadWrapped(data, Externs.NSDocFormatTextDocumentType);
						break;
						
					case "Word 2007 Format (docx)":
						m_text = DoReadWrapped(data, Externs.NSOfficeOpenXMLTextDocumentType);
						break;
						
					case "Open Document Text (odt)":
						m_text = DoReadWrapped(data, Externs.NSOpenDocumentTextDocumentType);
						break;
					
					default:
						Contract.Assert(false, "bad typeName: " + typeName.description());
						break;
				}
				
				if (NSObject.IsNullOrNil(m_text))
					throw new InvalidOperationException("Couldn't decode the file.");
				
				if (m_text != null)
					DoCheckForControlChars(m_text.string_().description());
					
				m_size = data.length();
				
				if (m_controller != null)			// will be null for initial open, but non-null for revert
				{
					m_controller.RichText = m_text;
					m_text = null;
				}
				else
					m_text.retain();
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
				
				switch (typeName.description())
				{
					case "Plain Text, UTF8 Encoded":
						data = DoGetEncodedString(Enums.NSUTF8StringEncoding);
						break;
						
					case "Plain Text, UTF16 Encoded":
						data = DoGetEncodedString(Enums.NSUTF16LittleEndianStringEncoding);
						break;
						
					case "Plain Text, 7-Bit ASCII Encoded":
						data = DoGetEncodedString(Enums.NSASCIIStringEncoding);
						break;
						
					case "Rich Text Format (RTF)":
						data = DoWriteWrapped(Externs.NSRTFTextDocumentType);
						break;
						
					case "HTML":
						data = DoWriteWrapped(Externs.NSHTMLTextDocumentType);
						break;
					
					case "Word 97 Format (doc)":
						data = DoWriteWrapped(Externs.NSDocFormatTextDocumentType);
						break;
						
					case "Word 2007 Format (docx)":
						data = DoWriteWrapped(Externs.NSOfficeOpenXMLTextDocumentType);
						break;
						
					case "Open Document Text (odt)":
						data = DoWriteWrapped(Externs.NSOpenDocumentTextDocumentType);
						break;
					
					default:
						Contract.Assert(false, "bad typeName: " + typeName.description());
						break;
				}
				
				m_size = data.length();
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
		private void DoResetURL(NSURL url)
		{
			if (url != m_url)
			{
				if (m_url != null)
					m_url.release();
				
				m_url = url;
				
				if (m_url != null)
					m_url.retain();
			}
		}
		
		private NSData DoGetEncodedString(uint encoding)
		{
			NSString str = m_controller.TextView.string_();
			return str.dataUsingEncoding_allowLossyConversion(encoding, true);
		}
		
		private NSData DoWriteWrapped(NSString type)
		{
			NSAttributedString str = m_controller.TextView.textStorage();
			
			NSRange range = new NSRange(0, (int) str.length());
			NSDictionary dict = NSDictionary.dictionaryWithObject_forKey(type, Externs.NSDocumentTypeDocumentAttribute);
			NSError error;
			NSData result = str.dataFromRange_documentAttributes_error(range, dict, out error);
			if (!NSObject.IsNullOrNil(error))
				error.Raise();
			
			return result;
		}
		
		private NSAttributedString DoReadWrapped(NSData data, NSString type)
		{
			NSDictionary options = NSDictionary.dictionaryWithObject_forKey(type, Externs.NSDocumentTypeDocumentAttribute);
			NSError error;
			NSAttributedString str = NSAttributedString.Alloc().initWithData_options_documentAttributes_error(data, options, IntPtr.Zero, out error).To<NSAttributedString>();
			if (!NSObject.IsNullOrNil(error))
				error.Raise();
				
			str.autorelease();
			
			return str;
		}
		
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
				if ((int) ch < 0x20 || (int) ch == 0x7F)
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
		private NSAttributedString m_text;
		private NSURL m_url;
		private uint m_size;
		
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
			{'\x7F', "del"},
		};
		#endregion
	}
}
