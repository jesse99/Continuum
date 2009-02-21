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
using System;
using System.Collections.Generic;

namespace Shared
{
	// Main interface for language bosses.
	public interface IStyler : IInterface
	{
		// Asynchronously computes the style runs and calls the callback on the 
		// main thread when finished. Boss needs IText and IStyles interfaces.
		// Note that the runs given to IStyles will cover the text.
		void Apply(Boss boss, Action callback);
		
		// Like the above except there is a delay before styling begins. Queue can 
		// be called multiple times and any queue requests which have not yet 
		// finished are dropped.
		void Queue(Boss boss, Action callback);
		
		// Returns true if the language supports showing leading/trailing tabs and
		// spaces.
		bool StylesWhitespace {get;}
	}
}
