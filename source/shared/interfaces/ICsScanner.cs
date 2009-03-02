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
using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Shared
{
	[Serializable]
	public sealed class CsScannerException : Exception
	{
		public CsScannerException()
		{
		}
		
		public CsScannerException(string text) : base(text)
		{
		}
		
		public CsScannerException(string format, params object[] args) : base(string.Format(format, args))
		{
		}
		
		public CsScannerException(string text, Exception inner) : base (text, inner)
		{
		}
		
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		private CsScannerException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
	
	// Interface used to extract tokens from C# code.
	public interface ICsScanner : IInterface
	{
		void Init(string text);
		
		void Init(string text, int offset);
		
		// Returns the current token. Once all the characters have been consumed 
		// the token's kind will be invalid.
		Token Token {get;}
		
		// Returns the nth token from the current token.
		Token LookAhead(int delta);
		
		// Skips whitespace and comments and advances to the next token. Should 
		// only be called if Token is valid.
		void Advance();
		
		// Returns all of the preprocessing directives that have been encountered
		// so far. 
		CsPreprocess[] Preprocess {get;}
	}
}
