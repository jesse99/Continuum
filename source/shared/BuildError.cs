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

using Gear.Helpers;
using System;
using System.Diagnostics;

namespace Shared
{
	public sealed class BuildError
	{
		public BuildError(string tool, string file, string message, int line, int col, bool isError)
		{
			Contract.Requires(!string.IsNullOrEmpty(tool), "tool is null or empty");
			Contract.Requires(!string.IsNullOrEmpty(file), "file is null or empty");
			Contract.Requires(!string.IsNullOrEmpty(message), "message is null or empty");
			Contract.Requires(line > 0 || line == -1, "line is oor: " + line);
			Contract.Requires(col > 0 || col == -1, "col is oor: " + col);
			
			Tool = tool;
			File = file;
			Message = message;
			Line = line;
			Column = col;
			IsError = isError;
			
			ActiveObjects.Add(this);
		}
		
		// "gmcs", "make", etc.
		public string Tool {get; private set;}
		
		// Path to the file with the problem. May be a relative path.
		public string File {get; private set;}
		
		public string Message {get; private set;}
		
		// May be -1. One based value.
		public int Line {get; set;}
		
		// May be -1. One based value.
		public int Column {get; set;}
		
		// Returns false if it's a warning.
		public bool IsError {get; private set;}
	}
}
