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
using Mono.Unix;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DirectoryEditor
{
	// Represents a directory in the directory editor outline view.
	[ExportClass("FolderItem", "TableItem")]
	internal sealed class FolderItem : TableItem
	{
		public FolderItem(string path, DirectoryItemStyler styler, DirectoryController controller) : base(path, styler, "FolderItem")
		{
			m_controller = controller;
		}
		
		public override bool IsExpandable
		{
			get {return true;}
		}
		
		public override int Count
		{
			get
			{
				// Note that we want to defer loading children until we absolutely have
				// to so that we work better with large directory trees.
				if (m_children == null)
				{
					m_children = new List<TableItem>();
					DoReload(null);
				}
				
				return m_children.Count;
			}
		}
		
		public override TableItem this[int index]
		{
			get {return m_children[index];}
		}
		
		public override NSString Bytes
		{
			get {return null;}
		}
		
		public override TableItem Find(NSString path)
		{
			TableItem result = base.Find(path);
			
			if (m_children != null)
			{
				for (int i = 0; i < m_children.Count && result == null; ++i)
				{
					result = m_children[i].Find(path);
				}
			}
			
			return result;
		}
		
		public override bool Reload(List<TableItem> added)
		{
			bool changed = false;
			
			if (m_children != null)
			{
				changed = DoReload(added);
			}
			
			return changed;
		}
		
		#region Protected Methods
		protected override void OnSetNameAttributes(NSMutableDictionary attrs, string name)
		{
			NSColor color = Styler.GetPathColor();
			attrs.setObject_forKey(color, Externs.NSForegroundColorAttributeName);
			attrs.setObject_forKey(NSNumber.Create(-3.0f), Externs.NSStrokeWidthAttributeName);
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		protected override void OnDealloc()
		{
			if (m_children != null)
			{
				m_children.ForEach(c => c.release());
				m_children.Clear();
			}
			
			base.OnDealloc();
		}
		#endregion
		
		#region Private Methods
		private bool DoReload(List<TableItem> added)
		{
			bool changed = false;
			
			// Get the current state of the folder.
			string[] paths = DoGetPaths();
			
			// Remove items not in paths.
			for (int i = m_children.Count - 1; i >= 0; --i)
			{
				if (!paths.Any(p => p == m_children[i].CanonicalPath))
				{
					m_children[i].release();
					m_children.RemoveAt(i);
					changed = true;
				}
			}
			
			// Reload any existing items.
			foreach (TableItem item in m_children)
			{
				if (item.Reload(added))
					changed = true;
			}
			
			// Add any items from paths that are missing.
			foreach (string path in paths)
			{
				if (!m_children.Any(c => c.CanonicalPath == path))
				{
					TableItem item;
					if (System.IO.File.Exists(path) || NSWorkspace.sharedWorkspace().isFilePackageAtPath(NSString.Create(path)))
						item = new FileItem(path, Styler);
					else
						item = new FolderItem(path, Styler, m_controller);
					
					m_children.Add(item);
					if (added != null)
						added.Add(item);
					
					changed = true;
				}
			}
			
			// Sort them without using case and with packages treated as files.
			if (changed)
			{
				m_children.Sort((lhs, rhs) =>
				{
					bool file1 = lhs is FileItem;
					bool file2 = rhs is FileItem;
					
					if (file1 && !file2)
						return -1;
					else if (!file1 && file2)
						return +1;
					else
						return lhs.Path.ToLower().CompareTo(rhs.Path.ToLower());
				});
		}
			
			return changed;
		}
		
		private string[] DoGetPaths()
		{
			var children = new List<string>();
			
			try
			{
				foreach (string entry in System.IO.Directory.GetFileSystemEntries(Path))
				{
					if (!DoIsIgnored(System.IO.Path.GetFileName(entry)))
					{
						string path = UnixPath.GetCanonicalPath(entry);
						children.AddIfMissing(path);
					}
				}
			}
			catch (System.IO.IOException)
			{
				// If the file system is changing via another process we may land here.
			}
			
			return children.ToArray();
		}
		
		private bool DoIsIgnored(string name)
		{
			if (m_controller.IgnoredItems != null)
			{
				foreach (string glob in m_controller.IgnoredItems)
				{
					if (Glob.Match(glob, name))
						return true;
				}
			}
			
			return false;
		}
		#endregion
		
		#region Fields
		private List<TableItem> m_children;		// null => children have not been loaded
		private DirectoryController m_controller;
		#endregion
	}

	// Represents a file or package in the directory editor outline view.
	[ExportClass("FileItem", "TableItem")]
	internal sealed class FileItem : TableItem
	{
		public FileItem(string path, DirectoryItemStyler styler) : base(path, styler, "FileItem")
		{
			Reload(null);
		}
		
		public override bool IsExpandable
		{
			get {return false;}
		}
		
		public override int Count
		{
			get {return 0;}
		}
		
		public override TableItem this[int index]
		{
			get {throw new InvalidOperationException("files don't have children");}
		}
		
		public override NSString Bytes
		{
			get {return m_bytes;}
		}
		
		public override bool Reload(List<TableItem> added)
		{
			bool changed = false;
			
			try
			{
				// This check may not matter much for normal files but it should help
				// for packages because we have to recursively get the size for those.
				// (Bit it won't work quite right if a file buried within a package is changed
				// but that should rarely make a significant difference to the package size).
				DateTime time = System.IO.File.GetLastWriteTime(Path);
				if (time != m_writeTime)
				{
					if ((object) m_bytes != null)
						m_bytes.release();
					
					Boss boss = ObjectModel.Create("FileSystem");
					var fs = boss.Get<IFileSystem>();
					
					long bytes = fs.GetBytes(Path);
					m_bytes = NSString.Create(bytes.ToByteString()).Retain();
					
					m_writeTime = time;
					changed = true;
				}
			}
			catch
			{
				// We may land here if another process deleted the file while we were
				// inside the try block.
				changed = m_bytes != null;
				m_bytes = null;
			}
			
			return changed;
		}
		
		#region Protected Methods
		protected override void OnSetNameAttributes(NSMutableDictionary attrs, string name)
		{
			NSColor color = Styler.GetFileColor(name);
			attrs.setObject_forKey(color, Externs.NSForegroundColorAttributeName);
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		protected override void OnDealloc()
		{
			if ((object) m_bytes != null)
				m_bytes.release();
			
			base.OnDealloc();
		}
		#endregion
		
		#region Fields
		private NSString m_bytes;
		private DateTime m_writeTime;
		#endregion
	}
}
