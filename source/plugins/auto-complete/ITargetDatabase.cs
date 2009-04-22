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

namespace AutoComplete
{
	internal interface ITargetDatabase
	{
		// Returns true if the name is a special or type name.
		bool HasType(string typeName);
		
		Member[] GetNamespaces(string ns);

		// Returns type names for the type's base class and any interfaces it directly
		// implements.
		void GetBases(string typeName, List<string> baseNames, List<string> interfaceNames, List<string> allNames);
		
		Member[] GetTypes(string[] namespaces, string stem);
		Member[] GetCtors(string[] namespaces, string stem);

		// Returns the (unique) members for all of the types, but not the methods
		// which extend the types. 
		Member[] GetMembers(string[] typeNames, bool instanceCall, bool isStaticCall, bool includeProtected);
		Member[] GetMembers(string[] typeNames, bool instanceCall, bool isStaticCall, string name, int arity, bool includeProtected);
		
		Member[] GetExtensionMethods(string targetType, string[] typeNames, string[] namespaces);
		Member[] GetExtensionMethods(string targetType, string[] typeNames, string[] namespaces, string name, int arity);
		
		Member[] GetFields(string[] typeNames, bool instanceCall, bool isStaticCall, bool includeProtected);
		Member[] GetFields(string[] typeNames, bool instanceCall, bool isStaticCall, string name, bool includeProtected);
	}
}
