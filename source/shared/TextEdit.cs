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
using MCocoa;
using System;

namespace Shared
{
	// Used with the "text changed" broadcast notification.
	public sealed class TextEdit
	{
		// The TextEditor boss.
		public Boss Boss {get; set;}
		
		// True if the change was caused by the user editing the text (as opposed
		// to something like reverting the file or a refactor command).
		public bool UserEdit {get; set;}
		
		// The range of the new text.
		public NSRange EditedRange {get; set;}
		
		// The difference between the old selection and the new text.
		public int ChangeInLength {get; set;}
		
		// The first line in the new text.
		public int StartLine {get; set;}
		
		// The difference between the number of lines before and after the edit.
		public int ChangeInLines {get; set;}
	}
}
