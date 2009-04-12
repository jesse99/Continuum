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
					
//					string name = CsHelpers.GetRealName(selection);
					
					var objects = m_dirBoss.Get<IObjectModel>();		// full name/file name/kind
//					Tuple3<string, string, int>[] info = objects.FindInfo(name, 2*MaxOpenItems);
					
					// Add open types.
					DoAddOpenType(items, objects, selection, 0.2f);
					
					// Add open methods.
					DoAddOpenMethod(items, objects, selection, 0.3f);
					
#if false
					// Add type info commands. 
					bool addedSep = false;
					var interfaces = (from i in info where i.Third == 1 select i.First).Distinct();
					if (interfaces.Any())
					{
						items.Add(new TextContextItem(0.4f));
						addedSep = true;
						
						items.Add(new TextContextItem(
							"Show Implementors",
							s => {DoShowImplementors(interfaces); return s;},
							0.4f));
					}
					
					var types = (from i in info where i.Third >= 1 select i.First).Distinct();
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
								
						var types2 = (from i in info where i.Third > 1 select i.First).Distinct();	// note that we can't mutate the types variable or it will screw up our lambda
						if (types2.Any(t => t != "System.Object"))
							items.Add(new TextContextItem(
								"Show Base Classes",
								s => {DoShowBases(types2); return s;},
								0.4f));
					}
					
					var unsealed = (from i in info where i.Third == 2 select i.First).Distinct();
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
#endif

					if (timer != null)
						Log.WriteLine(TraceLevel.Verbose, "FindInDatabase", "get took {0:0.000} secs", timer.ElapsedMilliseconds/1000.0);
				}
			}
		}
		
		#region Private Methods
#if false
		private void DoShowShort(IEnumerable<string> fullNames)
		{
			Boss boss = Gear.ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			
			foreach (string fullName in fullNames)
			{
				try
				{
					string file = fs.GetTempFile(fullName + " Short Form", ".cs");
					using (StreamWriter writer = new StreamWriter(file))
					{
						var form = new ShortForm(m_dirBoss, writer);
						form.Write(fullName);
					}
					
					boss = Gear.ObjectModel.Create("Application");
					var launcher = boss.Get<ILaunch>();
					launcher.Launch(file, -1, -1, 1);
				}
				catch (Exception e)
				{
					NSString title = NSString.Create("Couldn't process '{0}'.", fullName);
					NSString message = NSString.Create(e.Message);
					Unused.Value = Functions.NSRunAlertPanel(title, message);
				}
			}
		}
		
		private void DoShowBases(IEnumerable<string> fullNames)
		{
			var objects = m_dirBoss.Get<IObjectModel>();
			
			Boss boss = Gear.ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			
			foreach (string fullName in fullNames)
			{
				TypeAttributes[] attrs = objects.FindAttributes(fullName);
				if (attrs.Length > 0)
				{
					Tuple2<string, TypeAttributes>[] bases = objects.FindBases(fullName);
					string file = fs.GetTempFile(fullName + " Base Classes", ".cs");
					
					int i = 0;
					using (StreamWriter writer = new StreamWriter(file))
					{
						foreach (var b in bases)
						{
							writer.WriteLine("{0}{1}{2}", new string('\t', i++), ShortForm.GetModifiers(null, b.Second), b.First);
						}
						writer.WriteLine("{0}{1}{2}", new string('\t', i), ShortForm.GetModifiers(null, attrs[0]), fullName);
					}
					
					boss = Gear.ObjectModel.Create("Application");
					var launcher = boss.Get<ILaunch>();
					launcher.Launch(file, -1, -1, 1);
				}
			}
		}
		
		private const int MaxDerived = 200;
		
		private void DoShowDerived(IEnumerable<string> fullNames)
		{
			var objects = m_dirBoss.Get<IObjectModel>();
			
			Boss boss = Gear.ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			
			foreach (string fullName in fullNames)
			{
				TypeAttributes[] attrs = objects.FindAttributes(fullName);
				if (attrs.Length > 0)
				{
					Tuple2<string, TypeAttributes>[] derived = objects.FindDerived(fullName, MaxDerived + 1);
					
					string file = fs.GetTempFile(fullName + " Derived Classes", ".cs");
					using (StreamWriter writer = new StreamWriter(file))
					{
						DoWriteTypes(writer, fullName, attrs[0], derived, string.Empty);
					}
					
					boss = Gear.ObjectModel.Create("Application");
					var launcher = boss.Get<ILaunch>();
					launcher.Launch(file, -1, -1, 1);
				}
			}
		}
		
		private void DoShowImplementors(IEnumerable<string> fullNames)
		{
			Boss boss = Gear.ObjectModel.Create("FileSystem");
			var fs = boss.Get<IFileSystem>();
			
			var objects = m_dirBoss.Get<IObjectModel>();
			
			foreach (string fullName in fullNames)
			{
				TypeAttributes[] attrs = objects.FindAttributes(fullName);
				if (attrs.Length > 0)
				{
					Tuple2<string, TypeAttributes>[] derived = objects.FindImplementors(fullName);
					
					string file = fs.GetTempFile(fullName + " Implementors", ".cs");
					using (StreamWriter writer = new StreamWriter(file))
					{
						DoWriteTypes(writer, fullName, attrs[0], derived, string.Empty);
					}
					
					boss = Gear.ObjectModel.Create("Application");
					var launcher = boss.Get<ILaunch>();
					launcher.Launch(file, -1, -1, 1);
				}
			}
		}
