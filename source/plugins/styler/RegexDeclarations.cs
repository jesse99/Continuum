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
using Gear.Helpers;
using MCocoa;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Styler
{
	internal sealed class RegexDeclarations : IInterface, IObserver
	{
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			Broadcaster.Register("computed style runs", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "computed style runs":
					DoProcessStyles((StyleRuns) value);
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		#region Private Methods 
		private void DoProcessStyles(StyleRuns styles)
		{
			if (styles.Boss == m_boss)
			{
				string text = DoGetText(styles.Path);
				if (text != null)
				{
					var decs = new List<Declaration>();
					string indent = string.Empty;
					foreach (StyleRun run in styles.Runs)
					{
						if (run.Type == StyleType.Type)
						{
							decs.Add(new Declaration(
								text.Substring(run.Offset, run.Length),
								new NSRange(run.Offset, run.Length),
								true, false));
							indent = "    ";
						}
						else if (run.Type == StyleType.Member)
						{
							decs.Add(new Declaration(
								indent + text.Substring(run.Offset, run.Length),
								new NSRange(run.Offset, run.Length),
								false, false));
						}
					}
					
					var data = new Declarations(styles.Path, styles.Edit, decs.ToArray());
					Broadcaster.Invoke("computed declarations", data);
				}
			}
		}
		
		public string DoGetText(string path)
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			
			foreach (Boss b in windows.All())
			{
				var editor = b.Get<ITextEditor>();
				if (editor.Path != null && Paths.AreEqual(editor.Path, path))
				{
					var text = b.Get<IText>();
					return text.Text;
				}
			}
			
			return null;
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		#endregion
	}
}
