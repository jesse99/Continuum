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
using Shared;
using System;
using System.IO;
using System.Linq;

namespace TextEditor
{
	internal sealed class CanOpen : ICanOpen
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public bool Can(string path)
		{
			// See if the extension is one we want to handle.
			// TODO: might want a rich-text language so users can have more
			// control over this.
			string ext = Path.GetExtension(path);
			if (ext == ".rtf")
				return true;
			
			// See if the extension matches one of our languages.
			Boss boss = ObjectModel.Create("Stylers");
			string fileName = Path.GetFileName(path);
			foreach (IFindLanguage find in boss.GetRepeated<IFindLanguage>())
			{
				if (find.FindByExtension(fileName) != null)
					return true;
			}
			
			// If the file starts with a shebang then it is a script and we want
			// to open it as a text file.
			if (ext.Length == 0)
				if (DoHasShebang(path))
					return true;
			
			return false;
		}
		
		#region Private Methods
		private bool DoHasShebang(string path)
		{
			bool has = false;
			
			try
			{
				using (var reader = new StreamReader(path))
				{
					string line = reader.ReadLine();
					has = line != null && line.StartsWith("#!");
				}
			}
			catch
			{
			}
			
			return has;
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		#endregion
	}
}
