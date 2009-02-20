// Copyright (C) 2008 Jesse Jones
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
using Mono.Cecil;
using Shared;
using System.Collections.Generic;

namespace ObjectModel
{	
	public sealed class SourceLine
	{
		public SourceLine(string path, int line, string hash)
		{
			Path = path;
			Line = line;
			AssemblyHash = hash;
		}
		
		// Absolute path to the compilation unit. May be empty.
		public string Path {get; private set;}

		// Line number within the compilation unit. May be -1.
		public int Line {get; private set;}

		public string AssemblyHash {get; private set;}
	}
	
	// Interface used to perform some standard queries against the type database.
	internal interface IObjectModel : IInterface
	{					
		// Returns the time in ticks at which the assembly with the specified hash was built.
		long GetBuildTime(string hash);
		
		SourceLine[] FindMethodSources(string fullName);
		
		SourceLine[] FindTypeSources(string fullName);

		string[] FindTypeAssemblyPaths(string fullName);
		
		TypeAttributes[] FindAttributes(string fullName);

		Tuple2<string, TypeAttributes>[] FindImplementors(string fullName);

		Tuple2<string, TypeAttributes>[] FindBases(string fullName);

		// If there are more than maxResults results then the last tuple will be
		// Ellipsis, 0.
		Tuple2<string, TypeAttributes>[] FindDerived(string fullName, int maxResults);
		
		// Returns the full name, file name, kind of methods/types which are named name.
		Tuple3<string, string, int>[] FindInfo(string name, int maxResults);
	} 
}
