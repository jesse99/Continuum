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
using System.Text;

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
			string sql = string.Format(@"
				SELECT name
					FROM Types 
				WHERE root_name = '{0}'", typeName);
			string[][] rows = m_database.QueryRows(sql);
			Contract.Assert(rows.Length <= 1, "too many rows");
			
			return rows.Length > 0;
		}
		
		public void GetBases(string typeName, List<string> baseNames, List<string> interfaceNames, List<string> allNames)
		{
			string sql = string.Format(@"
				SELECT base_type_name, interface_type_names
					FROM Types 
				WHERE root_name = '{0}'", typeName);
			string[][] rows = m_database.QueryRows(sql);
			Contract.Assert(rows.Length <= 1, "too many rows");
			
			if (rows.Length > 0)
			{
				if (rows[0][0].Length > 0)
				{
					baseNames.AddIfMissing(rows[0][0]);
					allNames.AddIfMissing(rows[0][0]);
				}
				
				string[] names = rows[0][1].Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);
				foreach (string name in names)
				{
					interfaceNames.AddIfMissing(name);
					allNames.AddIfMissing(name);
				}
				
				if (typeName == "System.Array")
				{
					interfaceNames.AddIfMissing("System.Collections.Generic.IEnumerable`1");
					allNames.AddIfMissing("System.Collections.Generic.IEnumerable`1");
				}
			}
		}
		
#if false
		public string FindAssembly(string fullName)
		{
			Contract.Requires(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			
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
			Contract.Requires(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			Contract.Requires(!string.IsNullOrEmpty(prefix), "prefix is null or empty");
			Contract.Requires(numArgs >= 0, "numArgs is negative");
			
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
		
		public Member[] GetFields(string[] typeNames, bool instanceCall, bool isStaticCall)
		{
			var members = new List<Member>();
			
			var types = new StringBuilder();
			for (int i = 0; i < typeNames.Length; ++i)
			{
				types.AppendFormat("declaring_root_name = '{0}'", typeNames[i]);
				
				if (i + 1 < typeNames.Length)
					types.Append(" OR ");
			}
			
			string sql;
			if (instanceCall && isStaticCall)
				sql = string.Format(@"
					SELECT name, type_name, declaring_root_name
						FROM Fields 
					WHERE ({0}) AND access < 3", types.ToString());	// we exclude all private fields (note that this won't affect this methods since the parser will pick up those)
			else
				sql = string.Format(@"
					SELECT name, type_name, declaring_root_name
						FROM Fields 
					WHERE ({0}) AND static = {1} AND access < 3", types.ToString(), isStaticCall ? "1" : "0");
			
			string[][] rows = m_database.QueryRows(sql);
			foreach (string[] r in rows)
			{
				members.AddIfMissing(new Member(r[0], r[1], r[2]));
			}
			
			return members.ToArray();
		}
		
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
		
		public Member[] GetMembers(string[] typeNames, bool instanceCall, bool isStaticCall)
		{
			var members = new List<Member>();
			
			var types = new StringBuilder();
			for (int i = 0; i < typeNames.Length; ++i)
			{
				types.AppendFormat("declaring_root_name = '{0}'", typeNames[i]);
				
				if (i + 1 < typeNames.Length)
					types.Append(" OR ");
			}
			
			string sql;
			if (instanceCall && isStaticCall)
				sql = string.Format(@"
					SELECT display_text, return_type_name, params_count, declaring_root_name
						FROM Methods 
					WHERE kind <= 1 AND ({0})", types.ToString());
			else
				sql = string.Format(@"
					SELECT display_text, return_type_name, params_count, declaring_root_name
						FROM Methods 
					WHERE static = {1} AND kind <= 1 AND ({0})", types.ToString(), isStaticCall ? "1" : "0");
			
			string[][] rows = m_database.QueryRows(sql);
			foreach (string[] r in rows)
			{
				string text = r[0];
				int j = text.IndexOf("::");
				text = text.Substring(j + 2);
				
				members.AddIfMissing(new Member(text, int.Parse(r[2]), r[1], r[3]));
			}
			
			return members.ToArray();
		}
		
		public Member[] GetExtensionMethods(string targetType, string[] typeNames, string[] namespaces)
		{
			var members = new List<Member>();
			
			var types = new StringBuilder();
			for (int i = 0; i < typeNames.Length; ++i)
			{
				types.AppendFormat("Methods.extend_type_name = '{0}'", typeNames[i]);
				
				if (i + 1 < typeNames.Length)
					types.Append(" OR ");
			}
			
			var ns = new StringBuilder();
			for (int i = 0; i < namespaces.Length; ++i)
			{
				ns.AppendFormat("Types.namespace = '{0}'", namespaces[i]);
				
				if (i + 1 < namespaces.Length)
					ns.Append(" OR ");
			}
			
			string sql = string.Format(@"
				SELECT Methods.display_text, Methods.return_type_name, Methods.params_count
					FROM Methods, Types
				WHERE Methods.static = 1 AND Methods.kind = 8 AND
					Methods.declaring_root_name = Types.root_name AND
					({0}) AND ({1})", types.ToString(), ns.ToString());
			
			string[][] rows = m_database.QueryRows(sql);
			foreach (string[] r in rows)
			{
				string text = r[0];
				int j = text.IndexOf("::");
				text = text.Substring(j + 2);
				
				j = text.IndexOf('{');
				int k = text.IndexOf('}');
				text = text.Substring(0, j) + text.Substring(k + 1);
				
				Member member = new Member(text, int.Parse(r[2]) - 1, r[1], targetType);
				member.IsExtensionMethod = true;
				
				members.AddIfMissing(member);
			}
			
			return members.ToArray();
		}
		
		#region Private Methods
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
