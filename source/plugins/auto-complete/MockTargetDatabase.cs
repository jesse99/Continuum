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
				has = Types != null && Types.Contains(typeName);
			}
			
			return has;
		}
		
		public Member[] GetNamespaces(string ns)
		{
			throw new NotImplementedException();
		}
		
		public void GetBases(string typeName, List<string> baseNames, List<string> interfaceNames, List<string> allNames)
		{
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
			else if (BaseClasses != null)
			{
				string b;
				if (BaseClasses.TryGetValue(typeName, out b))
				{
					baseNames.AddIfMissing(b);
					allNames.AddIfMissing(b);
				}
			}
		}
		
		public Member[] GetCtors(string ns, string stem)
		{
			throw new NotImplementedException();
		}

		public Member[] GetMembers(string[] typeNames, bool instanceCall, bool isStaticCall)
		{
			var result = new List<Member>();
			
			if (Members != null)
			{
				foreach (string typeName in typeNames)
				{
					Member[] members;
					if (Members.TryGetValue(typeName, out members))
					{
						foreach (Member member in members)
						{
							result.AddIfMissing(member);
						}
					}
				}
			}
			
			return result.ToArray();
		}
		
		public Member[] GetMembers(string[] typeNames, bool instanceCall, bool isStaticCall, string name, int arity)
		{
			var result = new List<Member>();
			
			if (Members != null)
			{
				foreach (string typeName in typeNames)
				{
					Member[] members;
					if (Members.TryGetValue(typeName, out members))
					{
						foreach (Member member in members)
						{
							if (member.Name == name && member.Arity == arity)
								result.AddIfMissing(member);
						}
					}
				}
			}
			
			return result.ToArray();
		}
		
		public Member[] GetExtensionMethods(string targetType, string[] typeNames, string[] namespaces)
		{
			var result = new List<Member>();
			
			if (ExtensionMethods != null)
			{
				foreach (string typeName in typeNames)
				{
					foreach (string ns in namespaces)
					{
						Member[] members;
						if (ExtensionMethods.TryGetValue(ns + "." + typeName, out members))
						{
							foreach (Member member in members)
							{
								result.AddIfMissing(member);
							}
						}
					}
				}
			}
			
			return result.ToArray();
		}
		
		public Member[] GetExtensionMethods(string targetType, string[] typeNames, string[] namespaces, string name, int arity)
		{
			var result = new List<Member>();
			
			if (ExtensionMethods != null)
			{
				foreach (string typeName in typeNames)
				{
					foreach (string ns in namespaces)
					{
						Member[] members;
						if (ExtensionMethods.TryGetValue(ns + "." + typeName, out members))
						{
							foreach (Member member in members)
							{
								if (member.Name == name && member.Arity == arity)
									result.AddIfMissing(member);
							}
						}
					}
				}
			}
			
			return result.ToArray();
		}
		
		public Member[] GetFields(string[] typeNames, bool instanceCall, bool isStaticCall)
		{
			var result = new List<Member>();
			
			if (Fields != null)
			{
				foreach (string typeName in typeNames)
				{
					Member[] members;
					if (Fields.TryGetValue(typeName, out members))
					{
						foreach (Member member in members)
						{
							result.AddIfMissing(member);
						}
					}
				}
			}
			
			return result.ToArray();
		}
		
		public Member[] GetFields(string[] typeNames, bool instanceCall, bool isStaticCall, string name)
		{
			var result = new List<Member>();
			
			if (Fields != null)
			{
				foreach (string typeName in typeNames)
				{
					Member[] members;
					if (Fields.TryGetValue(typeName, out members))
					{
						foreach (Member member in members)
						{
							if (member.Name == name)
								result.AddIfMissing(member);
						}
					}
				}
			}
			
			return result.ToArray();
		}
		
		public List<string> Types {get; set;}
		
		public Dictionary<string, string> BaseClasses {get; set;}
		
		public Dictionary<string, Member[]> Members {get; set;}

		public Dictionary<string, Member[]> ExtensionMethods {get; set;}

		public Dictionary<string, Member[]> Fields {get; set;}
	}
}
#endif
