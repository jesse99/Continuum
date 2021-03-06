// Copyright (C) 2008-2011 Jesse Jones
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
using System.Runtime.InteropServices;
using System.Text;

// Allow deprecated methods so that we can continue to run on leopard.
#pragma warning disable 618

namespace TextEditor
{
	internal enum LineEndian
	{
		Mac,				// "\r"
		Unix,				// "\n"
		Windows,			// "\r\n"
	}
	
	internal enum TextFormat
	{
		PlainText,
		RTF,
		HTML,
		Word97,
		Word2007,
		OpenDoc,
	}
	
	[ExportClass("TextDocument", "NSDocument")]
	internal sealed class TextDocument : NSDocument
	{
		private TextDocument(IntPtr instance) : base(instance)
		{
			ActiveObjects.Add(this);
		}
		
		public new void makeWindowControllers()
		{
			MakeWindowControllers("TextEditor");
		}
		
		public void MakeWindowControllers(string bossName)
		{
			m_controller = new TextController(bossName);
			addWindowController(m_controller);
			m_controller.autorelease();
			
			DoResetURL(fileURL());
			m_controller.OnPathChanged();
			
			if (NSObject.IsNullOrNil(m_text))
			{
				m_controller.Text = string.Empty;		// m_text will be null if we're opening a new doc
			}
			else
			{
				m_controller.RichText = m_text;
				m_text.release();
				m_text = null;
			}
			m_controller.Open();
		}
		
		public TextController Controller
		{
			get {return m_controller;}
		}
		
		public LineEndian Endian
		{
			get {return m_endian;}
			set
			{
				if (value != m_endian)
				{
					m_endian = value;
					updateChangeCount(Enums.NSChangeDone);
				}
			}
		}
		
		// Valid only if the format is plain text.
		public uint Encoding
		{
			get {return m_encoding;}
			set
			{
				if (value != m_encoding)
				{
					m_encoding = value;
					updateChangeCount(Enums.NSChangeDone);
				}
			}
		}
		
		public TextFormat Format
		{
			get
			{
				TextFormat result = TextFormat.PlainText;
				
				if (!NSObject.IsNullOrNil(fileURL()))
				{
					switch (fileType().description())
					{
						case "Plain Text, UTF8 Encoded":
						case "binary":
							result = TextFormat.PlainText;
							break;
						
						case "HTML":
							result = TextFormat.HTML;
							break;
						
						case "Rich Text Format (RTF)":
							result = TextFormat.RTF;
							break;
							
						case "Word 97 Format (doc)":
							result = TextFormat.Word97;
							break;
							
						case "Word 2007 Format (docx)":
							result = TextFormat.Word2007;
							break;
							
						case "Open Document Text (odt)":
							result = TextFormat.OpenDoc;
							break;
						
						default:
							Contract.Assert(false, "bad fileType: " + fileType().description());
							break;
					}
				}
				
				return result;
			}
			set
			{
				if (value != Format)
				{
					switch (value)
					{
						case TextFormat.PlainText:
							setFileType(NSString.Create("Plain Text, UTF8 Encoded"));
							break;
						
						case TextFormat.HTML:
							setFileType(NSString.Create("HTML"));
							break;
						
						case TextFormat.RTF:
							setFileType(NSString.Create("Rich Text Format (RTF)"));
							break;
						
						case TextFormat.Word97:
							setFileType(NSString.Create("Word 97 Format (doc)"));
							break;
						
						case TextFormat.Word2007:
							setFileType(NSString.Create("Word 2007 Format (docx)"));
							break;
						
						case TextFormat.OpenDoc:
							setFileType(NSString.Create("Open Document Text (odt)"));
							break;
						
						default:
							Contract.Assert(false, "bad format: " + value);
							break;
					}
					updateChangeCount(Enums.NSChangeDone);
				}
			}
		}
		
		public bool isBinary()
		{
			return m_binary;
		}
		
		public void getInfo()
		{
			Unused.Value = new TextInfoController(this);
		}
		
