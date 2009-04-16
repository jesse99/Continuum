// Copyright (C) 2008 Jesse Jones
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
using Mono.Cecil;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ObjectModel
{
	internal sealed class FindInDatabase : ITextContextCommands
	{
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Get(Boss boss, string selection, List<TextContextItem> items)
		{
			if (boss != null)
			{
				m_dirBoss = boss;
				
				if (selection != null && selection.Length < 100 && !selection.Any(c => char.IsWhiteSpace(c)))
				{
					Stopwatch timer = null;
					if (Log.IsEnabled(TraceLevel.Verbose, "FindInDatabase"))
					{
						timer = new Stopwatch();
						timer.Start();
					}
					
					// Add open types.
					var objects = m_dirBoss.Get<IObjectModel>();
					TypeInfo[] types = objects.FindTypes(selection, MaxOpenItems + 1);
					DoAddOpenType(items, objects, types, selection, 0.2f);
					
					// Add open methods.
					DoAddOpenMethod(items, objects, selection, 0.3f);
					
					// Add type info commands. 
					bool addedSep = false;
					var interfaces = from t in types where (t.Flags & TypeFlags.Interface) != 0 select t;
					if (interfaces.Any())
					{
						items.Add(new TextContextItem(0.4f));
						addedSep = true;
						
						items.Add(new TextContextItem(
							"Show Implementors",
							s => {DoShowImplementors(interfaces); return s;},
							0.4f));
					}
					
					if (types.Any())
					{
						if (!addedSep)
						{
							items.Add(new TextContextItem(0.4f));
							addedSep = true;
						}
						
						items.Add(new TextContextItem(
							"Show Short Form",
							s => {DoShowShort(types); return s;},
							0.4f));
						
						var baseable = from t in types
							where t.RootName != "System.Object" && (t.Flags & TypeFlags.Interface) == 0
							select t;
						if (baseable.Any())
							items.Add(new TextContextItem(
								"Show Base Classes",
								s => {DoShowBases(baseable); return s;},
								0.4f));
					}
					
					var unsealed = from t in types where (t.Flags & TypeFlags.Sealed) == 0 select t;
					if (unsealed.Any())
					{
						if (!addedSep)
						{
							items.Add(new TextContextItem(0.4f));
							addedSep = true;
						}
						
						items.Add(new TextContextItem(
							"Show Derived Classes",
							s => {DoShowDerived(unsealed); return s;},
							0.4f));
					}
					
					if (timer != null)
						Log.WriteLine(TraceLevel.Verbose, "FindInDatabase", "get took {0:0.000} secs", timer.ElapsedMilliseconds/1000.0);
				}
			}
		}
		
		#region Private Methods
		private void DoShowShort(TypeInfo[] types)
		{
			Boss boss = Gear.ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			
			foreach (TypeInfo type in types)
			{
				string fullName = type.RootName;
					
				try
				{
					if (fullName != null)
					{
						string file = fs.GetTempFile(fullName + " Short Form", ".cs");
						using (StreamWriter writer = new StreamWriter(file))
						{
							var form = new ShortForm(m_dirBoss, writer);
							form.Write(fullName, type.Assembly);
						}
						
						boss = Gear.ObjectModel.Create("Application");
						var launcher = boss.Get<ILaunch>();
						launcher.Launch(file, -1, -1, 1);
					}
				}
				catch (Exception e)
				{
					NSString title = NSString.Create("Couldn't process '{0}'.", fullName);
					NSString message = NSString.Create(e.Message);
					Unused.Value = Functions.NSRunAlertPanel(title, message);
				}
			}
		}
		
		private void DoShowBases(IEnumerable<TypeInfo> types)
		{
			var objects = m_dirBoss.Get<IObjectModel>();
			
			Boss boss = Gear.ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			
			foreach (TypeInfo type in types)
			{
				TypeInfo[] bases = objects.FindBases(type.RootName);
				if (bases.Length > 0)
				{
					string fullName = type.RootName;
					string file = fs.GetTempFile(fullName + " Base Classes", ".cs");
					
					int i = 0;
					using (StreamWriter writer = new StreamWriter(file))
					{
						foreach (TypeInfo b in bases)
						{
							DoWriteType(writer, i++, b, objects);
						}
						DoWriteType(writer, i, type, objects);
					}
					
					boss = Gear.ObjectModel.Create("Application");
					var launcher = boss.Get<ILaunch>();
					launcher.Launch(file, -1, -1, 1);
				}
			}
		}
		
		private const int MaxDerived = 200;
		
		private void DoShowDerived(IEnumerable<TypeInfo> types)
		{
			var objects = m_dirBoss.Get<IObjectModel>();
			
			Boss boss = Gear.ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			
			foreach (TypeInfo type in types)
			{
				TypeInfo[] derived = objects.FindDerived(type.RootName, MaxDerived);
				if (derived.Length > 0)
				{
					string fullName = type.RootName;
					string file = fs.GetTempFile(fullName + " Derived Classes", ".cs");
					
					int count = 0;
					using (StreamWriter writer = new StreamWriter(file))
					{
						DoWriteTypes(writer, type, derived, 0, ref count);
					}
					
					boss = Gear.ObjectModel.Create("Application");
					var launcher = boss.Get<ILaunch>();
					launcher.Launch(file, -1, -1, 1);
				}
			}
		}
		
		private void DoShowImplementors(IEnumerable<TypeInfo> types)
		{
			Boss boss = Gear.ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			
			var objects = m_dirBoss.Get<IObjectModel>();
			
			foreach (TypeInfo type in types)
			{
				TypeInfo[] derived = objects.FindImplementors(type.RootName, MaxDerived);
				if (derived.Length > 0)
				{
					string fullName = type.RootName;
					string file = fs.GetTempFile(fullName + " Implementors", ".cs");
					
					int count = 0;
					using (StreamWriter writer = new StreamWriter(file))
					{
						DoWriteTypes(writer, type, derived, 0, ref count);
					}
					
					boss = Gear.ObjectModel.Create("Application");
					var launcher = boss.Get<ILaunch>();
					launcher.Launch(file, -1, -1, 1);
				}
			}
		}
		
		private void DoOpenFile(string path, int line)
		{
			Boss boss = Gear.ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();
			launcher.Launch(path, line, -1, 1);
		}
		
		private const int MaxOpenItems = 20;
		
		public void DoAddOpenType(List<TextContextItem> items, IObjectModel objects, TypeInfo[] types, string selection, float order)
		{
			// First check the database to see if we can find a type.
			string[] roots = (from t in types select t.RootName).ToArray();
			if (roots.Length > 0)
			{
				SourceInfo[] sources = objects.FindTypeSources(roots, MaxOpenItems + 1);
				if (sources.Length == 0)
				{
					// If the database doesn't have the type then see if we can find a local or mono
					// file with that name (this is helpful because mdb files do not have source files
					// for enums and interfaces). TODO: might want to iterate local files so we find
					// any new ones
					Boss boss = Gear.ObjectModel.Create("FileSystem");
					var fs = boss.Get<IFileSystem>();
					string[] candidates = fs.LocatePath("/" + selection + ".cs");
					string[] local = DoGetLocalPaths();
					
					var defaults = NSUserDefaults.standardUserDefaults();
					string mono = defaults.objectForKey(NSString.Create("mono_root")).To<NSString>().description();
					
					var temp = new List<SourceInfo>();
					foreach (string candidate in candidates)
					{
						if (Array.Exists(local, l => candidate.StartsWith(l)) || candidate.StartsWith(mono))
							temp.Add(new SourceInfo(Path.GetFileName(candidate), candidate, -1));
					}
					sources = temp.ToArray();
				}
				
				// If we found some files then add them to the context menu.
				if (sources.Length > 0)
				{
					Array.Sort(sources, (lhs, rhs) => lhs.Source.CompareTo(rhs.Source));
					
					items.Add(new TextContextItem(order));
					for (int i = 0; i < Math.Min(sources.Length, MaxOpenItems); ++i)
					{
						if (sources[i].Path != null)
						{
							string title = sources[i].Source;
							if (sources.Count(s => s.Source == title) > 1)		// see if the name is ambiguous
							{
								title = Path.GetFileName(Path.GetDirectoryName(sources[i].Path));
								title = Path.Combine(title, sources[i].Source);
							}
							
							int k = i;											// need this for the delegate (or the for loop will mutate the value)
							items.Add(new TextContextItem(
								"Open " + title,
								s => {DoOpenFile(sources[k].Path, sources[k].Line); return s;},
								order));
						}
					}
					
					if (sources.Length > MaxOpenItems)
						items.Add(new TextContextItem(Shared.Constants.Ellipsis, null, order));
				}
			}
		}
		
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
				
		public void DoAddOpenMethod(List<TextContextItem> items, IObjectModel objects, string selection, float order)
		{
			SourceInfo[] sources = objects.FindMethodSources(selection, MaxOpenItems + 1);
			if (sources.Length > 0)
			{
				Array.Sort(sources, (lhs, rhs) => lhs.Source.CompareTo(rhs.Source));
				
				items.Add(new TextContextItem(order));
				for (int i = 0; i < Math.Min(sources.Length, MaxOpenItems); ++i)
				{
					if (!string.IsNullOrEmpty(sources[i].Path))
					{
						int k = i;											// need this for the delegate (or the for loop will mutate the value)
						string title = "Open " + sources[i].Source;
						
						items.Add(new TextContextItem(
							title,
							s => {DoOpenFile(sources[k].Path, sources[k].Line); return s;},
							order,
							null,
							DoGetAttrTitle(title)));
					}
				}
				
				if (sources.Length > MaxOpenItems)
					items.Add(new TextContextItem(Shared.Constants.Ellipsis, null, order));
			}
		}
		
		private NSMutableAttributedString DoGetAttrTitle(string title)
		{
			NSMutableAttributedString atitle = null;
			
			int index = title.IndexOf(':');						// hilite the declaring type (makes it much easier to pick out the method you want when there are more than a few)
			if (index > 0)
			{
				int k = index - 1, len = 0;
				while (k > 0 && char.IsLetterOrDigit(title[k]))
				{
					--k;
					++len;
				}
				
				atitle = NSMutableAttributedString.Create(title);
				atitle.addAttribute_value_range(
					Externs.NSFontAttributeName,
					NSFont.menuBarFontOfSize(0.0f),
					new NSRange(0, title.Length));
				
				atitle.addAttribute_value_range(
					Externs.NSForegroundColorAttributeName,
					NSColor.blueColor(),
					new NSRange(k + 1, len));
			}
			
			return atitle;
		}
		
		private void DoWriteTypes(TextWriter writer, TypeInfo parent, TypeInfo[] derived, int indent, ref int count)
		{
			var objects = m_dirBoss.Get<IObjectModel>();
			DoWriteType(writer, indent, parent, objects);
			
			if (++count == MaxDerived)
			{
				writer.WriteLine("	{0}{1}", new string('\t', indent), Shared.Constants.Ellipsis);
			}
			else
			{
				foreach (TypeInfo d in derived)
				{
					if ((d.Flags & TypeFlags.Interface) != 0)
					{
						TypeInfo[] children = objects.FindImplementors(d.RootName, MaxDerived);
						DoWriteTypes(writer, d, children, indent + 1, ref count);
					}
					else
					{
						TypeInfo[] children = objects.FindDerived(d.RootName, MaxDerived);
						DoWriteTypes(writer, d, children, indent + 1, ref count);
					}
				}
			}
		}
		
		private void DoWriteType(TextWriter writer, int indent, TypeInfo info, IObjectModel objects)
		{
			string fullName = info.RootName;
			writer.WriteLine("{0}{1}{2}", new string('\t', indent), DoGetModifiers(info), fullName);
		}
		
		private string DoGetModifiers(TypeInfo info)
		{
			var builder = new StringBuilder();
			
			switch (info.Visibility)
			{
				case TypeVisibility.Public:
					builder.Append("public ");
					break;
					
				case TypeVisibility.Family:
					builder.Append("protected ");
					break;
					
				case TypeVisibility.Internal:
					builder.Append("internal ");
					break;
					
				case TypeVisibility.Private:
					builder.Append("private ");
					break;
					
				default:
					Contract.Assert(false, "bad vis");
					break;
			}
			
			if ((info.Flags & TypeFlags.Interface) == 0)
			{
				if ((info.Flags & TypeFlags.Abstract) != 0)
					builder.Append("abstract ");
				
				if ((info.Flags & TypeFlags.Sealed) != 0)
					builder.Append("sealed ");
			}
			
			if ((info.Flags & TypeFlags.Interface) != 0)
				builder.Append("interface ");
			else if ((info.Flags & TypeFlags.Value) == 0)
				builder.Append("class ");
			else
				builder.Append("struct ");
			
			return builder.ToString();
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private Boss m_dirBoss;
		#endregion
	}
}
