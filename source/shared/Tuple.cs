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

using Gear.Helpers;
using System;

namespace Shared
{
	[ThreadModel(ThreadModel.Concurrent)]
	public static class Tuple
	{
		public static Tuple2<T0, T1> Make<T0, T1>(T0 value0, T1 value1)
		{
			return new Tuple2<T0, T1>(value0, value1);
		}
		
		public static Tuple3<T0, T1, T2> Make<T0, T1, T2>(T0 value0, T1 value1, T2 value2)
		{
			return new Tuple3<T0, T1, T2>(value0, value1, value2);
		}
	}
	
	// Represents a pair of values.
	[ThreadModel(ThreadModel.Concurrent)]
	public struct Tuple2<T0, T1> : IEquatable<Tuple2<T0, T1>>	// TODO: probably should support IList, IList<object>
	{
		public Tuple2(T0 value0, T1 value1)
		{
			First = value0;
			Second = value1;
		}
		
		public T0 First  {get; private set;}
		public T1 Second {get; private set;}
		
		public Tuple2<T0, T1> SetFirst(T0 value)
		{
			return Tuple.Make(value, Second);
		}
		
		public Tuple2<T0, T1> SetSecond(T1 value)
		{
			return Tuple.Make(First, value);
		}
		
		public override string ToString()
		{
			return string.Format("[{0}, {1}]", First, Second);
		}
		
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			if (GetType() != obj.GetType())
				return false;
			
			Tuple2<T0, T1> rhs = (Tuple2<T0, T1>) obj;
			return this == rhs;
		}
		
		public bool Equals(Tuple2<T0, T1> rhs)
		{
			return this == rhs;
		}
		
		public static bool operator==(Tuple2<T0, T1> lhs, Tuple2<T0, T1> rhs)
		{
			return DoCompare(lhs.First, rhs.First) && DoCompare(lhs.Second, rhs.Second);
		}
		
		public static bool operator!=(Tuple2<T0, T1> lhs, Tuple2<T0, T1> rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			int hash = 33;
			
			unchecked
			{
				hash += 3*First.GetHashCode() + 7*Second.GetHashCode();
			}
			
			return hash;
		}
		
		private static bool DoCompare(object lhs, object rhs)
		{
			if (lhs == null)
				return rhs == null;
			else
				return lhs.Equals(rhs);
		}
	}
	
	// Represents a triplet of values.
	[ThreadModel(ThreadModel.Concurrent)]
	public struct Tuple3<T0, T1, T2> : IEquatable<Tuple3<T0, T1, T2>>
	{
		public Tuple3(T0 value0, T1 value1, T2 value2)
		{
			First = value0;
			Second = value1;
			Third = value2;
		}
		
		public T0 First  {get; private set;}
		public T1 Second {get; private set;}
		public T2 Third  {get; private set;}
		
		public Tuple3<T0, T1, T2> SetFirst(T0 value)
		{
			return Tuple.Make(value, Second, Third);
		}
		
		public Tuple3<T0, T1, T2> SetSecond(T1 value)
		{
			return Tuple.Make(First, value, Third);
		}
		
		public Tuple3<T0, T1, T2> SetThird(T2 value)
		{
			return Tuple.Make(First, Second, value);
		}
		
		public override string ToString()
		{
			return string.Format("[{0}, {1}, {2}]", First, Second, Third);
		}
		
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			
			if (GetType() != obj.GetType())
				return false;
			
			Tuple3<T0, T1, T2> rhs = (Tuple3<T0, T1, T2>) obj;
			return this == rhs;
		}
		
		public bool Equals(Tuple3<T0, T1, T2> rhs)
		{
			return this == rhs;
		}
		
		public static bool operator==(Tuple3<T0, T1, T2> lhs, Tuple3<T0, T1, T2> rhs)
		{
			return DoCompare(lhs.First, rhs.First) && 
					DoCompare(lhs.Second, rhs.Second) &&
					DoCompare(lhs.Third, rhs.Third);
		}
		
		public static bool operator!=(Tuple3<T0, T1, T2> lhs, Tuple3<T0, T1, T2> rhs)
		{
			return !(lhs == rhs);
		}
		
		public override int GetHashCode()
		{
			int hash = 33;
			
			unchecked
			{
				hash += 3*First.GetHashCode() + 7*Second.GetHashCode() + 11*Third.GetHashCode();
			}
			
			return hash;
		}
		
		private static bool DoCompare(object lhs, object rhs)
		{
			if (lhs == null)
				return rhs == null;
			else
				return lhs.Equals(rhs);
		}
	}
}
