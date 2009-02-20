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
using System.Collections.Generic;

namespace Shared
{	
	// Repeated interface used for source code control systems (e.g. svn and git).
	// Normally these are accessed via Shared.Sccs.
	public interface ISccs : IInterface
	{
		// Normally something like "Svn" or "Git".
		string Name {get;}
		
		// Returns false if the rename cannot be done.
		bool Rename(string oldPath, string newPath);
						
		// Returns false if the duplicate cannot be done.
		bool Duplicate(string path);
						
		// Returns all the commands the sccs supports. These will be things like
		// "Svn remove", "Svn info", etc.
		string[] GetCommands();

		// Returns the commands which can be applied to every specified path.
		string[] GetCommands(IEnumerable<string> paths);

		// Executes the command. May pop up a dialog for additional input.
		void Execute(string command, string path);
	} 
}
