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

using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;

namespace DirectoryEditor
{
	// Represents a file or directory in the directory editor outline view.
	[ExportClass("TableItem", "NSObject")]
	internal abstract class TableItem : NSObject
	{
		public TableItem(string path, DirectoryItemStyler styler, string type) : base(NSObject.AllocNative(type))
		{
			m_path = path;
			m_styler = styler;
			
			string name = System.IO.Path.GetFileName(path);
			m_name = NSMutableAttributedString.Create(name).Retain();
			
			var attrs = NSMutableDictionary.Create();
			OnSetNameAttributes(attrs, name);
			m_name.setAttributes_range(attrs, new NSRange(0, (int) m_name.length()));
		}
		
		public string Path
		{
			get {return m_path;}
		}
		
		public NSAttributedString Name
		{
			get {return m_name;}
		}
		
		// Note that this should be used instead of Count because it will not force
		// all children to be loaded.
		public abstract bool IsExpandable {get;}
		
		public abstract int Count {get;}
		
		public abstract TableItem this[int index] {get;}
		
		// Returns null if the item has no size (directories or deleted files).
		public abstract NSString Bytes {get;}
		
		// Returns the (open) item which matches the specified path or null
		// if no item was found.
		public virtual TableItem Find(NSString path)
		{
			if (Paths.AreEqual(path.description(), Path))
				return this;
			
			return null;
		}
		
		// This is called whenever a file is added, removed, or modified in any
		// directory beneath the root item. Returns true if the item changed.
		public abstract bool Reload(List<TableItem> added);
		
		#region Protected Methods
		protected override void OnDealloc()
		{
			if (m_name != null)
				m_name.release();
			
			base.OnDealloc();
		}
		
		protected abstract void OnSetNameAttributes(NSMutableDictionary attrs, string name);
		
		protected DirectoryItemStyler Styler
		{
			get {return m_styler;}
		}
		#endregion
		
		#region Fields
		private readonly string m_path;
		private readonly NSMutableAttributedString m_name;
		private readonly DirectoryItemStyler m_styler;
		#endregion
	}
}
