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
using Gear.Helpers;

namespace Shared
{
	// Uses to access parser info for C# files. Note that this information is retained
	// even after the associated window closes and is only released after a successful
	// build for files with no open window.
	public interface IParses : IInterface
	{
		// Attempts to return the current parse information for the specified file.
		// May return null if the file has not been parsed yet or the parse info
		// has been purged. Note that this is thread safe.
		[ThreadModel(ThreadModel.Concurrent)]
		Parse TryParse(string path);
		
		// Blocks until the file is parsed and returns the result. Note that this is 
		// thread safe. This will normally not throw, but may return a parse with
		// errors.
		[ThreadModel(ThreadModel.Concurrent)]
		Parse Parse(string path, int edit, string text);
		
		// Searches globals in each parse and returns a matching type or null.
		CsType FindType(string fullName);
		
		// Searches globals in each parse and all of the types whose names 
		// start with stem. If ns is null the global namespace is searched.
		CsType[] FindTypes(string ns, string stem);
		
		// Returns the namespaces under ns. Note that the returned names don't
		// include the ns part of the name.
		string[] FindNamespaces(string ns);
	}
}
