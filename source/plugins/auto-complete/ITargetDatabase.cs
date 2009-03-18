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

namespace AutoComplete
{
	internal interface ITargetDatabase
	{
		// Returns either null or the assembly hash the specified type is declared within.
		string FindAssembly(string fullName);
		
		// Returns either null or the full type name of the result type of the specified
		// method.
//		string FindMethodType(string fullName, string name, int numArgs);

		// Returns all of the method return types/names in the specified type (but not 
		// bases) which start with the specified prefix.
		Tuple2<string, string>[] FindMethodsWithPrefix(string fullName, string prefix, int numArgs, bool includeInstanceMembers);
		
		// Returns either null or the full type name of a field defined in the fullName
		// type or one of its base classes.
//		string FindFieldType(string fullName, string name);
		
		// Returns a list of field type/names for the specified type (but not its bases).
		Tuple2<string, string>[] FindFields(string fullName, bool includeInstanceMembers);
		
		// Returns either null or the full name of the type's base class.
		string FindBaseType(string fullName);
		
		// Returns the interfaces directly implemented by the type.
		string[] FindInterfaces(string fullName);
	}
}
