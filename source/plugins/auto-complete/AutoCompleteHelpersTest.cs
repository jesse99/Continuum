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

#if TEST
using NUnit.Framework;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AutoComplete
{
	[TestFixture]
	public sealed class AutoCompleteHelpersTest
	{	
		[TestFixtureSetUp]
		public void Init()
		{
			Log.SetLevel(TraceLevel.Verbose);
		}
		
		private int DoGetIndex(string text)
		{
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", "{0} {1} {2}", new string('*', 10), new StackTrace().GetFrame(1).GetMethod().Name, new string('*', 10));
			Log.WriteLine(TraceLevel.Verbose, "AutoComplete", text);
			
			int anchorLoc = 0;
			int anchorLen = text.IndexOfAny(new char[]{'(', '<'}) + 1;
			int insertionPoint = text.IndexOf('|');
			int index = AutoCompleteHelpers.GetArgIndex(text, anchorLoc, anchorLen, insertionPoint);
			Log.WriteLine("AutoComplete", "index: {0}", index);
			
			return index;
		}
		
		[Test]
		public void Nullary()
		{
			Assert.AreEqual(1, DoGetIndex("Get(|)"));
			Assert.AreEqual(1, DoGetIndex("Get(|"));
			Assert.AreEqual(1, DoGetIndex("Get( 		|"));
			Assert.AreEqual(1, DoGetIndex("Get( 		x + y|"));
		}
		
		[Test]
		public void Nary()
		{
			Assert.AreEqual(2, DoGetIndex("Get(x, |)"));
			Assert.AreEqual(2, DoGetIndex("Get(x + z,|"));
			Assert.AreEqual(2, DoGetIndex("Get(x + Math.Min(z, 4), |"));
			Assert.AreEqual(3, DoGetIndex("Get(x, y, |"));
			Assert.AreEqual(1, DoGetIndex("Get(xx|x, y, "));
		}
		
		[Test]
		public void Bad()
		{
			Assert.AreEqual(0, DoGetIndex("|Get(z, y)"));
			Assert.AreEqual(0, DoGetIndex("|  Get(z, y)"));
			Assert.AreEqual(0, DoGetIndex("(x, |  Get(z, y)"));
			Assert.AreEqual(0, DoGetIndex("(x, |)  Get(z, y)"));
			
			Assert.AreEqual(0, DoGetIndex("Get(z, y)|"));
			Assert.AreEqual(0, DoGetIndex("Get(z, y) |"));
			Assert.AreEqual(0, DoGetIndex("Get(z, y) , |"));
			Assert.AreEqual(0, DoGetIndex("Get(z, y) (|"));
			Assert.AreEqual(0, DoGetIndex("Get(z, y) (|)"));
			Assert.AreEqual(0, DoGetIndex("Get(z, y) (x, |)"));
			
			Assert.AreEqual(0, DoGetIndex("Get(x + Math.Min(z, 4)), |"));	// extra )
			Assert.AreEqual(0, DoGetIndex("Get(x[a, x, |)"));						// missing ]
		}
		
		[Test]
		public void Subscripts()
		{
			Assert.AreEqual(2, DoGetIndex("Get(x[a, x], |)"));
			Assert.AreEqual(3, DoGetIndex("Get(x[a, x], y[a, y, z, w], |)"));
		}
		
		[Test]
		public void Generics()
		{
			Assert.AreEqual(2, DoGetIndex("Get(new Dictionary<int, string>(), |"));
			Assert.AreEqual(2, DoGetIndex("Get(x < y, |"));
		}
		
		[Test]
		public void AnonMethod()
		{
			Assert.AreEqual(2, DoGetIndex("Exists((x, y) => {foo(x, y, z, r);}, |"));
			Assert.AreEqual(2, DoGetIndex("Exists((x, y) => {x, y, z, r}, |"));
		}
		
		[Test]
		public void GenericMethod1()
		{
			Assert.AreEqual(-1, DoGetIndex("Get<|"));
			Assert.AreEqual(-1, DoGetIndex("Get<int|"));
			Assert.AreEqual(-1, DoGetIndex("Get<int|>"));
			Assert.AreEqual(-1, DoGetIndex("Get<int|>()"));
			Assert.AreEqual(0, DoGetIndex("Get<int>|()"));
			Assert.AreEqual(1, DoGetIndex("Get<int>(|)"));
			Assert.AreEqual(2, DoGetIndex("Get<int>(x, |"));
		}
		
		[Test]
		public void GenericMethod2()
		{
			Assert.AreEqual(-1, DoGetIndex("Get<|"));
			Assert.AreEqual(-1, DoGetIndex("Get<|int, string>()"));
			Assert.AreEqual(-1, DoGetIndex("Get<int|, string>()"));
			Assert.AreEqual(-2, DoGetIndex("Get<int, |string>()"));
			Assert.AreEqual(-2, DoGetIndex("Get<int, |"));
			Assert.AreEqual(-2, DoGetIndex("Get<int, string|>()"));
			Assert.AreEqual(0, DoGetIndex("Get<int, string>|()"));
			Assert.AreEqual(1, DoGetIndex("Get<int, string>(|)"));
			Assert.AreEqual(2, DoGetIndex("Get<int, string>(x, |"));
		}
	}
}
#endif	// TEST
