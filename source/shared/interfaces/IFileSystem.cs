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

namespace Shared
{
	// File system methods that know how to deal with platform quirks
	// better than System.IO.
	public interface IFileSystem : IInterface
	{
		// Open the file with its associated application. Returns false if the file could
		// not be opened.
		bool Launch(string path);
		
		// Returns the size of a file or the files within a directory.
		// Note that directories and files that start with a '.' are
		// ignored.
		long GetBytes(string path);
		
		// Uses the locate command to find the path. The search is case insensitive
		// and will match partial paths, e.g. "/Foo.cs" or "/Foo/". Note that this will
		// not return files within .svn or .Trashes directories.
		string[] LocatePath(string path);
		
		// Returns a path to a temporary file where the file name starts with
		// prefix and ends with extension.
		string GetTempFile(string prefix, string extension);
		
		// This works like Directory.GetFiles with the AllDirectories option but
		// does not fall down if the contents of the path are changing.
		string[] GetAllFiles(string path, string glob);
	}
}
