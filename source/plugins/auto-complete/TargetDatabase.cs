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
		
		public Tuple2<string, string>[] FindMethodsWithPrefix(string fullName, string prefix, int numArgs)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			Trace.Assert(!string.IsNullOrEmpty(prefix), "prefix is null or empty");
			Trace.Assert(numArgs >= 0, "numArgs is negative");
			
			string sql = string.Format(@"
				SELECT return_type, arg_names, name
					FROM Methods 
				WHERE declaring_type = '{0}' AND name GLOB '{0}*'", fullName, prefix);
			string[][] rows = m_database.QueryRows(sql);
			
			var result = from r in rows
				where r[1].Count(c => c == ':') == numArgs
				select Tuple.Make(r[0], r[2]);

			return result.ToArray();
		}
		
		public Tuple2<string, string>[] FindFields(string fullName)
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
					fields.Add(Tuple.Make(rows[i][1], rows[i][0]));
			}
			
			return fields.ToArray();
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
				
		#region Fields
		private Database m_database;
		#endregion
	}
}
