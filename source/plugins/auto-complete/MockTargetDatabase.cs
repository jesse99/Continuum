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

#if TEST
namespace AutoComplete
{
	internal sealed class MockTargetDatabase : ITargetDatabase
	{
		public string FindAssembly(string fullName)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			
			string hash = null;
			
			if (Hashes != null)
				Hashes.TryGetValue(fullName, out hash);
			
			return hash;
		}
		
		public Tuple2<string, string>[] FindMethodsWithPrefix(string fullName, string prefix, int numArgs, bool includeInstanceMembers)
		{
			return new Tuple2<string, string>[0];
		}
				
		public Tuple2<string, string>[] FindFields(string fullName, bool includeInstanceMembers)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			
			var fields = new List<Tuple2<string, string>>();
			
			if (BaseFieldTypes != null)
			{
				foreach (var entry in BaseFieldTypes)
				{
					if (entry.Key.StartsWith(fullName + "+"))
					{
						fields.Add(Tuple.Make(entry.Value, entry.Key.Substring(fullName.Length + 1)));
					}
				}
			}
			
			return fields.ToArray();
		}
		
		public string FindBaseType(string fullName)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			
			string type = null;
			
			if (BaseClasses != null)
				BaseClasses.TryGetValue(fullName, out type);
			
			return type;
		}
		
		public string[] FindInterfaces(string fullName)
		{
			return new string[0];
		}
		
		public Dictionary<string, string> Hashes {get; set;}
		
		public Dictionary<string, string> BaseClasses {get; set;}
		
		public Dictionary<string, string> BaseFieldTypes {get; set;}
	}
}
#endif
