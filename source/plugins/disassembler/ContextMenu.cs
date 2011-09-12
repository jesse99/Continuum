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
using Mono.Cecil;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Disassembler
{
	internal sealed class ContextMenu : ITextContextCommands
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Get(string selection, string language, bool editable, List<TextContextItem> items)
		{
			if (selection != null)
			{
				Boss boss = DoGetTextBoss();
				if (boss != null)
				{
					var editor = boss.Get<ITextEditor>();
					if (editor.Path != null)
					{
						Boss b = ObjectModel.Create("CsParser");
						var parses = b.Get<IParses>();
						var text = boss.Get<IText>();
						
						Parse parse = parses.Parse(editor.Key, text.EditCount, text.Text);
						if (parse.Globals != null)
						{
							CsType type = DoFindType(parse.Globals, text.Selection);
							if (type != null)
							{
								items.Add(new TextContextItem(
									"Disassemble " + type.Name,
									s => {DoDisassembleType(type); return s;},
									0.29f));
							}
							else
							{
								CsMethod method = DoFindMethod(parse.Globals, text.Selection);
								if (method != null)
								{
									items.Add(new TextContextItem(
										"Disassemble " + method.Name,
										s => {DoDisassembleMethod(method); return s;},
										0.31f));
								}
							}
						}
					}
				}
			}
		}
		
		#region Private Methods
		private string[] DoGetLocalPaths()
		{
			var localPaths = new List<string>();
			
			Boss boss = Gear.ObjectModel.Create("DirectoryEditorPlugin");
			var windows = boss.Get<IWindows>();
			foreach (Boss b in windows.All())
			{
				var editor = b.Get<IDirectoryEditor>();
				localPaths.Add(editor.Path);
			}
			
			return localPaths.ToArray();
		}
		
		private AssemblyDefinition DoLoadAssembly(string path)
		{
			try
			{
				return AssemblyCache.Load(path, true);
			}
			catch
			{
			}
			
			return null;
		}
		
		private IEnumerable<KeyValuePair<string, AssemblyDefinition>> DoGetLocalAssemblies()
		{
			var paths = new List<string>();
			
			// Get paths for all the local assemblies.
			string[] dirs = DoGetLocalPaths();
			foreach (string dir in dirs)
			{
				foreach (string path in Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories))
					paths.Add(path);
				
				foreach (string path in Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories))
					paths.Add(path);
			}
			
			// Sort the paths so that the ones built most recently appear first.
			paths.Sort((lhs, rhs) =>
			{
				DateTime l = File.GetLastWriteTime(lhs);
				DateTime r = File.GetLastWriteTime(rhs);
				return r.CompareTo(l);
			});
			
			// Return each assembly.
			foreach (string path in paths)
			{
				AssemblyDefinition assembly = DoLoadAssembly(path);
				if (assembly != null)
					yield return new KeyValuePair<string, AssemblyDefinition>(path, assembly);
			}
		}
		
		private void DoOpen(string name, string text)
		{
			Boss boss = ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			string file = fs.GetTempFile(name.Replace(".", string.Empty), ".cil");
			
			try
			{
				using (StreamWriter writer = new StreamWriter(file))
				{
					writer.WriteLine("{0}", text);
				}
				
				boss = ObjectModel.Create("Application");
				var launcher = boss.Get<ILaunch>();
				launcher.Launch(file, -1, -1, 1);
			}
			catch (IOException e)	// can sometimes land here if too many files are open (max is system wide and only 256)
			{
				NSString title = NSString.Create("Couldn't process '{0}'.", file);
				NSString message = NSString.Create(e.Message);
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		private void DoDisassembleType(CsType type)
		{
			foreach (KeyValuePair<string, AssemblyDefinition> entry in DoGetLocalAssemblies())
			{
				string name = type.FullName;
				if (type.GenericArguments != null)
					name += "`" + (name.Count(c => c == ',') + 1);
				
				TypeDefinition td = entry.Value.MainModule.GetType(name);
				if (td != null)
				{
					string text = td.Disassemble(entry.Key);
					DoOpen(td.Name, text);
					return;
				}
			}
			
			Functions.NSBeep();
		}
		
		// Would be nice to get this working with other members like properties
		// and constructors too.
		private void DoDisassembleMethod(CsMethod method)
		{
			foreach (KeyValuePair<string, AssemblyDefinition> entry in DoGetLocalAssemblies())
			{
				TypeDefinition td = entry.Value.MainModule.GetType(method.DeclaringType.FullName);
				if (td != null)
				{
					bool found = false;
			
					foreach (MethodDefinition candidate in td.Methods.Where(m => m.Name == method.Name))
					{
						if (candidate.Parameters.Count == method.Parameters.Length)
						{
							string text = candidate.Disassemble(entry.Key);
							DoOpen(method.Name, text);
							found = true;
						}
					}
					
					if (found)
						return;
				}
			}
		}
		
		private Boss DoGetTextBoss()
		{
			Boss boss = null;
			
			boss = ObjectModel.Create("TextEditorPlugin");
			var windows = boss.Get<IWindows>();
			boss = windows.Main();
			
			return boss;
		}
		
		private CsType DoFindType(CsNamespace ns, NSRange range)
		{
			foreach (CsType candidate in ns.Types)
			{
				if (candidate.NameOffset == range.location)
					if (candidate.Name.Length == range.length)
						return candidate;
			}
			
			foreach (CsNamespace n2 in ns.Namespaces)
			{
				CsType type = DoFindType(n2, range);
				if (type != null)
					return type;
			}
			
			return null;
		}
		
		private CsMethod DoFindMethod(CsNamespace ns, NSRange range)
		{
			foreach (CsType type in ns.Types)
			{
				foreach (CsMethod candidate in type.Methods)
				{
					if (candidate.NameOffset == range.location)
						if (candidate.Name.Length == range.length)
							return candidate;
				}
			}
			
			foreach (CsNamespace n2 in ns.Namespaces)
			{
				CsMethod method = DoFindMethod(n2, range);
				if (method != null)
					return method;
			}
			
			return null;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	}
}
