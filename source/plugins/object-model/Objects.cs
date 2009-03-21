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

namespace ObjectModel
{
	internal sealed class Objects : IObjectModel, IOpened
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
			
			Broadcaster.Register("mono_root changed", this, this.DoMonoRootChanged);
		}
		
		public long GetBuildTime(string hash)
		{
			string sql = string.Format(@"
				SELECT DISTINCT write_time
					FROM AssemblyPaths 
				WHERE hash = '{0}'", hash);
			string[][] rows = m_database.QueryRows(sql);
			
			return rows.Length > 0 ? long.Parse(rows[0][0]) : 0;
		}
	
		public SourceLine[] FindMethodSources(string fullName)
		{
			string sql = string.Format(@"
				SELECT file, line, hash
					FROM Methods 
				WHERE method = '{0}' AND length(file) > 0 AND line != -1", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			var sources = from r in rows 
				select new SourceLine(DoGetPath(r[0]), int.Parse(r[1]), r[2]);

			return sources.ToArray();
		}
		
		public SourceLine[] FindTypeSources(string fullName)
		{
			string name = fullName.GetTypeName();	// we want the unbound generic type
			
			string sql = string.Format(@"
				SELECT hash, file, MIN(line)
					FROM Methods 
				WHERE (declaring_type = '{0}' OR declaring_type GLOB '{0}<*') AND length(file) > 0 AND line != -1
				GROUP BY hash, file", name);
			string[][] rows = m_database.QueryRows(sql);
						
			var sources = from r in rows 
				select new SourceLine(DoGetPath(r[1]), int.Parse(r[2]), r[0]);

			return sources.ToArray();
		}
		
		public string[] FindTypeAssemblyPaths(string fullName)
		{
			string name = fullName.GetTypeName();	// we want the unbound generic type
			
			string sql = string.Format(@"
				SELECT DISTINCT AssemblyPaths.path 
					FROM Types 
				INNER JOIN AssemblyPaths 
					ON Types.hash = AssemblyPaths.hash 
				WHERE Types.type = '{0}'", name);
			string[][] rows = m_database.QueryRows(sql);
			
			var result = from r in rows select r[0];

			return result.ToArray();
		}
		
		public TypeAttributes[] FindAttributes(string fullName)
		{			
			string name = fullName.GetTypeName();	// attributes are attached to the unbound type
			
			string sql = string.Format(@"
				SELECT DISTINCT attributes
					FROM Types 
				WHERE type = '{0}' OR type GLOB '{0}<*'", name);
			string[][] rows = m_database.QueryRows(sql);
						
			var attrs = from r in rows select (TypeAttributes) int.Parse(r[0]);

			return attrs.ToArray();
		}

		public Tuple2<string, TypeAttributes>[] FindImplementors(string fullName)
		{			
			string sql = string.Format(@"
				SELECT DISTINCT type,
					(SELECT attributes FROM Types WHERE type = i.type AND hash = i.hash) attributes
					FROM Implements i
				WHERE interface_type = '{0}' OR interface_type GLOB '{0}<*'", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			var types = from r in rows select Tuple.Make(r[0], (TypeAttributes) int.Parse(r[1]));

			return types.ToArray();
		}

		public Tuple2<string, TypeAttributes>[] FindBases(string fullName)
		{
			var bases = new List<Tuple2<string, TypeAttributes>>();
			
			string name = fullName;
			while (name != "System.Object")
			{
				name = DoFindBase(name);
				if (name == null)
					break;
					
				bases.Insert(0, Tuple.Make(name, DoFindAttrs(name, (TypeAttributes) uint.MaxValue)));
			}
			
			return bases.ToArray();
		}
		
		public Tuple2<string, TypeAttributes>[] FindDerived(string fullName, int maxResults)
		{			
			string sql = string.Format(@"
				SELECT DISTINCT type, attributes
					FROM Types
				WHERE base_type = '{0}' OR base_type GLOB '{0}<*'
				LIMIT {1}", fullName, maxResults + 1);
			string[][] rows = m_database.QueryRows(sql);
			
			if (rows.Length > maxResults)
				rows[maxResults] = new string[]{Shared.Constants.Ellipsis, "0"};
			
			var types = from r in rows select Tuple.Make(r[0], (TypeAttributes) int.Parse(r[1]));

			return types.ToArray();
		}
		
		public Tuple3<string, string, int>[] FindInfo(string name, int maxResults)
		{
			string sql = string.Format(@"
				SELECT DISTINCT full_name, file_name, kind
					FROM NameInfo
				WHERE name = '{0}' OR full_name = '{0}' OR full_name GLOB '{0}<*'
				LIMIT {1}", name, maxResults);
			string[][] rows = m_database.QueryRows(sql);
			
			var result = from r in rows select Tuple.Make(r[0], r[1], int.Parse(r[2]));

			return result.ToArray();
		}
		
		#region Private Methods		
		private string DoFindBase(string fullName)
		{
			string name = fullName.GetTypeName();	// base classes are attached to the unbound type
			
			string sql = string.Format(@"
				SELECT DISTINCT base_type
					FROM Types
				WHERE type = '{0}' OR type GLOB '{0}<*'", name);
			string[][] rows = m_database.QueryRows(sql);
			
			return rows.Length > 0 ? rows[0][0] : null;
		}
		
		private TypeAttributes DoFindAttrs(string fullName, TypeAttributes missing)
		{
			string name = fullName.GetTypeName();	// attributes are attached to the unbound type
			
			string sql = string.Format(@"
				SELECT DISTINCT attributes
					FROM Types
				WHERE type = '{0}' OR type GLOB '{0}<*'", name);
			string[][] rows = m_database.QueryRows(sql);
			
			return rows.Length > 0 ? (TypeAttributes) int.Parse(rows[0][0]) : missing;
		}
		
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
