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
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AutoComplete
{
	internal sealed class TargetDatabase : ITargetDatabase
	{
		public TargetDatabase(Database database)
		{
			m_database = database;
		}
		
		public bool HasType(string typeName)
		{
			int id = DoFindId(new TypeId(typeName, 0));
			
			return id > 0;
		}
		
		public TypeId[] GetBases(TypeId type)
		{
#if false
			int id = DoFindId(type);

			string sql = string.Format(@"
				SELECT base_type_name, interface_type_names
					FROM Types 
				WHERE root_name = '{0}'", typeName);
			string[][] rows = m_database.id(sql);
			Trace.Assert(rows.Length <= 1, "too many rows");
			
			var types = new List<TypeId>();
			
			if (rows.Length > 0)
			{
				if (rows[0][0] != "0")
					types.Add(int.Parse(rows[0][0]));
				
				string[] names = rows[0][1].Split(new char[]{' '}, StringSplitOptions.RemoveEmptyEntries);
				foreach (string name in names)
				{
					types.AddIfMissing(int.Parse(name));
				}
			}
			
			return types.ToArray();
#endif
			return null;
		}
		
#if false
		public string FindAssembly(string fullName)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			
			string sql = string.Format(@"
				SELECT hash
					FROM Types 
				WHERE type = '{0}'", fullName.Replace("'", "''"));
			string[][] rows = m_database.QueryRows(sql);
			
			return rows.Length > 0 ? rows[0][0] : null;
		}
#endif
		
#if false
		public Tuple2<string, string>[] FindMethodsWithPrefix(string fullName, string prefix, int numArgs, bool includeInstanceMembers)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			Trace.Assert(!string.IsNullOrEmpty(prefix), "prefix is null or empty");
			Trace.Assert(numArgs >= 0, "numArgs is negative");
			
			string sql = string.Format(@"
				SELECT return_type, arg_names, name, attributes
					FROM Methods 
				WHERE declaring_type = '{0}' AND name GLOB '{0}*'", fullName, prefix);
			string[][] rows = m_database.QueryRows(sql);
			
			var result = from r in rows
				where DoIncludeMethod(r[1], numArgs, ushort.Parse(r[3]), includeInstanceMembers)
				select Tuple.Make(r[0], r[2]);
			
			return result.ToArray();
		}
#endif
		
#if false
		public Tuple2<string, string>[] FindFields(string fullName, bool includeInstanceMembers)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			
			var fields = new List<Tuple2<string, string>>();
			
			string sql = string.Format(@"
				SELECT name, type, attributes
					FROM Fields 
				WHERE declaring_type = '{0}'", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			for (int i = 0; i < rows.Length; ++i)
			{
				if ((ushort.Parse(rows[i][2]) & 1) == 0)				// no private base fields (declaring type fields will come from the parser)
					if (DoIncludeField(ushort.Parse(rows[i][2]), includeInstanceMembers))
						fields.Add(Tuple.Make(rows[i][1], rows[i][0]));
			}
			
			return fields.ToArray();
		}
#endif
		
#if false
		public string FindBaseType(string fullName)
		{
			if (fullName == "System.Object")
				return null;
				
			string sql = string.Format(@"
				SELECT DISTINCT base_type
					FROM Types
				WHERE type = '{0}' OR type GLOB '{0}<*'", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			return rows.Length > 0 ? rows[0][0] : null;
		}
#endif
		
#if false
		public string[] FindInterfaces(string fullName)
		{
			if (fullName == "System.Object")
				return new string[0];
				
			string sql = string.Format(@"
				SELECT DISTINCT interface_type
					FROM Implements
				WHERE type = '{0}' OR type GLOB '{0}<*'", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			var result = from r in rows select r[0];
			
			return result.ToArray();
		}
#endif
		
		public Member[] GetMembers(TypeId[] types, bool instanceCall, bool isStaticCall)
		{
			var members = new List<Member>();

#if false
			string sql;
			if (instanceCall && isStaticCall)
				sql = string.Format(@"
					SELECT text, return_type, arg_names, namespace
						FROM Members 
					WHERE type = '{0}'", fullName);
			else
				sql = string.Format(@"
					SELECT text, return_type, arg_names, namespace
						FROM Members 
					WHERE type = '{0}' AND is_static = '{1}'", fullName, isStaticCall ? "1" : "0");
			
			string[][] rows = m_database.QueryRows(sql);
			foreach (string[] r in rows)
			{
				string[] names = r[2].Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);
				if (r[3].Length == 0)
				{
					members.AddIfMissing(new Member(r[0], names, r[1], fullName));
				}
				else if (DoIsValidExtensionMethod(r[3], globals))
				{
					string[] last = new string[names.Length - 1];
					Array.Copy(names, 1, last, 0, last.Length);
					
					Member member = new Member(r[0], last, r[1], fullName);
					member.IsExtensionMethod = true;
					members.AddIfMissing(member);
				}
			}
#endif

			return members.ToArray();
		}
		
		public Member[] GetExtensionMethods(TypeId[] types)
		{
			var members = new List<Member>();
			
#if false
			string sql = string.Format(@"
				SELECT text, return_type, arg_names, namespace
					FROM Members 
				WHERE type = '{0}' AND length(namespace) > 0", fullName);
			
			string[][] rows = m_database.QueryRows(sql);
			foreach (string[] r in rows)
			{
				if (DoIsValidExtensionMethod(r[3], globals))
				{
					string[] names = r[2].Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);
					
					string[] last = new string[names.Length - 1];
					Array.Copy(names, 1, last, 0, last.Length);
					
					Member member = new Member(r[0], last, r[1], fullName);
					member.IsExtensionMethod = true;
					members.AddIfMissing(member);
				}
			}
#endif
			
			return members.ToArray();
		}
		
		#region Private Methods
		private int DoFindId(TypeId type)
		{
			int id = type.TypeName;
			
			if (id < 1)
			{
				string sql = string.Format(@"
					SELECT name
						FROM Names
					WHERE value = {0}", type.FullName.Replace("'", "''"));
				string[][] rows = m_database.QueryRows(sql);
				Trace.Assert(rows.Length <= 1, "too many rows");
				
				id = rows.Length > 0 ? int.Parse(rows[0][0]) : 0;
			}
			
			return id;
		}
		
#if false
		private bool DoIsValidExtensionMethod(string ns, CsGlobalNamespace globals)
		{
			bool valid = false;
			
			for (int i = 0; i < globals.Uses.Length && !valid; ++i)
			{
				valid = globals.Uses[i].Namespace == ns;
			}
			
			return valid;
		}
		
		private bool DoIncludeMethod(string argNames, int numArgs, ushort attributes, bool includeInstanceMembers)
		{
			bool include = argNames.Count(c => c == ':') == numArgs;
			
			if (include && !includeInstanceMembers)
				include = (attributes & 0x0010) != 0;
			
			return include;
		}

		private bool DoIncludeField(ushort attributes, bool includeInstanceMembers)
		{
			bool include = true;
			
			if (!includeInstanceMembers)
				include = (attributes & 0x0010) != 0;
			
			return include;
		}
#endif
		#endregion
		
		#region Fields
		private Database m_database;
		#endregion
	}
}
