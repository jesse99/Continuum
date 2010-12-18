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
using Shared;
using System;
using System.Diagnostics;

namespace DirectoryEditor
{
	internal sealed class FindDirectoryEditor : IFindDirectoryEditor
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public Boss GetDirectoryEditor(Boss window)
		{
			Boss result = null;
			
			if (window != null)
			{
				if (window.Has<IDirectoryEditor>())
				{
					result = window;
				}
				else if (window.Has<ITextEditor>())
				{
					result = DoFindAssociatedBoss(window.Get<ITextEditor>());
				}
			}
			
			if (result == null)
			{
				result = DoFindDefaultBoss();
			}
			
			return result;
		}
		
		public Boss GetDirectoryEditor(string path)
		{
			if (!path.EndsWith("/"))
				path += "/";
				
			Boss plugin = ObjectModel.Create("DirectoryEditorPlugin");
			var windows = plugin.Get<IWindows>();
			foreach (Boss candidate in windows.All())
			{
				var editor = candidate.Get<IDirectoryEditor>();
				if (path.StartsWith(editor.Path))
					return candidate;
			}
			
			return null;
		}
		
		#region Private Methods
		private Boss DoFindAssociatedBoss(ITextEditor text)
		{
			if (text.Path != null)			// will be null if it is not on disk
			{
				var windows = m_boss.Get<IWindows>();
				
				foreach (Boss boss in windows.All())
				{
					var candidate = boss.Get<IDirectoryEditor>();
					if (text.Path.StartsWith(candidate.Path))
						return boss;
				}
			}
			
			return null;
		}
		
		private Boss DoFindDefaultBoss()
		{
			Boss result = null;
			
			Boss[] candidates = m_boss.Get<IWindows>().All();
			if (candidates.Length > 0)
			{
				DateTime time = DateTime.MinValue;
				
				foreach (Boss boss in candidates)
				{
					var candidate = boss.Get<IDirectoryEditor>();
					if (candidate.BuildStartTime >= time)
					{
						result = boss;
						time = candidate.BuildStartTime;
					}
				}
			}
			else if (candidates.Length == 1)
			{
				result = candidates[0];
			}
			
			return result;
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		#endregion
	}
}
