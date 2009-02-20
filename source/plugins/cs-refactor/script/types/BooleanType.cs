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
	internal sealed class BooleanType : RefactorType
	{
		private BooleanType()
		{
		}
		
		public static BooleanType Instance 
		{
			get 
			{
				if (ms_instance == null)
					ms_instance = new BooleanType();
					
				return ms_instance;
			}
		}
		
		public override RefactorType Base
		{
			get {return ObjectType.Instance;}
		}
		
		public override string Name
		{
			get {return "Boolean";}
		}

		public override Type ManagedType
		{
			get {return typeof(bool);}
		}
		
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<bool, bool>("op_LogicalAnd", this.DoAnd);
			type.Register<bool>("op_LogicalComplement", this.DoComplement);
			type.Register<bool, bool>("op_LogicalOr", this.DoOr);
		}
		
		#region Private Methods
		private object DoAnd(bool lhs, bool rhs)
		{
			return lhs && rhs;
		}

		private object DoComplement(bool value)
		{
			return !value;
		}

		private object DoOr(bool lhs, bool rhs)
		{
			return lhs || rhs;
		}
		#endregion
		private static BooleanType ms_instance;
	} 
}
