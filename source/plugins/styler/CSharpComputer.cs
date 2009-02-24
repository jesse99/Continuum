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
using Shared;
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Styler
{
	internal sealed class CSharpComputer : RegexComputer
	{
		public override void Instantiated(Boss boss)
		{
			base.Instantiated(boss);
			
			Boss b = ObjectModel.Create("CsParser");
			m_parser = b.Get<ICsParser>();
		}
		
		protected override CsGlobalNamespace OnComputeRuns(string text, int edit, List<StyleRun> runs)	// threaded
		{
			Unused.Value = base.OnComputeRuns(text, edit, runs);
			
			return DoParseMatch(text, runs);
		}
		
		#region Private Methods
		private CsGlobalNamespace DoParseMatch(string text, List<StyleRun> runs)		// threaded
		{
			int offset, length;
			CsGlobalNamespace globals = m_parser.TryParse(text, out offset, out length);
			if (length > 0)
			{
				// We can't highlight control characters because they have zero width so 
				// we'll grow to the left until we find a non-control character.
				while (offset > 0 && char.IsControl(text, offset) && text[offset] != '\t')
				{
					--offset;
					++length;
				}
				
				runs.Add(new StyleRun(offset, length, StyleType.Error));
			}
			
			DoMatchScope(globals, runs);
			
			return globals;
		}
		
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
				// These are considered members not types, so we need to special case
				// them if they are within a namespace.
				foreach (CsMember member in ns.Delegates)
				{
					runs.Add(new StyleRun(member.NameOffset, member.Name.Length, StyleType.Type));
				}
				
				foreach (CsMember member in ns.Enums)
				{
					runs.Add(new StyleRun(member.NameOffset, member.Name.Length, StyleType.Type));
				}
				
				foreach (CsNamespace n in ns.Namespaces)
				{
					DoMatchScope(n, runs);
				}
			}
		}
		#endregion
		
		#region Fields
		private ICsParser m_parser;
		#endregion
	}
}
