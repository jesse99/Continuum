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
			Contract.Requires(!string.IsNullOrEmpty(typeName), "typeName is null or empty");
			
			bool has = false;
			
			if (typeName == "array-type")
			{
				has = true;
			}
			else if (typeName == "nullable-type")
			{
				has = true;
			}
			else if (typeName == "pointer-type")
			{
				has = true;
			}
			else
			{
				string sql = string.Format(@"
					SELECT name
						FROM Types 
					WHERE root_name = '{0}'", typeName.Replace("'", "''"));
				string[][] rows = m_database.QueryRows(sql);
				Contract.Assert(rows.Length <= 1, "too many rows");

				has = rows.Length > 0;
			}
			
			return has;
		}
		
		public void GetBases(string typeName, List<string> baseNames, List<string> interfaceNames, List<string> allNames)
		{
			if (typeName == "array-type")
			{
				baseNames.AddIfMissing("System.Array");
				allNames.AddIfMissing("System.Array");
				
				interfaceNames.AddIfMissing("System.Collections.Generic.IEnumerable`1");
				allNames.AddIfMissing("System.Collections.Generic.IEnumerable`1");
			}
			else if (typeName == "nullable-type")
			{
				baseNames.AddIfMissing("System.Nullable`1");
				allNames.AddIfMissing("System.Nullable`1");
			}
			else if (typeName == "pointer-type")
			{
				// can't use the . operator with pointers
			}
			else
			{
				string sql = string.Format(@"
					SELECT base_root_name, interface_root_names
						FROM Types 
					WHERE root_name = '{0}'", typeName.Replace("'", "''"));
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
				}
			}
		}
		
		public Member[] GetFields(string[] typeNames, bool instanceCall, bool isStaticCall)
		{
			var members = new List<Member>();
			
			if (typeNames.Length > 0)
			{
				var types = new StringBuilder();
				for (int i = 0; i < typeNames.Length; ++i)
				{
					types.AppendFormat("declaring_root_name = '{0}'", typeNames[i].Replace("'", "''"));
					
					if (i + 1 < typeNames.Length)
						types.Append(" OR ");
				}
				
				string sql;
				if (instanceCall && isStaticCall)
					sql = string.Format(@"
						SELECT name, type_name, declaring_root_name
							FROM Fields 
						WHERE access < 3 AND ({0})", types.ToString());	// we exclude all private fields (note that this won't affect this methods since the parser will pick up those)
				else
					sql = string.Format(@"
						SELECT name, type_name, declaring_root_name
							FROM Fields 
						WHERE static = {1} AND access < 3 AND ({0})", types.ToString(), isStaticCall ? "1" : "0");
				
				string[][] rows = m_database.QueryRows(sql);
				foreach (string[] r in rows)
				{
					members.AddIfMissing(new Member(r[0], r[1], r[2]));
				}
			}
			
			return members.ToArray();
		}
		
		public Member[] GetFields(string[] typeNames, bool instanceCall, bool isStaticCall, string name)
		{
			var members = new List<Member>();
			
			if (typeNames.Length > 0)
			{
				var types = new StringBuilder();
				for (int i = 0; i < typeNames.Length; ++i)
				{
					types.AppendFormat("declaring_root_name = '{0}'", typeNames[i].Replace("'", "''"));
					
					if (i + 1 < typeNames.Length)
						types.Append(" OR ");
				}
				
				string sql;
				if (instanceCall && isStaticCall)
					sql = string.Format(@"
						SELECT name, type_name, declaring_root_name
							FROM Fields 
						WHERE name = '{0}' AND access < 3 AND ({1})", name, types.ToString());	// we exclude all private fields (note that this won't affect this methods since the parser will pick up those)
				else
					sql = string.Format(@"
						SELECT name, type_name, declaring_root_name
							FROM Fields 
						WHERE name = '{0}' AND static = {2} AND access < 3 AND ({1})", name, types.ToString(), isStaticCall ? "1" : "0");
				
				string[][] rows = m_database.QueryRows(sql);
				foreach (string[] r in rows)
				{
					members.AddIfMissing(new Member(r[0], r[1], r[2]));
				}
			}
			
			return members.ToArray();
		}
		
		public Member[] GetMembers(string[] typeNames, bool instanceCall, bool isStaticCall)
		{
			var members = new List<Member>();
			
			if (typeNames.Length > 0)
			{
				var types = new StringBuilder();
				for (int i = 0; i < typeNames.Length; ++i)
				{
					types.AppendFormat("declaring_root_name = '{0}'", typeNames[i].Replace("'", "''"));
					
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
			}
			
			return members.ToArray();
		}
		
		public Member[] GetMembers(string[] typeNames, bool instanceCall, bool isStaticCall, string name, int arity)
		{
			var members = new List<Member>();
			
			if (typeNames.Length > 0)
			{
				var types = new StringBuilder();
				for (int i = 0; i < typeNames.Length; ++i)
				{
					types.AppendFormat("declaring_root_name = '{0}'", typeNames[i].Replace("'", "''"));
					
					if (i + 1 < typeNames.Length)
						types.Append(" OR ");
				}
				
				string sql;
				if (instanceCall && isStaticCall)
					sql = string.Format(@"
						SELECT display_text, return_type_name, params_count, declaring_root_name
							FROM Methods 
						WHERE (name = '{0}' OR name = '{1}') AND params_count = {2} AND 
							kind <= 1 AND ({3})", name, "get_" + name, arity, types.ToString());
				else
					sql = string.Format(@"
						SELECT display_text, return_type_name, params_count, declaring_root_name
							FROM Methods 
						WHERE (name = '{0}' OR name = '{1}') AND params_count = {2} AND 
							static = {4} AND kind <= 1 AND ({3})", name, "get_" + name, arity, types.ToString(), isStaticCall ? "1" : "0");
				
				string[][] rows = m_database.QueryRows(sql);
				foreach (string[] r in rows)
				{
					string text = r[0];
					int j = text.IndexOf("::");
					text = text.Substring(j + 2);
					
					members.AddIfMissing(new Member(text, int.Parse(r[2]), r[1], r[3]));
				}
			}
			
			return members.ToArray();
		}
		
		public Member[] GetExtensionMethods(string targetType, string[] typeNames, string[] namespaces)
		{
			var members = new List<Member>();
			
			if (typeNames.Length > 0)
			{
				var types = new StringBuilder();
				for (int i = 0; i < typeNames.Length; ++i)
				{
					types.AppendFormat("Methods.extend_type_name = '{0}'", typeNames[i].Replace("'", "''"));
					
					if (i + 1 < typeNames.Length)
						types.Append(" OR ");
				}
				
				var ns = new StringBuilder();
				for (int i = 0; i < namespaces.Length; ++i)
				{
					ns.AppendFormat("Types.namespace = '{0}'", namespaces[i].Replace("'", "''"));
					
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
					
					j = text.IndexOf('(');
					Contract.Assert(j > 0, "couldn't find ( in " + text);
					
					int k = text.IndexOf(';', j + 1);
					if (k > 0)
					{
						text = text.Substring(0, j + 1) + text.Substring(k + 1);
					}
					else
					{
						k = text.IndexOf(')', j + 1);
						Contract.Assert(j < k, "couldn't find a second ; or ) in " + text);
						text = text.Substring(0, j + 1) + text.Substring(k);
					}
					
					Member member = new Member(text, int.Parse(r[2]) - 1, r[1], targetType);
					member.IsExtensionMethod = true;
					
					members.AddIfMissing(member);
				}
			}
			
			return members.ToArray();
		}
		
		public Member[] GetExtensionMethods(string targetType, string[] typeNames, string[] namespaces, string name, int arity)
		{
			var members = new List<Member>();
			
			if (typeNames.Length > 0)
			{
				var types = new StringBuilder();
				for (int i = 0; i < typeNames.Length; ++i)
				{
					types.AppendFormat("Methods.extend_type_name = '{0}'", typeNames[i].Replace("'", "''"));
					
					if (i + 1 < typeNames.Length)
						types.Append(" OR ");
				}
				
				var ns = new StringBuilder();
				for (int i = 0; i < namespaces.Length; ++i)
				{
					ns.AppendFormat("Types.namespace = '{0}'", namespaces[i].Replace("'", "''"));
					
					if (i + 1 < namespaces.Length)
						ns.Append(" OR ");
				}
				
				string sql = string.Format(@"
					SELECT Methods.display_text, Methods.return_type_name, Methods.params_count
						FROM Methods, Types
					WHERE Methods.name = '{0}' AND Methods.params_count = '{1}' AND
						Methods.static = 1 AND Methods.kind = 8 AND
						Methods.declaring_root_name = Types.root_name AND
						({2}) AND ({3})", name, arity + 1, types.ToString(), ns.ToString());
				
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
			}
			
			return members.ToArray();
		}
		
		#region Fields
		private Database m_database;
		#endregion
	}
}
