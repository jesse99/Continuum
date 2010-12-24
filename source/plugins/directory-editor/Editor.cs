// Copyright (C) 2007-2008 Jesse Jones
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
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DirectoryEditor
{
	internal sealed class Editor : IWindow, IDirectoryEditor
	{		
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public NSWindow Window
		{
			get {Contract.Requires(m_window != null, "window isn't set"); return m_window;}
			set {Contract.Requires(value != null, "value is null"); Contract.Requires(m_window == null, "window isn't null"); m_window = (NSWindow) value;}
		}
		
		public string Path
		{
			get
			{
				Contract.Requires(m_window != null, "window isn't set");
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				DirectoryController controller = (DirectoryController) m_window.windowController();
				return controller.Path;
			}
		}
		
		public string[] SelectedPaths()
		{
			Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
			
			var paths = new List<string>();
			
			DirectoryController controller = (DirectoryController) m_window.windowController();
			NSOutlineView table = controller.Table;
			
			foreach (uint row in table.selectedRowIndexes())
			{
				TableItem item = (TableItem) (table.itemAtRow((int) row));
				paths.Add(item.Path);
			}
			
			return paths.ToArray();
		}
		
		public bool IsIgnored(string name)
		{
			DirectoryController controller = (DirectoryController) m_window.windowController();
			return controller.IsIgnored(name);
		}
		
		public DateTime BuildStartTime
		{
			get
			{
				Contract.Requires(m_window != null, "window isn't set");
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				DirectoryController controller = (DirectoryController) m_window.windowController();
				return controller.BuildStartTime;
			}
		}
		
		public bool AddSpace
		{
			get
			{
				Contract.Requires(m_window != null, "window isn't set");
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				DirectoryController controller = (DirectoryController) m_window.windowController();
				return controller.AddSpace;
			}
		}
		
		public bool AddBraceLine
		{
			get
			{
				Contract.Requires(m_window != null, "window isn't set");
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				DirectoryController controller = (DirectoryController) m_window.windowController();
				return controller.AddBraceLine;
			}
		}
		
		public bool UseTabs
		{
			get
			{
				Contract.Requires(m_window != null, "window isn't set");
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				DirectoryController controller = (DirectoryController) m_window.windowController();
				return controller.UseTabs;
			}
		}
		
		public int NumSpaces
		{
			get
			{
				Contract.Requires(m_window != null, "window isn't set");
				Contract.Requires(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
				
				DirectoryController controller = (DirectoryController) m_window.windowController();
				return controller.NumSpaces;
			}
		}
		
		#region Fields 
		private Boss m_boss;
		private NSWindow m_window;
		#endregion
	}
}
