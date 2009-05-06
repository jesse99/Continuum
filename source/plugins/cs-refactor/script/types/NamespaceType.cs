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

using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CsRefactor.Script
{
	internal sealed class NamespaceType : RefactorType
	{
		private NamespaceType()
		{
		}
		
		public static NamespaceType Instance
		{
			get
			{
				if (ms_instance == null)
					ms_instance = new NamespaceType();
					
				return ms_instance;
			}
		}
		
		public override RefactorType Base
		{
			get {return TypeScopeType.Instance;}
		}
		
		public override string Name
		{
			get {return "Namespace";}
		}
		
		public override Type ManagedType
		{
			get {return typeof(CsNamespace);}
		}
		
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<CsNamespace, string>("AddUsing", this.DoAddUsing);
			type.Register<CsNamespace>("get_Aliases", this.DoGetAliases);
			type.Register<CsNamespace>("get_Externs", this.DoGetExterns);
			type.Register<CsNamespace>("get_Name", this.DoGetName);
			type.Register<CsNamespace>("get_Namespaces", this.DoGetNamespaces);
			type.Register<CsNamespace>("get_Uses", this.DoGetUses);
			type.Register<CsNamespace, string, string>("TypeMatches", this.DoTypeMatches);
		}
		
		#region Private Methods
		private object DoAddUsing(CsNamespace ns, string name)
		{
			return new AddUsing(ns, name);
		}
		
		private object DoGetAliases(CsNamespace ns)
		{
			return ns.Aliases;
		}
		
		private object DoGetExterns(CsNamespace ns)
		{
			return ns.Externs;
		}
		
		private object DoGetName(CsNamespace ns)
		{
			return ns.Name;
		}
		
		private object DoGetNamespaces(CsNamespace ns)
		{
			return ns.Namespaces;
		}

		private object DoGetUses(CsNamespace ns)
		{
			return ns.Uses;
		}

		private object DoTypeMatches(CsNamespace ns, string type, string name)
		{
			return DoMatchType(ns, type, name);
		}
		
		private bool DoMatchType(CsNamespace ns, string type, string name)
		{
			bool matches = false;
			
			if (type == name)
			{
				matches = true;
			}

			if (!matches)
			{
				foreach (CsUsingDirective used in ns.Uses)
				{
					if (used.Namespace + "." + type == name)
					{
						matches = true;
						break;
					}
				}
			}
			
			if (!matches)
			{
				foreach (CsUsingAlias alias in ns.Aliases)
				{
					string t = type.Replace(alias.Alias + ".", alias.Value + ".");
					if (t == name)
					{
						matches = true;
						break;
					}
				}
			}
			
			if (!matches)
			{
				foreach (CsUsingAlias alias in ns.Aliases)
				{
					if (alias.Alias == type && DoMatchType(ns, alias.Value, name))
					{
						matches = true;
						break;
					}
				}
			}
			
			if (!matches && ns.Namespace != null)
				matches = DoMatchType(ns.Namespace, type, name);
			
			return matches;
		}
		#endregion

		private static NamespaceType ms_instance;
	} 
}
