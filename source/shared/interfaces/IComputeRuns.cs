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
using System;
using System.Collections.Generic;

#if false
namespace Shared
{
	// Primary interface on language bosses.
	public interface IComputeRuns : IInterface
	{
		// Computes style runs and updates ICachedStyleRuns on the boss. 
		// Note that this is called from a thread.
		[ThreadModel(ThreadModel.Concurrent)]
		void ComputeRuns(Boss boss, string path, string text, int edit);
		
		// Returns true if the language supports showing leading/trailing tabs and
		// spaces.
		[ThreadModel(ThreadModel.Concurrent)]
		bool StylesWhitespace {get;}
	}
}
#endif
