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

//using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CsRefactor.Script
{
	internal sealed class StringType : RefactorType
	{
		private StringType()
		{
		}
		
		public static StringType Instance 
		{
			get 
			{
				if (ms_instance == null)
					ms_instance = new StringType();
					
				return ms_instance;
			}
		}
		
		public override RefactorType Base
		{
			get {return ObjectType.Instance;}
		}
		
		public override string Name
		{
			get {return "String";}
		}

		public override Type ManagedType
		{
			get {return typeof(string);}
		}
		
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<string, object>("op_Add", this.DoAdd);
			type.Register<string, string>("Contains", this.DoContains);
			type.Register<string, string>("EndsWith", this.DoEndsWith);
			type.Register<string>("get_IsEmpty", this.DoIsEmpty);
			type.Register<string, object[]>("Join", this.DoJoin);
			type.Register<string, string, string>("Replace", this.DoReplace);
			type.Register<string, string>("StartsWith", this.DoStartsWith);
		}
		
		#region Private Methods		
		private object DoAdd(string str, object rhs)
		{
			string s1 = str ?? "null";
			string s2 = rhs.Stringify();		// works with null
			
			return s1 + s2;
		}

		private object DoContains(string str, string rhs)
		{
			return str.Contains(rhs);
		}

		private object DoEndsWith(string str, string rhs)
		{
			return str.EndsWith(rhs);
		}

		private object DoIsEmpty(string str)
		{
			return str.Length == 0;
		}

		private object DoJoin(string str, object[] data)
		{
			var list = new List<string>(data.Length);
			foreach (object d in data)
			{
				list.Add(d.Stringify());
			}
			
			return string.Join(str, list.ToArray());
		}

		private object DoReplace(string str, string os, string ns)
		{
			return str.Replace(os, ns);
		}

		private object DoStartsWith(string str, string rhs)
		{
			return str.StartsWith(rhs);
		}
		#endregion

		private static StringType ms_instance;
	} 
}
