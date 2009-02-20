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
					
					string name = DoGetRealName(selection);
					
					var objects = m_dirBoss.Get<IObjectModel>();		// full name/file name/kind
					Tuple3<string, string, int>[] info = objects.FindInfo(name, 2*MaxOpenItems);
					
					// Add open types.
					DoAddOpenType(items, info, 0.2f);
					
					// Add open methods.
					DoAddOpenMethod(items, info, 0.3f);
					
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
					
					if (timer != null)
						Log.WriteLine(TraceLevel.Verbose, "FindInDatabase", "get took {0:0.000} secs", timer.ElapsedMilliseconds/1000.0);
				}
			}
		}
		
		#region Private Methods		
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

		private void DoOpenFile(string fullName, bool isMethod)
		{
			Boss boss = Gear.ObjectModel.Create("Application");
			var launcher = boss.Get<ILaunch>();

			var objects = m_dirBoss.Get<IObjectModel>();
			SourceLine[] sources = isMethod ? objects.FindMethodSources(fullName) : objects.FindTypeSources(fullName);

			// If there is only one possibility then open it.
			if (sources.Length == 1)
			{
				launcher.Launch(sources[0].Path, sources[0].Line, -1, 1);
			}
			
			// If there are multiple possibilities that means that two or more assemblies
			// reference the same method name and file but think the method is at
			// different locations. So, what we want to do is use the information from
			// the newest assemblies because that is most likely to be correct. 
			else
			{
				var temp = from c in sources select new {Path = c.Path, Line = c.Line, Time = objects.GetBuildTime(c.AssemblyHash)};			
				var ordered = from o in temp
					orderby o.Time descending
					select o;
					
				long time = ordered.First().Time;
				foreach (var o in ordered)
				{
					if (o.Time == time)
						launcher.Launch(o.Path, o.Line, -1, 1);
					else
						break;
				}
			}
		}
		
		private const int MaxOpenItems = 20;
 		
 		// full name/file name/kind
		public void DoAddOpenType(List<TextContextItem> items, Tuple3<string, string, int>[] info, float order)
		{
			var pairs = new List<Tuple2<string, string>>();		// full name/file name
			foreach (var item in info)
			{
				if (item.Second.Length > 0 && item.Third > 0)
					if (!pairs.Any(p => p.Second == item.Second))	// would be nice to use linq for this, but I'm not sure how we'd use Distinct for this part
						pairs.Add(Tuple.Make(item.First, item.Second));
			}
			pairs.Sort((lhs, rhs) => lhs.Second.CompareTo(rhs.Second));

			if (pairs.Count > 0)
			{
				items.Add(new TextContextItem(order));
				
				for (int i = 0; i < Math.Min(pairs.Count, MaxOpenItems); ++i)
				{
					int k = i;
					items.Add(new TextContextItem(
						"Open " + pairs[i].Second, 
						s => {DoOpenFile(pairs[k].First, false); return s;}, 
						order));
				}
				
				if (pairs.Count > MaxOpenItems)
					items.Add(new TextContextItem(Shared.Constants.Ellipsis, null, order));
			}
		}

 		// full name/file name/kind
		public void DoAddOpenMethod(List<TextContextItem> items, Tuple3<string, string, int>[] info, float order)
		{
			var names = (from e in info 
				where e.Third == 0 
				orderby e.First 
				select e.First).Distinct();

			int count = names.Count();
			if (count > 0)
			{
				items.Add(new TextContextItem(order));
				
				int i = 0;
				foreach (string name in names)
				{
					string n = name;
					string title = "Open " + name;					
										
					items.Add(new TextContextItem(
						title, 
						s => {DoOpenFile(n, true); return s;}, 
						order,
						null,
						DoGetAttrTitle(title)));

					if (++i >= MaxOpenItems)
						break;
				}
				
				if (count > MaxOpenItems)
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
								
		private string DoGetRealName(string name)
		{
			switch (name)
			{
				case "bool":
					return "System.Boolean";
					
				case "byte":
					return "System.Byte";
					
				case "char":
					return "System.Char";
					
				case "decimal":
					return "System.Decimal";
					
				case "double":
					return "System.Double";
					
				case "short":
					return "System.Int16";
					
				case "int":
					return "System.Int32";
					
				case "long":
					return "System.Int64";
															
				case "sbyte":
					return "System.SByte";
					
				case "object":
					return "System.Object";
					
				case "float":
					return "System.Single";
					
				case "string":
					return "System.String";
					
				case "ushort":
					return "System.UInt16";
					
				case "uint":
					return "System.UInt32";
					
				case "ulong":
					return "System.UInt64";
					
				case "void":
					return "System.Void";
					
				default:
					return name;
			}
		}								
		#endregion

		#region Fields
		private Boss m_boss;
		private Boss m_dirBoss; 
		#endregion
	} 
}