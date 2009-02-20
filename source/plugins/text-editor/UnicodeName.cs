// Copyright (C) 2007-2008 Jesse Jones
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
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TextEditor
{
	internal sealed class UnicodeName : IUnicodeName
	{		
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}

		public string GetName(char ch)
		{
			string name = null;
			
			List<string> names = DoGetNames();
			if (names != null && names.Count == 65536)
			{
				name = names[(int) ch];
				if (name == "-")
					name = "invalid code point";
					
				name = string.Format("0x{0:X4} {1}", (int) ch, name);
			}
			
			return name;
		}
			
		#region Private Methods 
		private List<string> DoGetNames()
		{
			List<string> names = m_names.Target as List<string>;
			
			if (names == null)
			{
				try
				{
					string resourcesPath = NSBundle.mainBundle().resourcePath().ToString();
					string path = Path.Combine(resourcesPath, "UnicodeNames.txt.gz");
					using (Stream stream = File.OpenRead(path))
					{
						names = new List<string>(65536);
						using (GZipInputStream zip = new GZipInputStream(stream))
						{
							using (StreamReader reader = new StreamReader(zip)) 
							{
								while (true)
								{
									string line = reader.ReadLine();
									if (line == null)
										break;
									names.Add(line);
								}
							}
						}
					}
					
					m_names.Target = names;
				}
				catch (Exception e)
				{
					Log.WriteLine(TraceLevel.Error, "Errors", "Coudn't process UnicodeNames.txt.gz.");
					Log.WriteLine(TraceLevel.Error, "Errors", "{0}", e);
				}
			}
			
			return names;
		}
		#endregion
		
		#region Fields 
		private Boss m_boss;
		private WeakReference m_names = new WeakReference(null);
		#endregion
	} 
}	