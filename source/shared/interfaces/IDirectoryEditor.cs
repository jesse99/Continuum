// Copyright (C) 2008-2010 Jesse Jones
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

namespace Shared
{
	// This is the primary interface for the plugin which pops up a window
	// showing a directory and allowing files to be added or deleted. Note
	// that when a directory editor is opened it will broadcast "opened directory"
	// with the boss of the new directory editor.
	public interface IDirectoryEditor : IInterface
	{
		// Returns the full path to the directory being edited.
		string Path {get;}
		
		// Returns the full paths of the selected items.
		string[] SelectedPaths();
		
		// Returns true if the directory or file name (not path) should
		// not be displayed.
		bool IsIgnored(string name);
		
		// Returns the time at which the the last build started (for this
		// session). If the directory was not built DateTime.MinValue is
		// returned.
		DateTime BuildStartTime {get;}
		
		// True if spaces should be added before method arguments.
		bool AddSpace {get;}
		
		// True if curly braces should be placed on their own lines.
		bool AddBraceLine {get;}
		
		// True if tabs hould be inserted as tabs (instead of spaces).
		bool UseTabs {get;}
		
		// Number of spaces to use if UseTabs is false.
		int NumSpaces {get;}
	}
}
