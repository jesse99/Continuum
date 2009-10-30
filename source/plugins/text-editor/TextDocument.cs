// Copyright (C) 2008-2009 Jesse Jones
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

namespace TextEditor
{
	internal enum LineEndian
	{
		Mac,					// "\r"
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
			m_controller = new TextController();
			addWindowController(m_controller);
			m_controller.autorelease();
			
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
					if (size != null && size.unsignedIntValue() > m_size)
					{
						changed = true;
					}
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
					DoCheckForControlChars(text);
						
					m_size = data.length();
					
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
		
		// Used to write the document.
		public NSData dataOfType_error(NSString typeName, IntPtr outError)
		{
			NSData data = null;
			
			try
			{
				DoCheckForControlChars(m_controller.Text);
				
				NSMutableAttributedString astr = m_controller.TextView.textStorage().mutableCopy().To<NSMutableAttributedString>();
				NSMutableString str = astr.mutableString();
				astr.autorelease();
				
				DoRestoreEndian(str);
				
				switch (typeName.description())
				{
					case "Plain Text, UTF8 Encoded":
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
				
				m_size = data.length();
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
				if (CharHelpers.IsBadControl(ch))
				{
					if (chars.ContainsKey(ch))
						chars[ch] = chars[ch] + 1;
					else
						chars[ch] = 1;
				}
			}
			
			return chars;
		}
		#endregion
		
		#region Fields
		private TextController m_controller;
		private NSMutableAttributedString m_text;
		private NSURL m_url;
		private uint m_size;
		private bool m_binary;
		private LineEndian m_endian;
		private uint m_encoding;
		
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
