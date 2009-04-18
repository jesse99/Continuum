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
using System.Diagnostics;

namespace Shared
{
	public sealed class Parse
	{
		public Parse(int edit, int index, int length, CsGlobalNamespace globals, Token[] comments, Token[] tokens)
		{
			Contract.Requires(index >= 0, "index is negative");
			Contract.Requires(length >= 0, "length is negative");
			Contract.Requires(globals != null, "globals is null");
			Contract.Requires(comments != null, "comments is null");
			Contract.Requires(tokens != null, "tokens is null");
			
			Edit = edit;
			ErrorIndex = index;
			ErrorLength = length;
			Globals = globals;
			Comments = comments;
			Tokens = tokens;
		}
		
		// Edit count for the text this parse is associated with.
		public int Edit {get; private set;}
		
		// Index and length of the text associated with the first parser error.
		// If there was no error ErrorLength will be zero.
		public int ErrorIndex {get; private set;}
		
		public int ErrorLength {get; private set;}
		
		public CsGlobalNamespace Globals {get; private set;}
		
		// This contains all of the tokens except for comments and preprocess
		// directives.
		public Token[] Tokens {get; private set;}
		
		public Token[] Comments {get; private set;}
	}
	
	// Uses to access parser info for C# files. Note that this information is retained
	// even after the associated window closes and is only released after a successful
	// build for files with no open windo.
	public interface IParses : IInterface
	{
		// Called when a text document is opened or becomes dirty. It will be parsed
		// within a thread and when finished "parsed file" will be broadcast with the
		// path as the argument.
		void OnEdit(string language, string path, int edit, string text);
		
		// Attempts to return the current parse information for the specified file.
		// May return null if the file has not been parsed yet or the parse info
		// has been purged. Note that this is thread safe.
		Parse TryParse(string path);
		
		// Blocks until the file is parsed and returns the result.Note that this is 
		// thread safe.
		Parse Parse(string path, int edit, string text);
		
		// Searches globals in each parse and returns a matching type or null.
		CsType FindType(string fullName);
		
		// Searches globals in each parse and all of the types whose names 
		// start with stem.
		CsType[] FindTypes(string ns, string stem);
	}
}