#endif

		private void DoOpenFile(string path, int line)
		{
			Boss boss = Gear.ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();
			launcher.Launch(path, line, -1, 1);
		}
		
		private const int MaxOpenItems = 20;
		
		public void DoAddOpenType(List<TextContextItem> items, IObjectModel objects, string selection, float order)
		{			
			// First check the database to see if we can find a type.
			SourceLine[] sources = objects.FindTypeSources(selection, MaxOpenItems + 1);
			if (sources.Length == 0)
			{
				// If the database doesn't have the type then see if we can find a local or mono
				// file with that name (this is helpful because mdb files do not have source files
				// for enums and interfaces).
				Boss boss = Gear.ObjectModel.Create("FileSystem");
				var fs = boss.Get<IFileSystem>();
				string[] candidates = fs.LocatePath("/" + selection + ".cs");
				string[] local = DoGetLocalPaths();
				
				var defaults = NSUserDefaults.standardUserDefaults();
				string mono = defaults.objectForKey(NSString.Create("mono_root")).To<NSString>().description();
				
				var temp = new List<SourceLine>();
				foreach (string candidate in candidates)
				{
					if (Array.Exists(local, l => candidate.StartsWith(l)) || candidate.StartsWith(mono))
						temp.Add(new SourceLine(Path.GetFileName(candidate), candidate, 1));
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
			SourceLine[] sources = objects.FindMethodSources(selection, MaxOpenItems + 1);
			if (sources.Length > 0)
			{
				Array.Sort(sources, (lhs, rhs) => lhs.Source.CompareTo(rhs.Source));
				
				items.Add(new TextContextItem(order));
				for (int i = 0; i < Math.Min(sources.Length, MaxOpenItems); ++i)
				{
					if (sources[i].Path != null)
					{
						int k = i;											// need this for the delegate (or the for loop will mutate the value)
						string title = "Open " + sources[i].Source;
						title = title.Replace("{", string.Empty);
						if (title.Contains("})"))
							title = title.Replace("}", string.Empty);
						else
							title = title.Replace("}", ", ");
						
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

#if false		
		private void DoWriteTypes(TextWriter writer, string fullName, TypeAttributes attrs, Tuple2<string, TypeAttributes>[] derived, string indent)
		{
			var objects = m_dirBoss.Get<IObjectModel>();
			
			writer.WriteLine("{0}{1}{2}", indent, ShortForm.GetModifiers(null, attrs), fullName);
			foreach (var entry in derived)
			{
				if (entry.First == Shared.Constants.Ellipsis)
				{
					writer.WriteLine("	{0}{1}", indent, Shared.Constants.Ellipsis);
				}
				else if ((entry.Second & TypeAttributes.ClassSemanticMask) == TypeAttributes.Interface)
				{
					var temp = objects.FindImplementors(entry.First);
					DoWriteTypes(writer, entry.First, entry.Second, temp, indent + "\t");
				}
				else
				{
					var temp = objects.FindDerived(entry.First, MaxDerived);
					DoWriteTypes(writer, entry.First, entry.Second, temp, indent + "\t");
				}
			}
		}
#endif
		#endregion
		
		#region Fields
		private Boss m_boss;
		private Boss m_dirBoss;
		#endregion
	}
}
