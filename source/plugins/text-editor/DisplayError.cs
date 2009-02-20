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
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Diagnostics;

namespace TextEditor
{	
	internal sealed class DisplayError : IDisplayBuildError
	{
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Clear()
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			
			foreach (Boss b in windows.All())
			{
				var window = b.Get<IWindow>();
				TextController controller = (TextController) window.Window.windowController();
				controller.ClearError();
			}
		}
		
		public void Display(string path, int line, int col, int tabWidth)
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			
			foreach (Boss b in windows.All())
			{
				var editor = b.Get<ITextEditor>();
				if (editor.Path != null && Paths.AreEqual(editor.Path, path))
				{
					var text = b.Get<IText>();
					int offset = Editor.GetOffset(text.Text, line, col, tabWidth);
					int length = DoGetLength(text.Text, offset);
					
					var window = b.Get<IWindow>();
					TextController controller = (TextController) window.Window.windowController();
					if (length > 0)
						controller.HighlightError(offset, length);
					else
						controller.ClearError();
					break;
				}
			}
		}
		
		#region Private Methods
		private int DoGetLength(string text, int offset)
		{
			int length = offset < text.Length ? 1 : 0;
			
			if (offset < text.Length && char.IsLetter(text[offset]))
			{
				while (offset + length < text.Length && char.IsLetterOrDigit(text[offset + length]))
				{
					++length;
				}
			}
			
			return length;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	} 
}
