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
using MCocoa;
using Shared;
using System;
using System.Diagnostics;
using System.Collections.Generic;

#if false
namespace Styler
{
	internal sealed class CSharpComputer : RegexComputer
	{
		public override void Instantiated(Boss boss)
		{
			base.Instantiated(boss);
			
			Boss b = ObjectModel.Create("CsParser");
			m_parses = b.Get<IParses>();
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		protected override void OnComputeRuns(Boss boss, string path, string text, int edit, List<StyleRun> runs)	// threaded
		{
			base.OnComputeRuns(boss, path, text, edit, runs);
			
			Unused.Value = DoParseMatch(path, text, edit, runs);
		}
		
		#region Private Methods
		[ThreadModel(ThreadModel.Concurrent)]
		private Parse DoParseMatch(string path, string text, int edit, List<StyleRun> runs)		// threaded
		{
			Parse parse = m_parses.Parse(path, edit, text);
			
			int length = parse.ErrorLength;
			if (length > 0)
			{
				// We can't highlight control characters because they have zero width so 
				// we'll grow to the left until we find a non-control character.
				int offset = parse.ErrorIndex;
				while (offset > 0 && offset < text.Length && char.IsControl(text, offset) && text[offset] != '\t')
				{
					--offset;
					++length;
				}
				
				runs.Add(new StyleRun(offset, length, StyleType.Error));
			}
			
			if (parse.Globals != null)
				DoMatchScope(parse.Globals, runs);
			
			return parse;
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		private void DoMatchScope(CsTypeScope scope, List<StyleRun> runs)		// threaded
		{
			foreach (CsType type in scope.Types)
			{
				runs.Add(new StyleRun(type.NameOffset, type.Name.Length, StyleType.Type));
				
				foreach (CsMember member in type.Members)
				{
					if (!(member is CsField))
						if (member.Name != "<this>")
							runs.Add(new StyleRun(member.NameOffset, member.Name.Length, StyleType.Member));
						else
							runs.Add(new StyleRun(member.NameOffset, member.Name.Length - 2, StyleType.Member));
				}
				
				DoMatchScope(type, runs);
			}
			
			CsNamespace ns = scope as CsNamespace;
			if (ns != null)
			{
				foreach (CsNamespace n in ns.Namespaces)
				{
					DoMatchScope(n, runs);
				}
			}
		}
		#endregion
		
		#region Fields
		private IParses m_parses;
//		private CsGlobalNamespace m_globals;
//		private Token[] m_tokens;
//		private Token[] m_comments;
		#endregion
	}
}
#endif
