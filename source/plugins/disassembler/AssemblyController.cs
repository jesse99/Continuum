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
using MObjc;
using Mono.Cecil;
using Mono.Cecil.Binary;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;

namespace Disassembler
{
	[ExportClass("AssemblyController", "NSWindowController", Outlets = "table")]
	internal sealed class AssemblyController : NSWindowController
	{
		public AssemblyController(AssemblyDocument doc) : base(NSObject.AllocAndInitInstance("AssemblyController"))
		{
			m_doc = doc;
			
			Unused.Value = NSBundle.loadNibNamed_owner(NSString.Create("disassembler"), this);
			window().setDelegate(this);
			
			m_table = new IBOutlet<NSOutlineView>(this, "table").Value;
			m_table.setDoubleAction("doubleClicked:");
			m_table.setTarget(this);
			
			ActiveObjects.Add(this);
			autorelease();							// get rid of the retain done by AllocAndInitInstance
		}
		
		public void windowWillClose(NSObject notification)
		{
			m_table.setDelegate(null);
			
			window().release();
		}
		
		public void doubleClicked(NSOutlineView sender)
		{
			NSIndexSet selections = m_table.selectedRowIndexes();
			uint row = selections.firstIndex();
			while (row != Enums.NSNotFound)
			{
				AssemblyItem item = (AssemblyItem) (m_table.itemAtRow((int) row));
				DoOpen(item);
				
				row = selections.indexGreaterThanIndex(row);
			}
		}
		
		public void getInfo(NSObject sender)
		{
			NSIndexSet selections = m_table.selectedRowIndexes();
			if (selections.count() > 0)
			{
				uint row = selections.firstIndex();
				while (row != Enums.NSNotFound)
				{
					AssemblyItem item = (AssemblyItem) (m_table.itemAtRow((int) row));
					DoGetInfo(item);
					
					row = selections.indexGreaterThanIndex(row);
				}
			}
			else
				DoGetAssemblyInfo();
		}
		
		public int outlineView_numberOfChildrenOfItem(NSOutlineView table, AssemblyItem item)
		{
			if (item == null)
				if (m_doc.Resources.ChildCount > 0)
					return 1 + m_doc.Namespaces.Length;
				else
					return m_doc.Namespaces.Length;
			else
				return item.ChildCount;
		}
		
		public bool outlineView_isItemExpandable(NSOutlineView table, AssemblyItem item)
		{
			if (item == null)
				return true;
			else
				return item.ChildCount > 0;
		}
		
		public NSObject outlineView_child_ofItem(NSOutlineView table, int index, AssemblyItem item)
		{
			if (item == null)
			{
				if (m_doc.Resources.ChildCount > 0)
				{
					if (index == 0)
						return m_doc.Resources;
					else
						return m_doc.Namespaces[index - 1];
				}
				else
				{
					return m_doc.Namespaces[index];
				}
			}
			else
				return item.GetChild(index);
		}
		
		public NSObject outlineView_objectValueForTableColumn_byItem(NSOutlineView table, NSTableColumn col, AssemblyItem item)
		{
			return NSString.Create(item.Label);
		}
		
