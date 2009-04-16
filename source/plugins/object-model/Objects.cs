// Copyright (C) 2007-2008 Jesse Jones
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
	internal sealed class Objects : IObjectModel, IOpened, IObserver
	{
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Opened()
		{
			string path = Populate.GetDatabasePath(m_boss);
			m_database = new Database(path, "ObjectModel-" + Path.GetFileNameWithoutExtension(path));
			
			Broadcaster.Register("mono_root changed", this);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "mono_root changed":
					DoMonoRootChanged(name, value);
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		public TypeInfo[] FindTypes(string name, int max)
		{
			name = CsHelpers.GetRealName(name);
			name = CsHelpers.TrimNamespace(name);
			
			int gargs = 0;
			int i = name.IndexOf('`');
			if (name.Contains("`"))
			{
				int k = name.IndexOf('<');
				gargs = k > 0 ? int.Parse(name.Substring(i + 1, k - i - 1)) : int.Parse(name.Substring(i + 1));
				name = name.Substring(0, i);
			}
			
			string sql;
			if (gargs > 0)
				sql = string.Format(@"
					SELECT root_name, attributes, assembly, visibility
						FROM Types
					WHERE name = '{0}' AND generic_arg_count = {2}
					LIMIT {1}", name, max, gargs);
			else
				sql = string.Format(@"
					SELECT root_name, attributes, assembly, visibility
						FROM Types
					WHERE name = '{0}'
					LIMIT {1}", name, max);
			string[][] rows = m_database.QueryRows(sql);
			
			var types = from r in rows
				select new TypeInfo(int.Parse(r[2]), r[0], int.Parse(r[1]), int.Parse(r[3]));
			
			return types.ToArray();
		}
				
		public SourceInfo[] FindMethodSources(string name, int max)
		{
			string sql = string.Format(@"
				SELECT display_text, file_path, line
					FROM Methods
				WHERE (name = '{0}' OR name = '{2}') AND Methods.file_path != 0
				LIMIT {1}", name, max, "get_" + name);
			var rows = new List<string[]>(m_database.QueryRows(sql));
			
			sql = string.Format(@"
				SELECT Methods.display_text, Methods.file_path, Methods.line
					FROM Methods, Types
				WHERE Types.name = '{0}' AND 
					Methods.kind = 6 AND Types.root_name = Methods.declaring_root_name AND
					Methods.file_path != 0
				LIMIT {1}", name, max);
			rows.AddRange(m_database.QueryRows(sql));
			
			var sources = from r in rows
				select new SourceInfo(r[0].Replace(";", ", "), DoGetPath(r[1]), int.Parse(r[2]));
			
			return sources.ToArray();
		}
		
		public SourceInfo[] FindTypeSources(string[] rootNames, int max)
		{
			Contract.Requires(rootNames.Length > 0);
			
			var sources = new List<SourceInfo>();

			if (rootNames.Length > 0)
			{
				var roots = new StringBuilder();
				for (int i = 0; i < rootNames.Length; ++i)
				{
					roots.AppendFormat("Types.root_name = '{0}'", rootNames[i]);
					
					if (i + 1 < rootNames.Length)
						roots.Append(" OR ");
				}
				
				// Get all of the file paths/lines for the type name.
				string sql = string.Format(@"
					SELECT Methods.file_path, Methods.line
						FROM Types, Methods
					WHERE ({0}) AND
						Types.root_name = Methods.declaring_root_name AND
						Methods.file_path != 0", roots.ToString());
				string[][] rows = m_database.QueryRows(sql);
				
				// Get the smallest line number for each type.
				var files = new Dictionary<string, int>();
				foreach (string[] row in rows)
				{
					int line = int.Parse(row[1]);
					if (line >= 1)
					{
						if (files.ContainsKey(row[0]))
						{
							if (line < files[row[0]])
								files[row[0]] = line;
						}
						else
							files.Add(row[0], line);
					}
				}
				
				// Build a SourceInfo array.
				foreach (var entry in files)
				{
					string path = DoGetPath(entry.Key);
					sources.Add(new SourceInfo(Path.GetFileName(path), path, entry.Value));
					
					if (sources.Count == max)
						break;
				}
			}
			
			return sources.ToArray();
		}
		
		public string FindAssemblyPath(int assembly)
		{			
			string sql = string.Format(@"
				SELECT path 
					FROM Assemblies 
				WHERE assembly = {0}", assembly);
			string[][] rows = m_database.QueryRows(sql);
			Contract.Assert(rows.Length <= 1, "too many rows");
			
			return rows.Length > 0 ? rows[0][0] : null;
		}
		
		public TypeInfo[] FindBases(string rootName)
		{
			var types = new List<TypeInfo>();
			
			while (rootName != null && rootName != "System.Object")
			{
				string sql = string.Format(@"
					SELECT t2.root_name, t2.attributes, t2.assembly, t2.visibility
						FROM Types t1, Types t2
					WHERE t1.root_name = '{0}' AND 
						t1.base_root_name = t2.root_name", rootName);
				string[][] rows = m_database.QueryRows(sql);
				Contract.Assert(rows.Length <= 1, "too many rows");
			
				if (rows.Length > 0)
				{
					rootName = rows[0][0];
					types.Add(new TypeInfo(int.Parse(rows[0][2]), rootName, int.Parse(rows[0][1]), int.Parse(rows[0][3])));
				}
				else
					rootName = null;
			}
			
			types.Reverse();
			
			return types.ToArray();
		}
		
		public TypeInfo[] FindDerived(string rootName, int max)
		{
			string sql = string.Format(@"
				SELECT t2.root_name, t2.attributes, t2.assembly, t2.visibility
					FROM Types t1, Types t2
				WHERE t1.base_root_name = '{0}' AND 
					t1.root_name = t2.root_name
				LIMIT {1}", rootName, max);
			string[][] rows = m_database.QueryRows(sql);
		
			var types = from r in rows
				select new TypeInfo(int.Parse(r[2]), r[0], int.Parse(r[1]), int.Parse(r[3]));
			
			return types.ToArray();
		}
		
		public TypeInfo[] FindImplementors(string rootName, int max)
		{
			string sql = string.Format(@"
				SELECT t2.root_name, t2.attributes, t2.assembly, t2.visibility
					FROM Types t1, Types t2
				WHERE  t1.interface_root_names GLOB '*{0}:*' AND 
					t1.root_name = t2.root_name
				LIMIT {1}", rootName, max);
			string[][] rows = m_database.QueryRows(sql);
		
			var types = from r in rows
				select new TypeInfo(int.Parse(r[2]), r[0], int.Parse(r[1]), int.Parse(r[3]));
			
			return types.ToArray();
		}
		
		#region Private Methods
		private void DoMonoRootChanged(string name, object value)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			m_monoRoot = defaults.objectForKey(NSString.Create("mono_root")).To<NSString>().description();
			
			if (!Directory.Exists(Path.Combine(m_monoRoot, "mcs")))
			{
				NSString title = NSString.Create("Mono root appears invalid.");
				NSString message = NSString.Create("It does not contain an 'mcs' directory.");
				Unused.Value = Functions.NSRunAlertPanel(title, message);
			}
		}
		
		// If users have the mono source, but are using the mono from the installer then
		// the paths in the gac's mdb files will be wrong. So, we fix them up here.
		private string DoGetPath(string path)
		{
			// Pre-built mono files will look like "/private/tmp/monobuild/build/BUILD/mono-2.2/mcs/class/corlib/System.IO/File.cs"
			// Mono_root will usually look like "/foo/mono-2.2".
			if (!File.Exists(path))
			{
				if (m_monoRoot == null)
					DoMonoRootChanged("mono_root changed", null);
				
				if (m_monoRoot != null)
				{
					int i = path.IndexOf("/mcs/");
					if (i >= 0)
					{
						string temp = path.Substring(i + 1);
						path = Path.Combine(m_monoRoot, temp);
					}
				}
			}
			
			return path;
		}
		#endregion

		#region Fields 
		private Boss m_boss;
		private Database m_database;
		private string m_monoRoot;
		#endregion
	}
}	
