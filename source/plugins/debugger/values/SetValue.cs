// Machine generated by multimethod-sharp 0.3 using
// /usr/local/bin/multimethod --result=void --name=SetValue --arg=ThreadMirror:thread --arg=VariableItem:item --arg=object:parent --arg=object:key --arg=object:newValue --namespace=Debugger --using=Mono.Debugger.Soft --comment="Updates the value for an item."
using Mono.Debugger.Soft;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Debugger
{
	// Updates the value for an item.
	internal static class SetValue
	{
		// Used to identify overloads. The overload method name may be anything
		// but the signature should match Invoke except that both the return and
		// argument types may be derived versions of the types that appear in Invoke.
		[Serializable]
		[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
		internal sealed class OverloadAttribute : Attribute
		{
		}
		
		// Calls the best overload for the actual argument types or throws System.
		// MissingMethodException or System.Reflection.AmbiguousMatchException.
		// Arguments may be null.
		public static void Invoke(ThreadMirror thread, VariableItem item, object parent, object key, object newValue)
		{
			if (ms_candidates == null)
				DoAutoRegister();
			
			var __actual = new Entry(thread, item, parent, key, newValue);
			int __index = DoGetNextMethod(__actual, 0);
			if (__index == -1)
				throw new MissingMethodException(string.Format("Invoke failed to find an overload for SetValue({0}).", __actual));
			
			ms_nextIndex = DoGetNextMethod(__actual, __index + 1);
			if (ms_nextIndex > 0)
				if (DoLeftPrecedesRight(ms_candidates[__index], ms_candidates[ms_nextIndex]) == 0)
					throw new AmbiguousMatchException(string.Format("Invoke found ambiguous overloads for SetValue({0}).", __actual));
			
			ms_candidates[__index].Method(thread, item, parent, key, newValue);
		}
		
		// Calls the next best overload. For example, if overload Foo(Derived) was last
		// called then this would call Foo(Base). The arguments should normally be the
		// same as the ones passed into the original Invoke call. May throw System.
		// MissingMethodException or System.Reflection.AmbiguousMatchException.
		public static void NextMethod(ThreadMirror thread, VariableItem item, object parent, object key, object newValue)
		{
			var __actual = new Entry(thread, item, parent, key, newValue);
			
			int __index = ms_nextIndex;
			if (__index == -1)
				throw new MissingMethodException(string.Format("NextMethod failed to find an overload for SetValue({0}).", __actual));
			
			ms_nextIndex = DoGetNextMethod(__actual, __index + 1);
			if (ms_nextIndex > 0)
				if (DoLeftPrecedesRight(ms_candidates[__index], ms_candidates[ms_nextIndex]) == 0)
					throw new AmbiguousMatchException(string.Format("NextMethod found ambiguous overloads for SetValue({0}).", __actual));
			
			ms_candidates[__index].Method(thread, item, parent, key, newValue);
		}
		
		// Manually registers overload methods in the specified type. Note that if
		// this is not called then all types and methods in the loaded assemblies
		// are registered the first time Invoke is called.
		public static void Register(Type type)
		{
			if (ms_candidates == null)
				ms_candidates = new List<Entry>();
			
			int oldCount = ms_candidates.Count;
			foreach (MethodInfo info in type.GetMethods(MethodBinding))
			{
				if (info.IsDefined(typeof(OverloadAttribute), false))
				{
					DoCheckMethod(info);
					
					MethodInfo temp = info;			// need this so foreach doesn't hose the lambdas
					ParameterInfo[] parms = info.GetParameters();
					Entry entry;
					if (info.IsStatic)
					{
						entry = new Entry(
							(ThreadMirror a1, VariableItem a2, object a3, object a4, object a5) => temp.Invoke(null, new object[]{a1, a2, a3, a4, a5}),
							parms[0].ParameterType, parms[1].ParameterType, parms[2].ParameterType, parms[3].ParameterType, parms[4].ParameterType);
					}
					else
					{
						entry = new Entry(
							(ThreadMirror a1, VariableItem a2, object a3, object a4, object a5) => temp.Invoke(a1, new object[]{a2, a3, a4, a5}),
							temp.DeclaringType, parms[0].ParameterType, parms[1].ParameterType, parms[2].ParameterType, parms[3].ParameterType);
					}
					
					if (ms_candidates.IndexOf(entry) >= 0)
						throw new InvalidOperationException(string.Format("There is already an overload registered with the same signature as {0}.{1}.", info.DeclaringType.Name, info.Name));
					
					ms_candidates.Add(entry);
				}
			}
			
			if (oldCount < ms_candidates.Count)
				DoSortCandidates();
		}
		
		#region Private Types
		private delegate void OverloadType(ThreadMirror thread, VariableItem item, object parent, object key, object newValue);
		
		private struct Entry : IEquatable<Entry>
		{
			public Entry(ThreadMirror thread, VariableItem item, object parent, object key, object newValue) : this()
			{
				Method = null;
				Type1 = (object) thread != null ? thread.GetType() : null;
				Type2 = (object) item != null ? item.GetType() : null;
				Type3 = (object) parent != null ? parent.GetType() : null;
				Type4 = (object) key != null ? key.GetType() : null;
				Type5 = (object) newValue != null ? newValue.GetType() : null;
			}
			
			public Entry(OverloadType overload, Type type1, Type type2, Type type3, Type type4, Type type5) : this()
			{
				Method = overload;
				Type1 = type1;
				Type2 = type2;
				Type3 = type3;
				Type4 = type4;
				Type5 = type5;
			}
			
			public OverloadType Method {get; private set;}
			
			public Type Type1 {get; private set;}
			
			public Type Type2 {get; private set;}
			
			public Type Type3 {get; private set;}
			
			public Type Type4 {get; private set;}
			
			public Type Type5 {get; private set;}
			
			public override string ToString()
			{
				var builder = new System.Text.StringBuilder();
				
				builder.Append(Type1 != null ? Type1.ToString() : "null");
				builder.Append(", ");
				builder.Append(Type2 != null ? Type2.ToString() : "null");
				builder.Append(", ");
				builder.Append(Type3 != null ? Type3.ToString() : "null");
				builder.Append(", ");
				builder.Append(Type4 != null ? Type4.ToString() : "null");
				builder.Append(", ");
				builder.Append(Type5 != null ? Type5.ToString() : "null");
				
				return builder.ToString();
			}
			
			public override bool Equals(object obj)
			{
				if (obj == null)
					return false;
				
				if (GetType() != obj.GetType())
					return false;
				
				Entry rhs = (Entry) obj;
				return this == rhs;
			}
			
			public bool Equals(Entry rhs)
			{
				return this == rhs;
			}
			
			public static bool operator==(Entry lhs, Entry rhs)
			{
				if (lhs.Type1 != rhs.Type1)
					return false;
				
				if (lhs.Type2 != rhs.Type2)
					return false;
				
				if (lhs.Type3 != rhs.Type3)
					return false;
				
				if (lhs.Type4 != rhs.Type4)
					return false;
				
				if (lhs.Type5 != rhs.Type5)
					return false;
				
				return true;
			}
			
			public static bool operator!=(Entry lhs, Entry rhs)
			{
				return !(lhs == rhs);
			}
			
			public override int GetHashCode()
			{
				int hash = 0;
				
				unchecked
				{
					hash += Type1.GetHashCode();
					hash += Type2.GetHashCode();
					hash += Type3.GetHashCode();
					hash += Type4.GetHashCode();
					hash += Type5.GetHashCode();
				}
				
				return hash;
			}
		}
		#endregion
		
		#region Private Members
		private const BindingFlags MethodBinding = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		
		private static int DoGetNextMethod(Entry actual, int startIndex)
		{
			for (int index = startIndex; index < ms_candidates.Count; ++index)
			{
				if (DoCanBeCalledWith(ms_candidates[index], actual))
					return index;
			}
			
			return -1;
		}
		
		// This and Entry.ToString are the only places where we need to worry about null types.
		private static bool DoCanBeCalledWith(Entry formal, Entry actual)
		{
			if (actual.Type1 != null && !formal.Type1.IsAssignableFrom(actual.Type1))
				return false;
			
			if (actual.Type2 != null && !formal.Type2.IsAssignableFrom(actual.Type2))
				return false;
			
			if (actual.Type3 != null && !formal.Type3.IsAssignableFrom(actual.Type3))
				return false;
			
			if (actual.Type4 != null && !formal.Type4.IsAssignableFrom(actual.Type4))
				return false;
			
			if (actual.Type5 != null && !formal.Type5.IsAssignableFrom(actual.Type5))
				return false;
			
			return true;
		}
		
		// Sort order doesn't depend on actual arguments so we can do it once.
		private static void DoSortCandidates()
		{
			// Note that we cannot use List.Sort because DoLeftPrecedesRight
			// does not obey the transitivity requirement of a comparison sort.
			// So, we'll instead use a crappy O(N^2) algorithm which should
			// not be too bad because there will hardly ever be a lot of overloads.
			for (int i = 0; i < ms_candidates.Count; ++i)
			{
				for (int j = i + 1; j < ms_candidates.Count; ++j)
				{
					if (DoLeftPrecedesRight(ms_candidates[i], ms_candidates[j]) > 0)
					{
						Entry temp = ms_candidates[i];
						ms_candidates[i] = ms_candidates[j];
						ms_candidates[j] = temp;
					}
				}
			}
		}
		
		// An overload A is better than overload B if A has an argument which is
		// more specialized than the corresponding argument in B and B has no
		// arguments which are more specialized. An overload A is ambiguous
		// with an overload B if A has a better argument than B and B has a
		// better argument than A.
		private static int DoLeftPrecedesRight(Entry left, Entry right)
		{
			bool leftPrecedes1 = left.Type1.IsSubclassOf(right.Type1);
			bool leftPrecedes2 = left.Type2.IsSubclassOf(right.Type2);
			bool leftPrecedes3 = left.Type3.IsSubclassOf(right.Type3);
			bool leftPrecedes4 = left.Type4.IsSubclassOf(right.Type4);
			bool leftPrecedes5 = left.Type5.IsSubclassOf(right.Type5);
			
			bool rightPrecedes1 = right.Type1.IsSubclassOf(left.Type1);
			bool rightPrecedes2 = right.Type2.IsSubclassOf(left.Type2);
			bool rightPrecedes3 = right.Type3.IsSubclassOf(left.Type3);
			bool rightPrecedes4 = right.Type4.IsSubclassOf(left.Type4);
			bool rightPrecedes5 = right.Type5.IsSubclassOf(left.Type5);
			
			if ((leftPrecedes1 || leftPrecedes2 || leftPrecedes3 || leftPrecedes4 || leftPrecedes5) && !rightPrecedes1 && !rightPrecedes2 && !rightPrecedes3 && !rightPrecedes4 && !rightPrecedes5)
				return -1;
			else if ((rightPrecedes1 || rightPrecedes2 || rightPrecedes3 || rightPrecedes4 || rightPrecedes5) && !leftPrecedes1 && !leftPrecedes2 && !leftPrecedes3 && !leftPrecedes4 && !leftPrecedes5)
				return +1;
			else
				return 0;
		}
		
		private static void DoAutoRegister()
		{
			ms_candidates = new List<Entry>();		// we want to set this even if no overloads are found
			
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				foreach (Type type in assembly.GetTypes())
				{
					foreach (MethodInfo method in type.GetMethods(MethodBinding))
					{
						if (method.IsDefined(typeof(OverloadAttribute), false))
						{
							Register(type);
							break;
						}
					}
				}
			}
		}
		
		public static void DoCheckMethod(MethodInfo info)
		{
			// Covariant return types are not very useful in the context of multimethods,
			// but may be useful if the overload is called directly.
			if (!typeof(void).IsAssignableFrom(info.ReturnType))
				throw new InvalidOperationException(string.Format("{0}.{1} has return type {2} but should have void (or a derived class).", info.DeclaringType.Name, info.Name, info.ReturnType));
			
			ParameterInfo[] parms = info.GetParameters();
			if (info.IsStatic)
			{
				if (parms.Length != 5)
					throw new InvalidOperationException(string.Format("{0}.{1} has arity {2} but should have arity 5.", info.DeclaringType.Name, info.Name, parms.Length));
				
				if (!typeof(ThreadMirror).IsAssignableFrom(parms[0].ParameterType))
					throw new InvalidOperationException(string.Format("{0}.{1} argument {2} is not compatible with type ThreadMirror.", info.DeclaringType.Name, info.Name, parms[0].Name));
				if (!typeof(VariableItem).IsAssignableFrom(parms[1].ParameterType))
					throw new InvalidOperationException(string.Format("{0}.{1} argument {2} is not compatible with type VariableItem.", info.DeclaringType.Name, info.Name, parms[1].Name));
				if (!typeof(object).IsAssignableFrom(parms[2].ParameterType))
					throw new InvalidOperationException(string.Format("{0}.{1} argument {2} is not compatible with type object.", info.DeclaringType.Name, info.Name, parms[2].Name));
				if (!typeof(object).IsAssignableFrom(parms[3].ParameterType))
					throw new InvalidOperationException(string.Format("{0}.{1} argument {2} is not compatible with type object.", info.DeclaringType.Name, info.Name, parms[3].Name));
				if (!typeof(object).IsAssignableFrom(parms[4].ParameterType))
					throw new InvalidOperationException(string.Format("{0}.{1} argument {2} is not compatible with type object.", info.DeclaringType.Name, info.Name, parms[4].Name));
			}
			else
			{
				if (parms.Length != 4)
					throw new InvalidOperationException(string.Format("{0}.{1} has arity {2} but should have arity 4.", info.DeclaringType.Name, info.Name, parms.Length));
				
				if (!typeof(ThreadMirror).IsAssignableFrom(info.DeclaringType))
					throw new InvalidOperationException(string.Format("{0}.{1} this argument is not compatible with type ThreadMirror.", info.DeclaringType.Name, info.Name));
				
				if (!typeof(VariableItem).IsAssignableFrom(parms[0].ParameterType))
					throw new InvalidOperationException(string.Format("{0}.{1} argument {2} is not compatible with type VariableItem.", info.DeclaringType.Name, info.Name, parms[0].Name));
				if (!typeof(object).IsAssignableFrom(parms[1].ParameterType))
					throw new InvalidOperationException(string.Format("{0}.{1} argument {2} is not compatible with type object.", info.DeclaringType.Name, info.Name, parms[1].Name));
				if (!typeof(object).IsAssignableFrom(parms[2].ParameterType))
					throw new InvalidOperationException(string.Format("{0}.{1} argument {2} is not compatible with type object.", info.DeclaringType.Name, info.Name, parms[2].Name));
				if (!typeof(object).IsAssignableFrom(parms[3].ParameterType))
					throw new InvalidOperationException(string.Format("{0}.{1} argument {2} is not compatible with type object.", info.DeclaringType.Name, info.Name, parms[3].Name));
			}
		}
		#endregion
		
		#region Fields
		private static List<Entry> ms_candidates;	// best methods are first
		private static int ms_nextIndex;
		#endregion
	}
}
