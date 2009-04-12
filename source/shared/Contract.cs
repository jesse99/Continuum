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

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;

// This is a subset of the upcoming code contracts feature in .NET (see
// http://msdn.microsoft.com/en-us/devlabs/dd491992.aspx for more
// details). Hopefully this will allow our code to be migrated easily to
// the real contracts when mono supports them.
namespace Shared
{
	// This is used to mark a classes invariant method (typically named
	// ObjectInvariant). The invariant method should check the object's
	// state using Contract.Invariant. Note that unlike the real contracts
	// code the invariant will not be called automatically so if it is present
	// it should be called manually at the end of all public methods.
	[Serializable]
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public sealed class ContractInvariantMethodAttribute : Attribute
	{
	}
	
	// Signals that an abstract class or interface has an associated class
	// which defines contracts. Usage is like this:
	// [ContractClass(typeof(IFooContract))]
	// public interface IFoo
	// {
	// 		int Work(object data);
	// }
	[Serializable]
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
	public sealed class ContractClassAttribute : Attribute
	{
		public ContractClassAttribute(Type otherClass)
		{
		}
	}
	
	// Marks a class as containing contracts for an abstract class or interface. 
	// Usage is like this:
	// [ContractClassFor(typeof(IFoo))]
	// public sealed class IFooContract : IFoo
	// {
	// 		int IFoo.Work(object data)
	// 		{
	// 			Contract.Requires(data != null, "data is null");
	// 			return default(int);
	// 		}
	// }
	[Serializable]
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public sealed class ContractClassForAttribute : Attribute
	{
		public ContractClassForAttribute(Type otherClass)
		{
		}
	}
	
	// Indicates that a method or delegate has no visible side effects (and 
	// therefore can be used within a Contract call). Note that getters are
	// assumed to be pure.
	[Serializable]
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate, AllowMultiple = false)]
	public sealed class PureAttribute : Attribute
	{
	}
	
	// Thrown when a contract method fails.
	[Serializable]
	public class ContractException : Exception
	{
		public ContractException()
		{
		}
		
		public ContractException(string text) : base(text)
		{
		}
		
		public ContractException(string text, Exception inner) : base (text, inner)
		{
		}
		
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		protected ContractException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
	
	public static class Contract
	{
		#region Asserts
		[Conditional("CONTRACTS_FULL")]
		[Conditional("DEBUG")]
		public static void Assert(bool predicate)
		{
			if (!predicate)
				throw new ContractException("assert failure");
		}
		
		[Conditional("CONTRACTS_FULL")]
		[Conditional("DEBUG")]
		public static void Assert(bool predicate, string mesg)
		{
			if (!predicate)
				throw new ContractException(mesg);
		}
		
		// These are like asserts except that the static verifier will make use
		// of the predicates.
		[Conditional("CONTRACTS_FULL")]
		[Conditional("DEBUG")]
		public static void Assume(bool predicate)
		{
			if (!predicate)
				throw new ContractException("assume failure");
		}
		
		[Conditional("CONTRACTS_FULL")]
		[Conditional("DEBUG")]
		public static void Assume(bool predicate, string mesg)
		{
			if (!predicate)
				throw new ContractException(mesg);
		}
		#endregion
		
		#region Design by Contract
		public static void RequiresAlways(bool predicate)
		{
			if (!predicate)
				throw new ContractException("requires failure");
		}
		
		public static void RequiresAlways(bool predicate, string mesg)
		{
			if (!predicate)
				throw new ContractException(mesg);
		}
		
		[Conditional("CONTRACTS_FULL")]
		[Conditional("CONTRACTS_PRECONDITIONS")]
		public static void Requires(bool predicate)
		{
			if (!predicate)
				throw new ContractException("requires failure");
		}
		
		[Conditional("CONTRACTS_FULL")]
		[Conditional("CONTRACTS_PRECONDITIONS")]
		public static void Requires(bool predicate, string mesg)
		{
			if (!predicate)
				throw new ContractException(mesg);
		}
		
		// Note that unlike the real contracts code these are placed at the end,
		// not the start of methods.
		[Conditional("CONTRACTS_FULL")]
		public static void Ensures(bool predicate)
		{
			if (!predicate)
				throw new ContractException("ensures failure");
		}
		
		[Conditional("CONTRACTS_FULL")]
		public static void Ensures(bool predicate, string mesg)
		{
			if (!predicate)
				throw new ContractException(mesg);
		}
		
		// This is not called automatically as it is with the real contracts code.
		[Conditional("CONTRACTS_FULL")]
		public static void EnsuresOnThrow<T>(bool predicate)
		{
			if (!predicate)
				throw new ContractException("ensures failure");
		}
		
		[Conditional("CONTRACTS_FULL")]
		public static void EnsuresOnThrow<T>(bool predicate, string mesg)
		{
			if (!predicate)
				throw new ContractException(mesg);
		}
		
		[Conditional("CONTRACTS_FULL")]
		public static void Invariant(bool predicate)
		{
			if (!predicate)
				throw new ContractException("invariant failure");
		}
		
		[Conditional("CONTRACTS_FULL")]
		public static void Invariant(bool predicate, string mesg)
		{
			if (!predicate)
				throw new ContractException(mesg);
		}
		#endregion
		
		#region Quantifiers
		public static bool ForAll<T>(IEnumerable<T> values, Func<T, bool> predicate)
		{
			Requires(values != null, "values is null");
			Requires(predicate != null, "predicate is null");
			
			foreach (T value in values)
			{
				if (!predicate(value))
					return false;
			}
			
			return true;
		}
		
		// Predicate will normally use a local array or list with the index it is given.
		public static bool ForAll(int lowerBound, int upperBound, Func<int, bool> predicate)
		{
			Requires(lowerBound >= 0, "lowerBound is negative");
			Requires(lowerBound <= upperBound, "lowerBound is larger than upperBound");
			Requires(predicate != null, "predicate is null");
			
			for (int index = lowerBound; index < upperBound; ++index)
			{
				if (!predicate(index))
					return false;
			}
			
			return true;
		}
		
		public static bool Exists<T>(IEnumerable<T> values, Func<T, bool> predicate)
		{
			Requires(values != null, "values is null");
			Requires(predicate != null, "predicate is null");
			
			foreach (T value in values)
			{
				if (predicate(value))
					return true;
			}
			
			return false;
		}
		
		// Predicate will normally use a local array or list with the index it is given.
		public static bool Exists(int lowerBound, int upperBound, Func<int, bool> predicate)
		{
			Requires(lowerBound >= 0, "lowerBound is negative");
			Requires(lowerBound <= upperBound, "lowerBound is larger than upperBound");
			Requires(predicate != null, "predicate is null");
			
			for (int index = lowerBound; index < upperBound; ++index)
			{
				if (predicate(index))
					return true;
			}
			
			return false;
		}
		#endregion
	}
}
