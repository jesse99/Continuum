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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace App
{
	internal sealed class Refactors : BaseScripts, IRegisterRefactor
	{
		public Refactors() : base("refactors", 24400)
		{
		}
		
		public void Add(string name, Action<Boss> callback)
		{
			Trace.Assert(!string.IsNullOrEmpty(name), "name is null or empty");
			Trace.Assert(callback != null, "callback is null");
			
			m_refactors[name] = callback;
			Rebuild();
		}
		
		#region Protected Methods
		protected override void RemoveScriptsFromMenu()
		{
			NSApplication app = NSApplication.sharedApplication();
			NSMenu menu = app.Call("refactorMenu").To<NSMenu>();
			
			int index = 0;
			while (!menu.itemAtIndex(index).isSeparatorItem())
			{
				menu.removeItemAtIndex(index);
			}
		}

		protected override Tuple2<NSMenu, int> GetScriptsLocation()
		{
			NSApplication app = NSApplication.sharedApplication();
			NSMenu menu = app.Call("refactorMenu").To<NSMenu>();
			
			return Tuple.Make(menu, 0);
		}
		
		protected override void Execute(int index)
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			boss = windows.Main();
			
			if (boss != null)
			{
				if (m_refactors.ContainsKey(Items[index]))
				{
					DoCustomRefactor(Items[index], boss, m_refactors[Items[index]]);
				}
				else
				{
					var text = boss.Get<IText>();
					NSRange range = text.Selection;
					string newText = DoScript(Items[index], range.location, range.length, text.Text);
					if (newText != text.Text)
					{
						string undoText = Path.GetFileNameWithoutExtension(Items[index]);
						text.Replace(newText, 0, text.Text.Length, undoText);
					}
				}
			}
		}
		
		protected override void OnAddCustom(List<string> items)
		{
			items.AddRange(m_refactors.Keys);
		}
		
		protected override bool OnIsValidFile(string file)
		{
			string ext = Path.GetExtension(file);
			return base.OnIsValidFile(file) && ext == ".ref";
		}
		#endregion
		
		#region Private Methods		
		private string DoScript(string path, int location, int length, string text)
		{
			string result = text;
			
			try
			{
				SaveFile(path);
				string script = File.ReadAllText(path);
				
				Boss boss = ObjectModel.Create("Refactor");
				var refactor = boss.Get<IRefactorScript>();
				
				result = refactor.Execute(script, text, location, length);
			}
			catch (OperationCanceledException)
			{
			}
			catch (ScriptAbortException a)
			{
				DoReportScriptError(path, a.Message, -1);
			}
			catch (ScriptException s)
			{
				DoReportScriptError(path, s.Message, s.Line);
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "Errors", "Couldn't execute '{0}':", path);
				Log.WriteLine(TraceLevel.Error, "Errors", e.ToString());
				
				NSString title = NSString.Create("Couldn't execute '{0}'.", path);
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
			
			return result;
		}
		
		private void DoCustomRefactor(string name, Boss boss, Action<Boss> callback)
		{
			try
			{
				callback(boss);
			}
			catch (OperationCanceledException o)
			{
				if (!string.IsNullOrEmpty(o.Message))
				{
					boss = ObjectModel.Create("Application");
					var transcript = boss.Get<ITranscript>();		
					transcript.Show();
					transcript.WriteLine(Output.Error, "{0}", o.Message);
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Error, "Errors", "Couldn't execute {0}:", name);
				Log.WriteLine(TraceLevel.Error, "Errors", e.ToString());
				
				NSString title = NSString.Create("Couldn't execute the refactor.");
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		private void DoReportScriptError(string path, string message, int line)
		{
			Boss boss = ObjectModel.Create("Application");
			var transcript = boss.Get<ITranscript>();		
			transcript.Show();
			transcript.WriteLine(Output.Error, "{0}", message);
			
			if (line >= 1)
			{
				var launcher = boss.Get<ILaunch>();
				launcher.Launch(path, line, -1, 1);
			}
		}
		#endregion

		#region Fields
		private Dictionary<string, Action<Boss>> m_refactors = new Dictionary<string, Action<Boss>>();
		#endregion
	} 
}