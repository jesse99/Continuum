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
		
		public string FindAssembly(string fullName)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			
			string sql = string.Format(@"
				SELECT hash
					FROM Types 
				WHERE type = '{0}'", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			return rows.Length > 0 ? rows[0][0] : null;
		}
		
		public string FindMethodType(string fullName, string name, int numArgs)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			
			string sql = string.Format(@"
				SELECT return_type, arg_names
					FROM Methods 
				WHERE declaring_type = '{0}' AND name = '{1}'", fullName, name);
			string[][] rows = m_database.QueryRows(sql);
			
			var result = from r in rows
				where r[1].Count(c => c == ':') == numArgs
				select r[0];

			return result.Count() == 1 ? result.First() : null;
		}
		
		public string FindFieldType(string fullName, string name)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			Trace.Assert(!string.IsNullOrEmpty(name), "name is null or empty");
			
			string type = null;
			
			while (type == null && fullName != null && fullName != "Sytem.Object")
			{				
				string sql = string.Format(@"
					SELECT name, type, attributes
						FROM Fields 
					WHERE declaring_type = '{0}'", fullName);
				string[][] rows = m_database.QueryRows(sql);
				
				for (int i = 0; i < rows.Length && type == null; ++i)
				{
					if (rows[i][0] == name)
						if ((ushort.Parse(rows[i][2]) & 1) == 0)	// no private base fields
							type = rows[i][1];
				}
				
				fullName = DoFindBaseType(fullName);
			}
			
			return type;
		}
		
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
		
		#region Private Methods
		private string DoFindBaseType(string fullName)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			
			if (fullName == "System.Object")
				return null;
				
			string sql = string.Format(@"
				SELECT DISTINCT base_type
					FROM Types
				WHERE type = '{0}' OR type GLOB '{0}<*'", fullName);
			string[][] rows = m_database.QueryRows(sql);
			
			return rows.Length > 0 ? rows[0][0] : null;
		}
		#endregion
		
		#region Fields
		private Database m_database;
		#endregion
	}
}
