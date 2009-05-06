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
using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DirectoryEditor
{
	[ExportClass("DirectoryItem", "NSObject")]
	internal sealed class DirectoryItem : NSObject
	{
		public DirectoryItem(string path, DirectoryItemStyler styler, string[] ignoredItems) : base(NSObject.AllocNative("DirectoryItem"))
		{
			m_path = path;
			m_styler = styler;
			m_ignoredItems = ignoredItems;
			
			string name = System.IO.Path.GetFileName(path);
			m_name = NSMutableAttributedString.Create(name).Retain();
			
			var attrs = NSMutableDictionary.Create();
			if (System.IO.File.Exists(m_path) || NSWorkspace.sharedWorkspace().isFilePackageAtPath(NSString.Create(m_path)))
			{
				NSColor color = m_styler.GetFileColor(name);
				attrs.setObject_forKey(color, Externs.NSForegroundColorAttributeName);
			}
			else
			{
				NSColor color = m_styler.GetPathColor();
				attrs.setObject_forKey(color, Externs.NSForegroundColorAttributeName);
				attrs.setObject_forKey(NSNumber.Create(-3.0f), Externs.NSStrokeWidthAttributeName);
			}
			m_name.setAttributes_range(attrs, new NSRange(0, (int) m_name.length()));
			
			Reset();	
			
			ActiveObjects.Add(this);
		}
		
		public void Close()
		{
			if (m_name != null)
			{
				m_name.release();
				m_name = null;
			}
			
			if ((object) m_bytes != null)
			{
				m_bytes.release();
				m_bytes = null;
			}
			
			if (m_children != null)
			{
				for (int i = 0; i < m_children.Length; ++i)
				{
					m_children[i].Close();
					m_children[i].release();
				}
				m_children = null;
			}
		}
		
		public void Reset()
		{
			if (m_loaded && m_children != null)
			{
				// This will be called if a file system item is modified, but we only
				// want to rebuild items if one is added or removed (this is more
				// efficient and prevents the NSOutlineView from collapsing items).
				List<string> fs = DoGetDirContents();
				var cached = from child in m_children select child.Path;
				var common = fs.Intersect(cached);
				
				if (common.Count() != fs.Count || common.Count() != cached.Count())
				{
					for (int i = 0; i < m_children.Length; ++i)
					{
						m_children[i].Close();
						m_children[i].release();
					}
					
					m_children = null;
					m_loaded = false;
				}
				else
				{
					foreach (var child in m_children)
					{
						child.Reset();
					}
				}
			}
			
			if ((object) m_bytes != null)
			{
				m_bytes.release();
				m_bytes = null;
			}
			
			if (System.IO.File.Exists(m_path) || NSWorkspace.sharedWorkspace().isFilePackageAtPath(NSString.Create(m_path)))
			{
				Boss boss = ObjectModel.Create("FileSystem");
				var fs = boss.Get<IFileSystem>();
				
				long bytes = fs.GetBytes(m_path);
				m_bytes = NSString.Create(bytes.ToByteString());
			}
			else
				m_bytes = NSString.Empty;
				
			Unused.Value = m_bytes.Retain();	
		}
		
		public int Count
		{
			get {DoGetChildren(); return m_children != null ? m_children.Length : -1;}
		}
		
		public DirectoryItem this[int index]
		{
			get {DoGetChildren(); return m_children[index];}
		}
			
		public string Path
		{
			get {return m_path;}
		}
		
		public NSAttributedString Name
		{
			get {return m_name;}
		}
		
		public NSString Bytes
		{
			get {return m_bytes;}
		}
		
		public DirectoryItem Find(NSString path)
		{
			if (Paths.AreEqual(path.description(), Path))
				return this;
			
			if (m_children != null)
			{
				foreach (DirectoryItem child in m_children)
				{
					DirectoryItem result = child.Find(path);
					if (result != null)
						return result;
				}
			}
			
			return null;
		}
		
		#region Private Methods
		private List<string> DoGetDirContents()
		{
			var children = new List<string>();
			
			try
			{
				foreach (string entry in System.IO.Directory.GetFileSystemEntries(m_path))
				{
					if (!DoIsIgnored(System.IO.Path.GetFileName(entry)))
						children.Add(entry);
				}
			}
			catch (System.IO.IOException)
			{
				// If the file system is changing via another process we may land here.
			}
			
			return children;
		}
		
		private bool DoIsIgnored(string name)
		{
			if (m_ignoredItems != null)
			{
				foreach (string glob in m_ignoredItems)
				{
					if (Glob.Match(glob, name))
						return true;
				}
			}
			
			return false;
		}
		
		private void DoGetChildren()
		{
			if (!m_loaded)
			{
				NSWorkspace workspace = NSWorkspace.sharedWorkspace();
				if (System.IO.Directory.Exists(m_path) && !workspace.isFilePackageAtPath(NSString.Create(m_path)))
				{
					// Get a list of all visible file system entries.
					List<string> children = DoGetDirContents();
					
					// Sort them without using case and with packages treated as files.
					children.Sort((lhs, rhs) =>
					{
						bool file1 = System.IO.File.Exists(lhs) || workspace.isFilePackageAtPath(NSString.Create(lhs));
						bool file2 = System.IO.File.Exists(rhs) || workspace.isFilePackageAtPath(NSString.Create(rhs));
						
						if (file1 && !file2)
							return -1;
						else if (!file1 && file2)
							return +1;
						else
							return lhs.ToLower().CompareTo(rhs.ToLower());
					});
					
					// Build the array.
					m_children = new DirectoryItem[children.Count];
					for (int i = 0; i < m_children.Length; ++i)
						m_children[i] = new DirectoryItem(children[i], m_styler, m_ignoredItems);
				}
				
				m_loaded = true;
			}
		}
		#endregion
		
		#region Fields
		private string m_path;
		private DirectoryItemStyler m_styler;
		private NSMutableAttributedString m_name;
		private NSString m_bytes;
		private DirectoryItem[] m_children;
		private string[] m_ignoredItems;
		private bool m_loaded;
		#endregion
	}
}	