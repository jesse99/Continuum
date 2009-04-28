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
			Profile.Start("TargetDatabase::HasType");
			
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
			
			Profile.Stop("TargetDatabase::HasType");
			return has;
		}
		
		public Item[] GetNamespaces(string ns)
		{
			var items = new List<Item>();
			Profile.Start("TargetDatabase::GetNamespaces");
			
			if (ns.Length > 0)
			{
				string sql = string.Format(@"
					SELECT children
						FROM Namespaces 
					WHERE parent = '{0}'", ns);
				string[][] rows = m_database.QueryRows(sql);
				
				foreach (string[] r in rows)
				{
					string[] children = r[0].Split(';');
					foreach (string child in children)
					{
						var item = new NameItem(child, ns + '.' + child, "Namespaces");
						items.AddIfMissing(item);
					}
				}
			}
			else
			{
				string sql = @"
					SELECT parent, children
						FROM Namespaces";
				string[][] rows = m_database.QueryRows(sql);
				
				foreach (string[] r in rows)
				{
					string[] children = r[1].Split(';');
					foreach (string child in children)
					{
						var item = new NameItem(r[0] + '.' + child, ns + '.' + r[0] + '.' + child, "Namespaces");
						items.AddIfMissing(item);
					}
				}
			}
			
			Profile.Stop("TargetDatabase::GetNamespaces");
			return items.ToArray();
		}
		
		public void GetBases(string typeName, List<string> baseNames, List<string> interfaceNames, List<string> allNames)
		{
			Profile.Start("TargetDatabase::GetBases");
			
			if (typeName == "array-type")
			{
				baseNames.AddIfMissing("System.Array");
				allNames.AddIfMissing("System.Array");
				
				interfaceNames.AddIfMissing("System.Collections.IList");
				allNames.AddIfMissing("System.Collections.IList");
				
				interfaceNames.AddIfMissing("System.Collections.Generic.IList`1");
				allNames.AddIfMissing("System.Collections.Generic.IList`1");
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
			
			Profile.Stop("TargetDatabase::GetBases");
		}
		
		public Item[] GetFields(string[] typeNames, bool instanceCall, bool isStaticCall, bool includeProtected)
		{
			Profile.Start("TargetDatabase::GetFields1");
			var items = new List<Item>();
			
			if (typeNames.Length > 0)
			{
				var types = new StringBuilder();
				for (int i = 0; i < typeNames.Length; ++i)
				{
					types.AppendFormat("declaring_root_name = '{0}'", typeNames[i].Replace("'", "''"));
					
					if (i + 1 < typeNames.Length)
						types.Append(" OR ");
				}
				
				string access = includeProtected ? "access < 3" : "(access = 0 OR access = 2)";
				
				string sql;
				if (instanceCall && isStaticCall)
					sql = string.Format(@"
						SELECT name, type_name, declaring_root_name
							FROM Fields 
						WHERE {1} AND ({0})", types.ToString(), access);	// we exclude all private fields (note that this won't affect this methods since the parser will pick up those)
				else
					sql = string.Format(@"
						SELECT name, type_name, declaring_root_name
							FROM Fields 
						WHERE static = {1} AND {2} AND ({0})", types.ToString(), isStaticCall ? "1" : "0", access);
				
				string[][] rows = m_database.QueryRows(sql);
				foreach (string[] r in rows)
				{
					items.AddIfMissing(new NameItem(r[0], r[1] + ' ' + r[0], r[2], r[1]));
				}
			}
			
			Profile.Stop("TargetDatabase::GetFields1");
			return items.ToArray();
		}
		
		public Item[] GetFields(string[] typeNames, bool instanceCall, bool isStaticCall, string name,  bool includeProtected)
		{
			Profile.Start("TargetDatabase::GetFields2");
			var items = new List<Item>();
			
			if (typeNames.Length > 0)
			{
				var types = new StringBuilder();
				for (int i = 0; i < typeNames.Length; ++i)
				{
					types.AppendFormat("declaring_root_name = '{0}'", typeNames[i].Replace("'", "''"));
					
					if (i + 1 < typeNames.Length)
						types.Append(" OR ");
				}
				
				string access = includeProtected ? "access < 3" : "(access = 0 OR access = 2)";
				
				string sql;
				if (instanceCall && isStaticCall)
					sql = string.Format(@"
						SELECT name, type_name, declaring_root_name
							FROM Fields 
						WHERE name = '{0}' AND {2} AND ({1})", name, types.ToString(), access);	// we exclude all private fields (note that this won't affect this methods since the parser will pick up those)
				else
					sql = string.Format(@"
						SELECT name, type_name, declaring_root_name
							FROM Fields 
						WHERE name = '{0}' AND static = {2} AND {3} AND ({1})", name, types.ToString(), isStaticCall ? "1" : "0", access);
				
				string[][] rows = m_database.QueryRows(sql);
				foreach (string[] r in rows)
				{
					items.AddIfMissing(new NameItem(r[0], r[1] + ' ' + r[0], r[2], r[1]));
				}
			}
			
			Profile.Stop("TargetDatabase::GetFields2");
			return items.ToArray();
		}
		
		public Item[] GetTypes(string[] namespaces, string stem)
		{
			Profile.Start("TargetDatabase::GetTypes");
			var items = new List<Item>();
			
			var ns = new StringBuilder();
			if (namespaces.Length > 0)
			{
				ns.Append('(');
				for (int i = 0; i < namespaces.Length; ++i)
				{
					ns.AppendFormat("namespace = '{0}'", namespaces[i]);
					
					if (i + 1 < namespaces.Length)
						ns.Append(" OR ");
				}
				ns.Append(") AND");
			}
			
			string sql;
			if (stem.Length > 0)
				sql = string.Format(@"
					SELECT name, root_name
						FROM Types
					WHERE visibility < 3 AND {0} name GLOB '{1}*'", ns.ToString(), stem);
			else
				sql = string.Format(@"
					SELECT name, root_name
						FROM Types
					WHERE {0} visibility < 3", ns.ToString());
			
			string[][] rows = m_database.QueryRows(sql);
			foreach (string[] r in rows)
			{
				var item = new NameItem(r[0], r[1], "types");
				items.AddIfMissing(item);
			}
			
			Profile.Stop("TargetDatabase::GetTypes");
			return items.ToArray();
		}
		
		public Item[] GetCtors(string[] namespaces, string stem)
		{
			Profile.Start("TargetDatabase::GetCtors");
			var items = new List<Item>();
			
			int badAttrs =
				0x01 |	// abstract
				0x04 |	// interface
				0x10 |	// enum
				0x40;	// delegate
				
			string common = string.Format(@"Methods.static = 0 AND Methods.kind = 6 AND 
				Types.visibility < 3 AND Methods.access < 3 AND (Types.attributes & {0}) = 0 AND
				Methods.declaring_root_name = Types.root_name", badAttrs);
				
			var ns = new StringBuilder();
			if (namespaces.Length > 0)
			{
				ns.Append('(');
				for (int i = 0; i < namespaces.Length; ++i)
				{
					ns.AppendFormat("Types.namespace = '{0}'", namespaces[i]);
					
					if (i + 1 < namespaces.Length)
						ns.Append(" OR ");
				}
				ns.Append(") AND");
			}
			
			string sql;
			if (stem.Length > 0)
				sql = string.Format(@"
					SELECT Methods.display_text, Methods.return_type_name, Types.namespace
						FROM Methods, Types
					WHERE {2} AND
						{0} Types.name GLOB '{1}*'", ns.ToString(), stem, common);
			else
				sql = string.Format(@"
					SELECT Methods.display_text, Methods.return_type_name, Types.namespace
						FROM Methods, Types
					WHERE {0} {1}", ns.ToString(), common);
			
			string[][] rows = m_database.QueryRows(sql);
			foreach (string[] r in rows)
			{
				string nsName = r[2] == "<globals>" || r[2].Length == 0 ? "global" : r[2];
				Item item = DoCreateItem(r[0], nsName + " constructors", 6, r[1]);
				items.AddIfMissing(item);
			}
			
			DoAddDefaultCtors(ns.ToString(), stem, badAttrs, items);
			
			Profile.Stop("TargetDatabase::GetCtors");
			return items.ToArray();
		}
		
		public Item[] GetMembers(string[] typeNames, bool instanceCall, bool isStaticCall, bool includeProtected)
		{
			Profile.Start("TargetDatabase::GetMembers1");
			var items = new List<Item>();
			
			if (typeNames.Length > 0)
			{
				var types = new StringBuilder();
				for (int i = 0; i < typeNames.Length; ++i)
				{
					types.AppendFormat("declaring_root_name = '{0}'", typeNames[i].Replace("'", "''"));
					
					if (i + 1 < typeNames.Length)
						types.Append(" OR ");
				}
				
				string access = includeProtected ? "access < 3" : "(access = 0 OR access = 2)";
				
				string sql;
				if (instanceCall && isStaticCall)
					sql = string.Format(@"
						SELECT display_text, declaring_root_name, kind, return_type_name
							FROM Methods 
						WHERE kind <= 2 AND {1} AND ({0})", types.ToString(), access);
				else
					sql = string.Format(@"
						SELECT display_text, declaring_root_name, kind, return_type_name
							FROM Methods 
						WHERE static = {1} AND kind <= 2 AND {2} AND ({0})", types.ToString(), isStaticCall ? "1" : "0", access);
				
				NamedRows rows = m_database.QueryNamedRows(sql);
				foreach (NamedRow r in rows)
				{
					Item item = DoCreateItem(r["display_text"], r["declaring_root_name"], int.Parse(r["kind"]), r["return_type_name"]);
					items.AddIfMissing(item);
				}
			}
			
			Profile.Stop("TargetDatabase::GetMembers1");
			return items.ToArray();
		}
		
		public Item[] GetMembers(string[] typeNames, bool instanceCall, bool isStaticCall, string name, int arity, bool includeProtected)
		{
			Profile.Start("TargetDatabase::GetMembers2");
			var items = new List<Item>();
			
			if (typeNames.Length > 0)
			{
				var types = new StringBuilder();
				for (int i = 0; i < typeNames.Length; ++i)
				{
					types.AppendFormat("declaring_root_name = '{0}'", typeNames[i].Replace("'", "''"));
					
					if (i + 1 < typeNames.Length)
						types.Append(" OR ");
				}
				
				string access = includeProtected ? "access < 3" : "(access = 0 OR access = 2)";
				
				string sql;
				if (instanceCall && isStaticCall)
					sql = string.Format(@"
						SELECT display_text, declaring_root_name, kind, return_type_name
							FROM Methods 
						WHERE (name = '{0}' OR name = '{1}') AND params_count = {2} AND 
							kind <= 2 AND {4} AND ({3})", name, "get_" + name, arity, types.ToString(), access);
				else
					sql = string.Format(@"
						SELECT display_text, declaring_root_name, kind, return_type_name
							FROM Methods 
						WHERE (name = '{0}' OR name = '{1}') AND params_count = {2} AND 
							static = {4} AND {5} AND kind <= 2 AND ({3})", name, "get_" + name, arity, types.ToString(), isStaticCall ? "1" : "0", access);
				
				NamedRows rows = m_database.QueryNamedRows(sql);
				foreach (NamedRow r in rows)
				{
					Item item = DoCreateItem(r["display_text"], r["declaring_root_name"], int.Parse(r["kind"]), r["return_type_name"]);
					items.AddIfMissing(item);
				}
			}
			
			Profile.Stop("TargetDatabase::GetMembers2");
			return items.ToArray();
		}
		
		public Item[] GetExtensionMethods(string[] typeNames, string[] namespaces)
		{
			Profile.Start("TargetDatabase::GetExtensionMethods1");
			var items = new List<Item>();
			
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
					SELECT Methods.display_text, Methods.return_type_name
						FROM Methods, Types
					WHERE Methods.static = 1 AND Methods.kind = 8 AND
						Methods.declaring_root_name = Types.root_name AND
						({0}) AND ({1})", types.ToString(), ns.ToString());
				
				string[][] rows = m_database.QueryRows(sql);
				foreach (string[] r in rows)
				{
					Item item = DoCreateItem(r[0], "extension methods", 8, r[1]);
					items.AddIfMissing(item);
				}
			}
			
			Profile.Stop("TargetDatabase::GetExtensionMethods1");
			return items.ToArray();
		}
		
		public Item[] GetExtensionMethods(string[] typeNames, string[] namespaces, string name, int arity)
		{
			Profile.Start("TargetDatabase::GetExtensionMethods2");
			var items = new List<Item>();
			
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
					SELECT Methods.display_text, Methods.return_type_name
						FROM Methods, Types
					WHERE Methods.name = '{0}' AND Methods.params_count = '{1}' AND
						Methods.static = 1 AND Methods.kind = 8 AND
						Methods.declaring_root_name = Types.root_name AND
						({2}) AND ({3})", name, arity + 1, types.ToString(), ns.ToString());
				
				string[][] rows = m_database.QueryRows(sql);
				foreach (string[] r in rows)
				{
					Item item = DoCreateItem(r[0], "extension methods", 8, r[1]);
					items.AddIfMissing(item);
				}
			}
			
			Profile.Stop("TargetDatabase::GetExtensionMethods2");
			return items.ToArray();
		}
		
		#region Private Methods
		private Item DoCreateItem(string displayText, string filter, int kind, string type)
		{
			string[] parts = displayText.Split(':');
			Contract.Assert(parts.Length == 6, "expected 6 parts from " + displayText);
			
			string rtype = parts[0];
			string name = parts[2];
			
			Item result;
			if (kind == 1 || kind == 2)
			{
				// property getter or setter
				result = new NameItem(name, rtype + ' ' + name, filter, type);
			}
			else
			{
				string[] gargs = parts[3].Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries);
				string[] argTypes = parts[4].Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries);
				string[] argNames = parts[5].Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries);
				
				if (kind == 3)
				{
					// indexer getter
					result = new MethodItem(rtype, name, gargs, argTypes, argNames, type, filter, '[', ']');
				}
				else if (kind == 4)
				{
					// indexer setter
					argTypes = argTypes.SubArray(0, argTypes.Length - 1);
					argNames = argNames.SubArray(0, argNames.Length - 1);
					result = new MethodItem(rtype, name, gargs, argTypes, argNames, "System.Void", filter, '[', ']');
				}
				else if (kind == 8)
				{
					// extension method
					argTypes = argTypes.SubArray(1);
					argNames = argNames.SubArray(1);
					result = new MethodItem(rtype, name, gargs, argTypes, argNames, type, filter);
				}
				else if (kind == 0 || kind == 6)
				{
					// normal method or constructor
					result = new MethodItem(rtype, name, gargs, argTypes, argNames, type, filter);
				}
				else
					throw new Exception("bad method kind: " + kind);
			}
			
			return result;
		}
				
		private void DoAddDefaultCtors(string ns, string stem, int badAttrs, List<Item> items)
		{
			string common = string.Format("visibility < 3 AND (attributes & {0}) = 0", badAttrs);
			
			if (ns.Length > 0)
				ns = ns.Replace("Types.", string.Empty);
			
			string sql;
			if (stem.Length > 0)
				sql = string.Format(@"
					SELECT attributes, name, namespace
						FROM Types
					WHERE {2} AND
						{0} name GLOB '{1}*'", ns, stem, common);
			else
				sql = string.Format(@"
					SELECT attributes, name, root_name
						FROM Types
					WHERE {0} {1}", ns, common);
			
			string[][] rows = m_database.QueryRows(sql);
			foreach (string[] r in rows)
			{
				int attrs = int.Parse(r[0]);
				if ((attrs & 0x08) != 0)			// struct
				{
					string displayText = string.Format("void:{0}:{1}:::", r[2], r[1]);
					string nsName = r[2] == "<globals>" || r[2].Length == 0 ? "global" : r[2];
					Item item = DoCreateItem(displayText, nsName + " constructors", 6, "System.Void");
					items.AddIfMissing(item);
				}
			}
		}
		#endregion
		
		#region Fields
		private Database m_database;
		#endregion
	}
}
