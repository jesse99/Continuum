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
	internal sealed class ResolvedTarget
	{
		public ResolvedTarget(string fullName, CsType type, string hash, bool isInstance)
		{
			Trace.Assert(!string.IsNullOrEmpty(fullName), "fullName is null or empty");
			Trace.Assert(type != null || hash != null, "type and hash are both null");
			
			FullName = fullName;
			Type = type;
			Hash = hash;
			IsInstance = isInstance;
		}
		
		// Will always be set.
		public string FullName {get; private set;}
		
		// Set if the type was found in globals.
		public CsType Type {get; private set;}
		
		// Set if the fullName was found in the database.
		public string Hash {get; private set;}
		
		// True if the target is an instance of the type (as opposed to the
		// type itself).
		public bool IsInstance {get; private set;}
	}
}
