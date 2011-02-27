// Copyright (C) 2010 Jesse Jones
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
using System.Collections.Generic;
using System.IO;

namespace DefaultBuilder
{
	// Note that this is designed to build simple projects. Anything that is at all complicated
	// should be built using waf, make, or nant.
	internal sealed class CanBuild : ICanBuild
	{
		public CanBuild()
		{
			foreach (string glob in CBuilder.Globs)
			{
				m_builders.Add(glob.Substring(1), "CBuilder");
			}
			
			foreach (string glob in CppBuilder.Globs)
			{
				m_builders.Add(glob.Substring(1), "CppBuilder");
			}
			
			foreach (string glob in CSharpBuilder.Globs)
			{
				m_builders.Add(glob.Substring(1), "CSharpBuilder");
			}
			
			foreach (string glob in FSharpBuilder.Globs)
			{
				m_builders.Add(glob.Substring(1), "FSharpBuilder");
			}
		}
		
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public IBuilder GetBuilder(string dir)
		{
			IBuilder builder = null;
			
			List<string> candidates = DoFindBuilder(dir);
			if (candidates.Count == 1 && candidates[0] == "CSharpBuilder")
			{
				if (DoHasNib(dir))
					candidates[0] = "MonoMacBuilder";
			}
			
			if (candidates.Count == 1 && candidates[0] != null)
			{
				Boss boss = ObjectModel.Create(candidates[0]);
				builder = boss.Get<IBuilder>();
			}
			
			return builder;
		}
		
		#region Private Methods
		private List<string> DoFindBuilder(string dir)
		{
			var candidates = new List<string>();
			
			foreach (string file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
			{
				string ext = Path.GetExtension(file);
				if (m_builders.ContainsKey(ext))
				{
					candidates.AddIfMissing(m_builders[ext]);
				}
			}
			
			return candidates;
		}
		
		private bool DoHasNib(string dir)
		{
			string[] dirs = Directory.GetDirectories(dir, "*.nib", SearchOption.AllDirectories);
			if (dirs.Length > 0)
				return true;
			
			dirs = Directory.GetFiles(dir, "*.xib", SearchOption.AllDirectories);
			if (dirs.Length > 0)
				return true;
			
			return false;
		}
		#endregion
		
		#region Fields 
		private Boss m_boss; 
		private Dictionary<string, string> m_builders = new Dictionary<string, string>
		{
			{".m", null},		// we need these in our table to ensure that we don't try to build a project which requires multiple compilers
			{".d", null},
		};
		#endregion
	} 
}
