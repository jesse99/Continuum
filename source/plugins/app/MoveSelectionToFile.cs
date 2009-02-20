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
	internal sealed class MoveSelectionToFile : IStartup
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
			register.Add("Move Selection to File", this.DoExecute);
		}
		
		#region Private Methods
		private void DoExecute(Boss boss)
		{
			var text = boss.Get<IText>();
			NSRange range = text.Selection;
			if (range.length == 0)
				throw new OperationCanceledException("Selection is empty");
			
			Boss b = ObjectModel.Create("CsParser");
			var cParser = b.Get<ICsParser>();
			CsGlobalNamespace globals = cParser.Parse(text.Text);
			CsDeclaration first = DoFindDeclaration(globals, range.location, range.length);
			string name = DoGetName(first);
			
			var editor = boss.Get<ITextEditor>();
			string path = DoGetPath(editor.Path, name + ".cs");
			if (path != null)
			{
				using (TextWriter writer = File.CreateText(path))
				{
					name = Path.GetFileNameWithoutExtension(path);
					DoSave(name, globals, first, writer, text.Text, range.location, range.length);				
					text.Replace(string.Empty, range.location, range.length, "Cut");
				}
			}
		}
		
		private string DoGetPath(string oldPath, string name)
		{
			NSSavePanel panel = NSSavePanel.savePanel();	
			panel.setCanCreateDirectories(true);
			panel.setExtensionHidden(false);
			
			string dir = Path.GetDirectoryName(oldPath);
			
			string path = null;
			int result = panel.runModalForDirectory_file(NSString.Create(dir), NSString.Create(name));
			if (result == Enums.NSOKButton)
			{
				path = panel.filename().description();
			}
			
			return path;
		}
		
		private void DoSave(string name, CsGlobalNamespace globals, CsDeclaration first, TextWriter writer, string text, int offset, int length)
		{
			// If the file starts with a comment then write it out.
			if (text.Length > 2 && text[0] == '/' && text[1] == '/')
				DoWriteSingleLineComments(writer, text);
			else if (text.Length > 2 && text[0] == '/' && text[1] == '*')
				DoWriteDelimitedComment(writer, text);
			
			// Write the global using directives.
			DoWriteUsing(writer, globals, string.Empty);
			
			// Write the namespace the declaration was in.
			CsNamespace ns = DoGetNamespace(first);
			if (ns != null && ns.Name != "<globals>")
			{
				writer.WriteLine("namespace {0}", ns.Name);
				writer.WriteLine("{");
				DoWriteUsing(writer, ns, "\t");
			}
			
			// If we're moving a member then create a dummy type.
			CsMember member = first as CsMember;
			if (member != null && member.DeclaringType != null)
			{
				string keyword = "?";
				if (member.DeclaringType is CsClass)
					keyword = "class";
				else if (member.DeclaringType is CsInterface)
					keyword = "interface";
				else if (member.DeclaringType is CsStruct)
					keyword = "struct";
					
				string modifiers;
				if (member.DeclaringType != null)
					modifiers = member.DeclaringType.Modifiers.ToString().ToLower();
				else
					modifiers = member.Modifiers.ToString().ToLower();
				modifiers = modifiers.Replace(",", string.Empty);
				
				writer.WriteLine("\t{0} {1} {2}", modifiers, keyword, name);
				writer.WriteLine("\t{");
			}
			
			// Write the selection.
			writer.Write(text.Substring(offset, length));
			
			// Close up type and namespaces.
			if (member != null && member.DeclaringType != null)
				writer.WriteLine("\t}");
			
			if (ns  != null && ns.Name != "<globals>")
				writer.WriteLine("}");
		}
		
		private CsNamespace DoGetNamespace(CsDeclaration first)
		{
			CsNamespace ns = null;
			
			// If the declaration is a type then use whatever namespace the
			// type is declared in.
			CsTypeScope type = first as CsTypeScope;
			if (type != null)
			{
				type = type.Namespace;
			}
			else
			{
				// If the declaration is a member then use whatever namespace
				// its declaring type was declared in.
				CsMember member = first as CsMember;
				if (member != null && member.DeclaringType != null)
					ns = member.DeclaringType.Namespace;
				
				// Special case enums and delegates declared in a namespace
				// scope.
				CsDelegate d = first as CsDelegate;
				if (ns == null && d != null)
					ns = d.Namespace;
				
				CsEnum e = first as CsEnum;
				if (ns == null && e != null)
					ns = e.Namespace;
			}
			
			return ns;
		}
		
		// Files often start with things like copyright notices which we want to preserve.
		private void DoWriteSingleLineComments(TextWriter writer, string text)
		{
			int i = 0;
			
			while (i + 2 < text.Length && text[i] == '/' && text[i + 1] == '/')
			{
				int j = text.IndexOf('\n', i);
				if (j >= 0)
				{
					writer.WriteLine(text.Substring(i, j - i));
					i = j + 1;
				}
				else
					break;
			}
			writer.WriteLine();
		}
		
		private void DoWriteDelimitedComment(TextWriter writer, string text)
		{
			int j = text.IndexOf("*/");
			if (j >= 0)
			{
				writer.WriteLine(text.Substring(0, j + 2));
				writer.WriteLine();
			}
		}
		
		private void DoWriteUsing(TextWriter writer, CsNamespace ns, string indent)
		{
			if (ns.Uses.Length > 0)
			{
				foreach (CsUsingDirective u in ns.Uses)
				{
					writer.WriteLine("{0}using {1};", indent, u.Namespace);
				}
				writer.WriteLine();
			}
		}
		
		private string DoGetName(CsDeclaration dec)
		{
			string name = string.Empty;
			
			do
			{
				var member = dec as CsMember;
				if (member != null)
				{
					name = member.Name;
					break;
				}
				
				var ns = dec as CsNamespace;
				if (ns != null)
				{
					name = ns.Name;
					break;
				}
				
				var type = dec as CsType;
				if (type != null)
				{
					name = type.Name;
					break;
				}
			}
			while (true);
			
			return name;
		}
		
		// Returns the longest declaration within the range.
		private CsDeclaration DoFindDeclaration(CsTypeScope scope, int offset, int length)
		{
			var decs = new List<CsDeclaration>(scope.Declarations.Length + 1);
			decs.Add(scope);
			
			CsDeclaration result = null;
			int resultLength = int.MinValue;
						
			// For every declaration,
			while (decs.Count > 0)
			{
				// if the declaration is within the range,
				CsDeclaration candidate = decs.Pop();
				if (offset <= candidate.Offset && candidate.Offset + candidate.Length <= offset + length)
				{
					// use the declaration if it is longer than what we have so far.
					if (candidate.Length > resultLength)
					{
						result = candidate;
						resultLength = candidate.Length;
					}
				}
				
				// If the declaration is a namespace or type then check the
				// inner declarations as well.
				CsTypeScope inner = candidate as CsTypeScope;
				if (inner != null)
					decs.AddRange(inner.Declarations);
			}
			
			return result;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	}
}
