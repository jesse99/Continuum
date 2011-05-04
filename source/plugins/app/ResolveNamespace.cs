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
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace App
{
	internal sealed class ResolveNamespace : IStartup
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void OnStartup()
		{
			var register = m_boss.Get<IRegisterRefactor>();
			register.Add("Resolve Namespace", this.DoExecute);
		}
		
		#region Private Methods
		private void DoExecute(Boss boss)
		{
			var editor = boss.Get<ITextEditor>();
			if (editor.Path != null)
			{
				var text = boss.Get<IText>();
				
				Boss b = ObjectModel.Create("CsParser");
				var parses = b.Get<IParses>();
				Parse parse = parses.Parse(editor.Key, text.EditCount, text.Text);
				if (parse.ErrorLength == 0 && parse.Globals != null)
				{
					NSRange sel = text.Selection;
					string type = text.Text.Substring(sel.location, sel.length);
					string ns = DoGetNamespace(boss, type);
					if (ns != null)
					{
						b = ObjectModel.Create("Refactors");
						var refactors = b.Get<IRefactors>();
						
						refactors.Init(text.Text);
						refactors.QueueAddUsing(parse.Globals, ns);
						string result = refactors.Process();
					
						if (result != text.Text)
						{
							text.Replace(result, 0, text.Text.Length, "Resolve Namespace");
						}
					}
				}
			}
		}
		
		public string[] DoFindFullNames(Boss windowBoss, string typeName, int max)
		{
			if (typeName.Contains("."))
				return new string[]{typeName};
				
			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
			var finder = boss.Get<IFindDirectoryEditor>();
			boss = finder.GetDirectoryEditor(windowBoss);
			if (boss == null)
				throw new InvalidOperationException("Couldn't find a directory window associated with the text window.");
			
			IEnumerable<string> names;
			var database = boss.Get<IDatabase>();
			using (Database db = database.GetDatabase())
			{
				string sql = string.Format(@"
					SELECT root_name
						FROM Types
					WHERE name = '{0}'
					LIMIT {1}", typeName, max);
				string[][] rows = db.QueryRows(sql);
				
				names = from r in rows select r[0];
			}
			
			return names.ToArray();
		}
		
		private string DoGetNamespace(Boss windowBoss, string type)
		{
			if (type.Length == 0)
				throw new InvalidOperationException("Expected a selection with a type name.");
			
			// Get the namespaces the type is within.
			string[] candidates = DoFindFullNames(windowBoss, type, 12);
			
			var names = new List<string>();
			foreach (string candidate in candidates)
			{
				string temp = candidate;
				
				int i = temp.IndexOf('/');	// strip off the nested type
				if (i > 0)
					temp = temp.Substring(0, i);
				
				i = temp.LastIndexOf('.');				// strip off the type name
				if (i > 0)
					names.Add(temp.Substring(0, i));
			}
			
			// Figure out which namespace we want to add.
			string name = null;
			if (names.Count == 0)
			{
				throw new InvalidOperationException("Couldn't find a type named '" + type + "'.");
			}
			else if (names.Count == 1)
			{
				name = names[0];
			}
			else
			{
				var picker = new GetItem<string>{
					Title = "Select Namespace",
					Items = names.ToArray(),
					AllowsMultiple = false};
					
				string[] result = picker.Run(s => s);
				if (result.Length == 1)
					name = result[0];
			}
			
			return name;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	}
}