		// TODO: The modification time resolution is rather coarse so when we get notified that the directory
		// the file in has changed we can't always tell that the file has changed. We used to fall back on 
		// comparing the file size with what we think the size should be, but that causes problems with
		// auto-save (especially on Lion). To do this right we'd have to figure out when auto-save starts
		// (easy), when it is done (hard unless maybe we use undocumented methods), and probably
		// suppress DoDirChanged while that is happening.
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
			}
			
			if (changed)
				Log.WriteLine(TraceLevel.Verbose, "App", "{0:D} changed on disk", url);
			
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
			Unused.Value = SuperCall(NSDocument.Class, "setFileURL:", url);
			
			if (m_controller != null && url != m_url)
			{
				DoResetURL(url);
				m_controller.OnPathChanged();
				
				Broadcaster.Invoke("document path changed", m_controller.Boss);
			}
		}
		
		public bool readFromData_ofType_error(NSData data, NSString typeName, IntPtr outError)
		{
			bool read = false;
			
			try
			{
				Contract.Assert(NSObject.IsNullOrNil(m_text), "m_text is not null");
				
				if (DoShouldOpen(data.length()))
				{
					DoReadData(data, typeName);
					if (NSObject.IsNullOrNil(m_text))
						throw new InvalidOperationException("Couldn't decode the file.");
					
					string text = m_text.string_().description();
					DoSetEndian(text);
					DoCheckForControlChars(m_text.string_());
					
					if (m_controller != null)			// will be null for initial open, but non-null for revert
					{
						m_controller.RichText = m_text;
						m_text = null;
					}
					else
						m_text.retain();
						
					read = true;
					Marshal.WriteIntPtr(outError, IntPtr.Zero);
				}
				else
				{
					NSObject error = NSError.errorWithDomain_code_userInfo(Externs.Cocoa3Domain, Enums.NSUserCancelledError, null);
					Marshal.WriteIntPtr(outError, error);
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "Couldn't open {0:D}", fileURL());
				Log.WriteLine(TraceLevel.Error, "App", "{0}", e);
				
				NSMutableDictionary userInfo = NSMutableDictionary.Create();
				userInfo.setObject_forKey(NSString.Create("Couldn't read the document data."), Externs.NSLocalizedDescriptionKey);
				userInfo.setObject_forKey(NSString.Create(e.Message), Externs.NSLocalizedFailureReasonErrorKey);
				
				NSObject error = NSError.errorWithDomain_code_userInfo(Externs.Cocoa3Domain, 1, userInfo);
				Marshal.WriteIntPtr(outError, error);
			}
			
			return read;
		}
		
		public new void saveDocument(NSObject sender)
		{
			bool saveAs = NSObject.IsNullOrNil(fileURL());
			Unused.Value = SuperCall(NSDocument.Class, "saveDocument:", sender);
			if (saveAs)
				Broadcaster.Invoke("saved new document window", m_controller.Boss);
			else
				Broadcaster.Invoke("saved document window", m_controller.Boss);
		}
		
		// Used to write the document.
		public NSData dataOfType_error(NSString typeName, IntPtr outError)
		{
			NSData data = null;
			
			try
			{
				DoCheckForControlChars(m_controller.TextView.textStorage().string_());
				
				NSMutableAttributedString astr = m_controller.TextView.textStorage().mutableCopy().To<NSMutableAttributedString>();
				NSMutableString str = astr.mutableString();
				astr.autorelease();
				
				DoRestoreEndian(str);
				
				switch (typeName.description())
				{
					case "Plain Text, UTF8 Encoded":
						// This is more like the default plain text type: when loading a document that is not
						// rtf or word or whatever this typename will be chosen via the plist. However the actual
						// encoding is inferred from the contents of the file (or set via the Get Info panel).
						data = str.dataUsingEncoding_allowLossyConversion(m_encoding, true);
						break;
					
					case "Plain Text, UTF16 Encoded":
						// This case is only used when the user selects save as and then the utf16 encoding.
						m_encoding = Enums.NSUTF16LittleEndianStringEncoding;
						data = str.dataUsingEncoding_allowLossyConversion(m_encoding, true);
						break;
					
					case "HTML":
						data = DoWriteWrapped(astr, Externs.NSHTMLTextDocumentType);
						break;
						
					case "Rich Text Format (RTF)":
						data = DoWriteWrapped(astr, Externs.NSRTFTextDocumentType);
						break;
						
					case "Word 97 Format (doc)":
						data = DoWriteWrapped(astr, Externs.NSDocFormatTextDocumentType);
						break;
						
					case "Word 2007 Format (docx)":
						data = DoWriteWrapped(astr, Externs.NSOfficeOpenXMLTextDocumentType);
						break;
						
					case "Open Document Text (odt)":
						data = DoWriteWrapped(astr, Externs.NSOpenDocumentTextDocumentType);
						break;
						
					default:
						Contract.Assert(false, "bad typeName: " + typeName.description());
						break;
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "App", "couldn't save:");
				Log.WriteLine(TraceLevel.Error, "App", "{0}", e);
				
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
		private bool DoShouldOpen(uint bytes)
		{
			const uint MaxBytes = 512*1024;		// I think this is around 16K lines of source
			
			bool open = true;
			
			if (bytes > MaxBytes)
			{
				string path = fileURL().path().description();
				string file = System.IO.Path.GetFileName(path);
				
				NSString title = NSString.Create(file);
				NSString message = NSString.Create("This file is over {0}K. Are you sure you want to open it?", bytes/1024);
				
				int button = Functions.NSRunAlertPanel(
					title,									// title
					message, 							// message
					NSString.Create("No"),	// default button
					NSString.Create("Yes"),	// alt button
					null);								// other button
					
				open = button == Enums.NSAlertAlternateReturn;
			}
			
			return open;
		}
		
		private void DoReadData(NSData data, NSString typeName)
		{
			switch (typeName.description())
			{
				// Note that this does not mean that the file is utf8, instead it means that the
				// file is our default document type which means we need to deduce the encoding.
				case "Plain Text, UTF8 Encoded":
				case "HTML":
					Boss boss = ObjectModel.Create("TextEditorPlugin");
					var encoding = boss.Get<ITextEncoding>();
					NSString str = encoding.Decode(data, out m_encoding);
					m_text = NSMutableAttributedString.Alloc().initWithString(str).To<NSMutableAttributedString>();
					
					if (m_encoding == Enums.NSMacOSRomanStringEncoding)
						DoEncodingWarning();
					
					// If an html file is being edited in Continuum then ensure that it is saved
					// as plain text. (To save a document as html the user needs to use save as
					// and explicitly select html).
					setFileType(NSString.Create("Plain Text, UTF8 Encoded"));
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
					
				// Open as Binary
				case "binary":
					m_text = NSMutableAttributedString.Create(data.bytes().ToText());
					m_binary = true;
					m_encoding = Enums.NSUTF8StringEncoding;
					break;
				
				default:
					Contract.Assert(false, "bad typeName: " + typeName.description());
					break;
			}
		}
		
		private void DoEncodingWarning()
		{
			Boss boss = ObjectModel.Create("Application");
			var transcript = boss.Get<ITranscript>();
			transcript.WriteLine(Output.Error, "Read the file as Mac OS Roman (it isn't utf-8, utf-16, or utf-32).");
		}
		
		private void DoSetEndian(string text)
		{
			int[] counts = new int[]{0, 0, 0};
			
			int windows = (int) LineEndian.Windows;
			int mac = (int) LineEndian.Mac;
			int unix = (int) LineEndian.Unix;
			
			// Find out how many line endings of each type the file has.
			int i = 0;
			while (i < text.Length)
			{
				if (i + 1 < text.Length && text[i] == '\r' && text[i + 1] == '\n')
				{
					counts[windows] += 1;
					i += 2;
				}
				else if (text[i] == '\r')
				{
					counts[mac] += 1;
					i += 1;
				}
				else if (text[i] == '\n')
				{
					counts[unix] += 1;
					i += 1;
				}
				else
				{
					i += 1;
				}
			}
			
			// Set the endian to whichever is the most common.
			if (counts[windows] > counts[mac] && counts[windows] > counts[unix])
				m_endian = LineEndian.Windows;
			
			else if (counts[mac] > counts[windows] && counts[mac] > counts[unix])
				m_endian = LineEndian.Mac;
			
			else
				m_endian = LineEndian.Unix;
				
			// To make life easier on ourselves text documents in memory are always
			// unix endian (this will also fixup files with mixed line endings).
			if (counts[windows] > 0)
				DoFixup("\r\n", "\n");
			
			if (counts[mac] > 0)
				DoFixup("\r", "\n");
		}
		
		private void DoRestoreEndian(NSMutableString str )
		{
			NSRange range = new NSRange(0, (int) str.length());
			NSString target = NSString.Create("\n");
			
			if (m_endian == LineEndian.Windows)
			{
				NSString replacement = NSString.Create("\r\n");
				str.replaceOccurrencesOfString_withString_options_range(target, replacement, Enums.NSLiteralSearch, range);
			}
			else if (m_endian == LineEndian.Mac)
			{
				NSString replacement = NSString.Create("\r");
				str.replaceOccurrencesOfString_withString_options_range(target, replacement, Enums.NSLiteralSearch, range);
			}
		}
		
		private void DoFixup(string src, string dst)
		{
			NSMutableString str = m_text.mutableString();
			
			NSString target = NSString.Create(src);
			NSString replacement = NSString.Create(dst);
			NSRange range = new NSRange(0, (int) str.length());
			
			str.replaceOccurrencesOfString_withString_options_range(target, replacement, Enums.NSLiteralSearch, range);
		}
		
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
		
		private NSData DoWriteWrapped(NSAttributedString str, NSString type)
		{
			NSRange range = new NSRange(0, (int) str.length());
			NSDictionary dict = NSDictionary.dictionaryWithObject_forKey(type, Externs.NSDocumentTypeDocumentAttribute);
			NSError error;
			NSData result = str.dataFromRange_documentAttributes_error(range, dict, out error);
			if (!NSObject.IsNullOrNil(error))
				error.Raise();
			
			return result;
		}
		
		private NSMutableAttributedString DoReadWrapped(NSData data, NSString type)
		{
			NSMutableAttributedString str;
			
			if (data.length() > 0)
			{
				NSDictionary options = NSDictionary.dictionaryWithObject_forKey(type, Externs.NSDocumentTypeDocumentAttribute);
				NSError error;
				str = NSMutableAttributedString.Alloc().initWithData_options_documentAttributes_error(data, options, IntPtr.Zero, out error).To<NSMutableAttributedString>();
				if (!NSObject.IsNullOrNil(error))
					error.Raise();
				
				str.autorelease();
			}
			else
				str = NSMutableAttributedString.Create();
			
			return str;
		}
		
		// It is fairly rare for control characters to wind up in text files, but 
		// when it does happen it can be quite annoying, especially because they
		// cannot ordinarily be seen. So, if this happens we'll write a message 
		// to the transcript window to alert the user.
		private void DoCheckForControlChars(NSString text)
		{
			Dictionary<char, int> chars = DoFindControlChars(text);
				
			if (chars.Count > 0)
			{
				string path = fileURL().path().ToString();
				
				string mesg;
				if (chars.Count <= 5)
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
				if (ms_controlNames.ContainsKey(entry.Key))
					strs[i++] = string.Format("{0} '\\x{1:X2}' ({2})", entry.Value, (int) entry.Key, ms_controlNames[entry.Key]);
				else
					strs[i++] = string.Format("{0} '\\x{1:X2}' (?)", entry.Value, (int) entry.Key);
					
				if (entry.Value > 1)
					plural = true;
			}
			
			return string.Join(" and ", strs) + (plural ? " characters" : " character");
		}
		
		private Dictionary<char, int> DoFindControlChars(NSString str)
		{
			var chars = new Dictionary<char, int>();
			
			int len = (int) str.length();
			
			NSRange range = new NSRange(0, len);
			while (true)
			{
				NSRange temp = str.rangeOfCharacterFromSet_options_range(
					NSCharacterSet.controlCharacterSet(),
					Enums.NSLiteralSearch,
					range);
				if (temp.length == 0)
					break;
					
				char ch = str.characterAtIndex((uint) temp.location);
				if (ch != '\r' && ch != '\n' && ch != '\t' && ch != Constants.BOM[0])
				{
					if (chars.ContainsKey(ch))
						chars[ch] = chars[ch] + 1;
					else
						chars[ch] = 1;
				}
				
				range = new NSRange(temp.location + 1, len - (temp.location + 1));
			}
			
			return chars;
		}
		#endregion
		
		#region Fields
		private TextController m_controller;
		private NSMutableAttributedString m_text;
		private NSURL m_url;
		private bool m_binary;
		private LineEndian m_endian = LineEndian.Unix;
		private uint m_encoding;
		
		private static Dictionary<char, string> ms_controlNames = new Dictionary<char, string>
		{
			// See http://www.fileformat.info/info/unicode/category/Cc/list.htm
			{'\x00', "NULL"},
			{'\x01', "START OF HEADING"},
			{'\x02', "START OF TEXT"},
			{'\x03', "END OF TEXT"},
			{'\x04', "END OF TRANSMISSION"},
			{'\x05', "ENQUIRY"},
			{'\x06', "ACKNOWLEDGE"},
			{'\x07', "BELL"},
			{'\x08', "BACKSPACE"},
			{'\x09', "CHARACTER TABULATION"},
			{'\x0A', "LINE FEED"},
			{'\x0B', "LINE TABULATION"},
			{'\x0C', "FORM FEED"},
			{'\x0D', "CARRIAGE RETURN"},
			{'\x0E', "SHIFT OUT"},
			{'\x0F', "SHIFT IN"},
			{'\x10', "DATA LINK ESCAPE"},
			{'\x11', "DEVICE CONTROL ONE"},
			{'\x12', "DEVICE CONTROL TWO"},
			{'\x13', "DEVICE CONTROL THREE"},
			{'\x14', "DEVICE CONTROL FOUR"},
			{'\x15', "NEGATIVE ACKNOWLEDGE"},
			{'\x16', "SYNCHRONOUS IDLE"},
			{'\x17', "END OF TRANSMISSION BLOCK"},
			{'\x18', "CANCEL"},
			{'\x19', "END OF MEDIUM"},
			{'\x1A', "SUBSTITUTE"},
			{'\x1B', "ESCAPE"},
			{'\x1C', "INFORMATION SEPARATOR FOUR"},
			{'\x1D', "INFORMATION SEPARATOR THREE "},
			{'\x1E', "INFORMATION SEPARATOR TWO"},
			{'\x1F', "INFORMATION SEPARATOR ONE"},
			
			{'\x7F', "DELETE"},
			{'\x80', "unnamed control"},
			{'\x81', "unnamed control"},
			{'\x82', "BREAK PERMITTED HERE"},
			{'\x83', "NO BREAK HERE"},
			{'\x84', "unnamed control"},
			{'\x85', "NEXT LINE"},
			{'\x86', "START OF SELECTED AREA"},
			{'\x87', "END OF SELECTED AREA"},
			{'\x88', "CHARACTER TABULATION SET"},
			{'\x89', "CHARACTER TABULATION WITH JUSTIFICATION"},
			{'\x8A', "LINE TABULATION SET"},
			{'\x8B', "PARTIAL LINE FORWARD"},
			{'\x8C', "PARTIAL LINE BACKWARD"},
			{'\x8D', "REVERSE LINE FEED"},
			{'\x8E', "SINGLE SHIFT TWO"},
			{'\x8F', "SINGLE SHIFT THREE"},
			{'\x90', "DEVICE CONTROL STRING"},
			{'\x91', "PRIVATE USE ONE"},
			{'\x92', "PRIVATE USE TWO"},
			{'\x93', "SET TRANSMIT STATE"},
			{'\x94', "CANCEL CHARACTER"},
			{'\x95', "MESSAGE WAITING"},
			{'\x96', "START OF GUARDED AREA"},
			{'\x97', "END OF GUARDED AREA"},
			{'\x98', "START OF STRING"},
			{'\x99', "unnamed control"},
			{'\x9A', "SINGLE CHARACTER INTRODUCER"},
			{'\x9B', "CONTROL SEQUENCE INTRODUCER"},
			{'\x9C', "STRING TERMINATOR"},
			{'\x9D', "OPERATING SYSTEM COMMAND"},
			{'\x9E', "PRIVACY MESSAGE"},
			{'\x9F', "APPLICATION PROGRAM COMMAND"},
		};
		#endregion
	}
}
