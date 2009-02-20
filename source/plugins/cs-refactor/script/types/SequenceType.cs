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
	internal sealed class SequenceType : RefactorType
	{
		private SequenceType()
		{
			Trace.Assert(ms_instance == null, "Types should only be instantiated once");
			
			ms_instance = this;
		}
		
		public static SequenceType Instance 
		{
			get 
			{
				if (ms_instance == null)
					ms_instance = new SequenceType();
					
				return ms_instance;
			}
		}
		
		public override RefactorType Base
		{
			get {return ObjectType.Instance;}
		}
		
		public override string Name
		{
			get {return "Sequence";}
		}

		public override Type ManagedType
		{
			get {return typeof(object[]);}
		}
		
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<object[], object>("Contains", this.DoContains);
			type.Register<object[]>("get_Fifth", o => DoGetNth(o, 4));
			type.Register<object[]>("get_First", o => DoGetNth(o, 0));
			type.Register<object[]>("get_Fourth", o => DoGetNth(o, 3));
			type.Register<object[]>("get_Head", o => DoGetNth(o, 0));
			type.Register<object[]>("get_IsEmpty", this.DoGetIsEmpty);
			type.Register<object[]>("get_Last", this.DoGetLast);
			type.Register<object[]>("get_Second", o => DoGetNth(o, 1));
			type.Register<object[]>("get_Tail", this.DoGetTail);
			type.Register<object[]>("get_Third", o => DoGetNth(o, 2));
		}
		
		#region Private Methods
		private object DoContains(object[] sequence, object rhs)
		{
			foreach (object candidate in sequence)
			{
				if (Equals(candidate, rhs))
					return true;
			}
			
			return false;
		}

		private object DoGetLast(object[] sequence)
		{
			if (sequence.Length == 0)
				throw new InvalidOperationException("Sequence is empty.");
				
			return sequence[sequence.Length - 1];
		}

		private object DoGetIsEmpty(object[] sequence)
		{
			return sequence.Length == 0;
		}

		private object DoGetNth(object[] sequence, int index)
		{
			if (index >= sequence.Length)
				throw new InvalidOperationException(string.Format("Attempt to get element {0} from the Sequence, but there are only {1} elements.", index +1, sequence.Length));
				
			return sequence[index];
		}

		private object DoGetTail(object[] sequence)
		{
			if (sequence.Length == 0)
				return sequence;
				
			else if (sequence.Length == 1)
				return new object[0];

			object[] result = new object[sequence.Length - 1];
			Array.Copy(sequence, 1, result, 0, sequence.Length - 1);				

			return result;
		}
		#endregion
		
		#region Fields
		private static SequenceType ms_instance;
		#endregion
	} 
}
