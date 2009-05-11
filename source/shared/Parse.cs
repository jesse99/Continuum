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

using Gear.Helpers;
using System;
using System.Diagnostics;

namespace Shared
{
	// This is the object included with the "parsed file" broadcast.
	[ThreadModel(ThreadModel.Concurrent)]
	public sealed class Parse
	{
		public Parse(string path, int edit, int index, int length, CsGlobalNamespace globals, Token[] comments, Token[] tokens)
		{
			Contract.Requires(!string.IsNullOrEmpty(path), "path is null or empty");
			Contract.Requires(index >= 0, "index is negative");
			Contract.Requires(length >= 0, "length is negative");
			Contract.Requires(comments != null, "comments is null");
			Contract.Requires(tokens != null, "tokens is null");
			Contract.Requires(globals != null || length >= 0, "null globals but error length is not set");
			
			Path = path;
			Edit = edit;
			ErrorIndex = index;
			ErrorLength = length;
			Globals = globals;
			Comments = comments;
			Tokens = tokens;
		}
		
		// Edit count for the text this parse is associated with.
		public string Path {get; private set;}
		
		// Edit count for the text this parse is associated with.
		public int Edit {get; private set;}
		
		// Index and length of the text associated with the first parser error.
		// If there was no error ErrorLength will be zero.
		public int ErrorIndex {get; private set;}
		
		public int ErrorLength {get; private set;}
		
		// May be null if the code is really messed up.
		public CsGlobalNamespace Globals {get; private set;}
		
		// This contains all of the tokens except for comments and preprocess
		// directives.
		public Token[] Tokens {get; private set;}
		
		public Token[] Comments {get; private set;}
	}
}