		#region Private Methods
		private void DoGetAssemblyInfo()
		{
			var builder = new System.Text.StringBuilder();
			
			AssemblyDefinition assembly = m_doc.Assembly;
			
			DoAppendList(builder, "Attributes:", assembly.CustomAttributes, i => assembly.CustomAttributes[i].ToText(true));
			builder.AppendLine("Culture:" + (assembly.Name.Culture.Length > 0 ? " " + assembly.Name.Culture: string.Empty));
			builder.AppendLine("Entry Point: " + (assembly.EntryPoint != null ? assembly.EntryPoint.ToString() : "none"));
			builder.AppendLine("Flags: " + assembly.Name.Flags);
			builder.AppendLine("Hash: " + (assembly.Name.Hash != null && assembly.Name.Hash.Length > 0 ? BitConverter.ToString(assembly.Name.Hash) : "none"));
			builder.AppendLine("Hash Algorithm: " + assembly.Name.HashAlgorithm);
			builder.AppendLine("Kind: " + assembly.Kind);
			builder.AppendLine("MetadataToken: " + assembly.MetadataToken);
			builder.AppendLine("Name: " + assembly.Name.Name);
			builder.AppendLine("PublicKeyToken: " + (assembly.Name.PublicKeyToken != null ? BitConverter.ToString(assembly.Name.PublicKeyToken) : "none"));
			builder.AppendLine("Runtime: " + assembly.Runtime);
			DoAppendList(builder, "Security:", assembly.SecurityDeclarations);
			builder.AppendLine("Version: " + assembly.Name.Version);
			
			foreach (ModuleDefinition module in assembly.Modules)
			{
				builder.AppendLine();
				
				DoAppendList(builder, "Assembly References:", module.AssemblyReferences, i => module.AssemblyReferences[i].FullName);
				DoAppendList(builder, "Attributes:", module.CustomAttributes, i => module.CustomAttributes[i].ToText(true));
				DoAppendList(builder, "Extern Types:", module.ExternTypes, i => module.ExternTypes[i].FullName);
				builder.AppendLine("Image Characteristics: " + (module.Image != null ? module.Image.PEFileHeader.Characteristics.ToString() : "no image"));
				builder.AppendLine("Is Main: " + (module.Main ? "true" : "false"));
				DoAppendList(builder, "Member References:", module.MemberReferences, i => module.MemberReferences[i].ToString());
				builder.AppendLine("MetadataToken: " + module.MetadataToken);
				builder.AppendLine("Module Name: " + module.Name);
				builder.AppendLine("Path: " + (module.Image != null && module.Image.FileInformation != null ? module.Image.FileInformation.FullName : "none"));
				builder.AppendLine("Runtime Flags: " + (module.Image != null ? module.Image.CLIHeader.Flags.ToString() : "no image"));
				DoAppendList(builder, "Type References:", module.TypeReferences, i => module.TypeReferences[i].FullName);
			}
			
			DoShowInfo(builder.ToString(), assembly.Name.Name);
		}
		
		// SecurityDeclarationCollection isn't an IList or even an ICollection.
		private void DoAppendList(System.Text.StringBuilder builder, string name, SecurityDeclarationCollection c)
		{
			var l = new List<SecurityDeclaration>();
			
			if (c != null)
				foreach (SecurityDeclaration s in c)
					l.Add(s);
			
			DoAppendList(builder, name, l, i => l[i].ToText(true));
		}
		
		private void DoAppendList(System.Text.StringBuilder builder, string name, System.Collections.IList c, Func<int, string> namer)
		{
			builder.AppendLine(name);
			
			var l = new List<string>(c.Count);
			for (int i = 0; i < c.Count; ++i)
				l.Add(namer(i));
				
			if (l.Count > 0)
			{
				l.Sort();
				
				foreach (string n in l)
					builder.AppendLine("\t" + n);
			}
			else
					builder.AppendLine("\tnone");
		}
		
		private void DoGetInfo(AssemblyItem item)
		{
			string text = item.GetInfo();
			if (text.Length > 0)
				DoShowInfo(text, item.Label);
			else
				Functions.NSBeep();
		}
		
		private void DoShowInfo(string text, string label)
		{
			Boss boss = ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			string file = fs.GetTempFile(label.Replace(".", string.Empty), ".info");
			
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
		
		private void DoOpen(AssemblyItem item)
		{
			string text = item.GetText();
			if (text.Length > 0)
			{
				Boss boss = ObjectModel.Create("FileSystem");
				var fs = boss.Get<IFileSystem>();
				string file = fs.GetTempFile(item.Label.Replace(".", string.Empty), item.Extension());
				
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
			else
			{
				Functions.NSBeep();
			}
		}
		#endregion
		
		#region Fields
		private AssemblyDocument m_doc;
		private NSOutlineView m_table;
		#endregion
	}
}
