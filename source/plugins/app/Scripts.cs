// Copyright (C) 2009-2010 Jesse Jones
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
using Mono.Unix;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace App
{
	internal sealed class Scripts : BaseScripts, IScripts
	{
		public Scripts() : base("scripts", 23400)
		{
		}
		
		public string[] Names()
		{
			var names = new List<string>(Items.Count);
			
			foreach (string script in Items)
			{
				string name = Path.GetFileNameWithoutExtension(script);
				names.Add(name);
			}
			
			return names.ToArray();
		}
		
		public string Execute(string name, string text)
		{
			int index = Items.FindIndex(s => Path.GetFileNameWithoutExtension(s) == name);
			return DoScript(Items[index], text);
		}
		
		#region Protected Methods
		protected override void RemoveScriptsFromMenu()
		{
			NSApplication app = NSApplication.sharedApplication();
			NSMenu menu = app.Call("textMenu").To<NSMenu>();
			int index = menu.indexOfItemWithTarget_andAction(app.delegate_(), "openScripts:") - 2;
			
			while (!menu.itemAtIndex(index).isSeparatorItem())
			{
				menu.removeItemAtIndex(index--);
			}
		}
		
		protected override Tuple2<NSMenu, int> GetScriptsLocation()
		{
			NSApplication app = NSApplication.sharedApplication();
			NSMenu menu = app.Call("textMenu").To<NSMenu>();
			int at = menu.indexOfItemWithTarget_andAction(app.delegate_(), "openScripts:") - 1;
			
			return Tuple.Make(menu, at);
		}
		
		protected override void Execute(int index)
		{
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			boss = windows.Main();
			
			if (boss != null)
			{
				var text = boss.Get<IText>();
				NSRange range = text.Selection;
				string oldText = text.Text.Substring(range.location, range.length);
				string newText = DoScript(Items[index], oldText);
				if (newText != oldText)
				{
					string undoText = Path.GetFileNameWithoutExtension(Items[index]);
					text.Replace(newText, range.location, range.length, undoText);
				}
			}
		}
		
		protected override bool IsEnabled()
		{
			bool enabled = false;
			
			Boss boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			boss = windows.Main();
			
			if (boss != null)
			{
				var text = boss.Get<IText>();
				var editor = boss.Get<ITextEditor>();
				enabled = text.Selection.length > 0 && editor.Editable;
			}
			
			return enabled;
		}
		#endregion
		
		#region Private Methods		
		private string DoScript(string path, string selection)
		{
			string result = selection;
			
			try
			{
				DoCheckEndian(selection, "\r", "Classic Mac");
				DoCheckEndian(selection, "\r\n", "Windows");
				SaveFile(path);
				
				// TODO: Need to handle aliaii here and possibly in open panel dialogs.
				// See http://developer.apple.com/documentation/Cocoa/Conceptual/LowLevelFileMgmt/Tasks/ResolvingAliases.html
				using (Process process = new Process())
				{
					process.StartInfo.FileName = path;
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.RedirectStandardInput = true;
					process.StartInfo.RedirectStandardOutput = true;
					process.StartInfo.RedirectStandardError = true;
					
					process.Start();
					
					TextWriter input = process.StandardInput;
					input.Write(selection);
					input.Close();
					
					bool exited = process.WaitForExit(1000);
					if (!exited)
						throw new Exception("Timed out.");
					
					if (process.ExitCode == 0)
						result = process.StandardOutput.ReadToEnd();
					
					string err = process.StandardError.ReadToEnd();
					if (err.Length > 0)
					{
						Boss boss = ObjectModel.Create("Application");
						var transcript = boss.Get<ITranscript>();
						
						transcript.Show();
						transcript.WriteLine(Output.Error, "{0}", err);
					}
				}
			}
			catch (Exception e)
			{
				NSString title = NSString.Create("Couldn't execute '{0}'.", path);
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
			
			return result;
		}
		
		// This should not happen: Windows and Classic Mac endian files are converted
		// to Unix endian when the document is opened. Pasted strings are also converted
		// but in case there are other ways for text to enter the system we'll do this check.
		private void DoCheckEndian(string text, string endian, string name)
		{
			if (text.Contains(endian))
			{
				Boss boss = ObjectModel.Create("Application");
				var transcript = boss.Get<ITranscript>();
				
				transcript.Show();
				transcript.WriteLine(Output.Error, "The selection contains {0} line endings so the script may not work properly.", name);
			}
		}
		#endregion
	}
}
