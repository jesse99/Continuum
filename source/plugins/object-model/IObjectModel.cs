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
//using Shared;
using System;
using System.Collections.Generic;

namespace ObjectModel
{
	internal sealed class SourceInfo
	{
		public SourceInfo(string source, string path, int line)
		{
			Source = source;
			Path = path;
			Line = line;
		}
		
		// File name for types, full method name for methods.
		public string Source {get; private set;}
		
		// Absolute path to the compilation unit. May be empty.
		public string Path {get; private set;}
		
		// Line number within the compilation unit. May be -1.
		public int Line {get; private set;}
	}
	
	[Serializable]
	internal enum TypeVisibility
	{
		Public,
		Family,
		Internal,
		Private,
	}
	
	[Flags]
	[Serializable]
	internal enum TypeFlags
	{
		Abstract = 0x01,
		Sealed = 0x02,
		Interface = 0x04,
		Value = 0x08,
		Enum = 0x10,
	}
	
	internal sealed class TypeInfo
	{
		public TypeInfo(int assembly, string rootName, int attributes, int visibility)
		{
			Assembly = assembly;
			RootName = rootName;
			Visibility = (TypeVisibility) visibility;
			Flags = (TypeFlags) attributes;
		}
		
		public int Assembly {get; private set;}
		
		public string RootName {get; private set;}
		
		public TypeVisibility Visibility {get; private set;}
		
		public TypeFlags Flags {get; private set;}
	}
	
	// Interface used to perform some standard queries against the type database.
	internal interface IObjectModel : IInterface
	{
		TypeInfo[] FindTypes(string name, int max);
		
		SourceInfo[] FindMethodSources(string name, int max);
		
		SourceInfo[] FindTypeSources(string[] rootNames, int max);
		
		string FindAssemblyPath(int assembly);
		
		TypeInfo[] FindBases(string rootName);
		
		TypeInfo[] FindDerived(string rootName, int max);
		
		TypeInfo[] FindImplementors(string rootName, int max);
	}
}
