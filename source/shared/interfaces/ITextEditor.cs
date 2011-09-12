// Copyright (C) 2008-2009 Jesse Jones
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

namespace Shared
{
	// This is the primary interface for the text editor plugin.
	public interface ITextEditor : IInterface
	{
		// Returns the full path to the file being edited. May be null if the document
		// is not on disk.
		string Path {get;}
		
		// Either something like "c#' or "python" or null.
		string Language {get;}
		
		// Returns a unique identifier for the document. If the document is on disk
		// it will be Path, otherwise it will be a string like "untitled2".
		string Key {get;}
		
		// If true the text can be changed.
		bool Editable {get; set;}
		
		// If the line number is too large the last line will be shown.
		// Insertion point will be col or the start of the line if col is -1.
		// tabWidth is used for tools like gmcs which think tabs are 8 characters.
		void ShowLine(int line, int col, int tabWidth);
		
		// Save any changes (if the document is on disk).
		void Save();
		
		LiveRange GetRange(NSRange range);
		
		// Returns a new (and hidden) annotation window anchored to the specified
		// character range.
		ITextAnnotation GetAnnotation(NSRange range, AnnotationAlignment alignment);
		
		// Returns a rectangle in window coordinates that encloses the characters
		// in the range.
		NSRect GetBoundingBox(NSRange range);
		
		// Briefly displays a translucent informational window in the middle of the text window.
		void ShowInfo(string text);
		
		// Briefly displays a translucent warning window in the middle of the text window.
		void ShowWarning(string text);
	}
}
