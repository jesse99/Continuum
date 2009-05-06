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

#if TEST
using NUnit.Framework;
//using Shared;
using System;
using System.Collections.Generic;

[TestFixture]
public sealed class ListTest
{
	[Test]
	public void Empty()
	{
		int[] data = new int [0];
		
		data.StableSort();
	}
	
	[Test]
	public void OneElement()
	{
		int[] data = new int[]{5};
		
		data.StableSort();
		Assert.AreEqual(5, data[0]);
	}
	
	[Test]
	public void AlreadySorted()
	{
		int[] data = new int[]{5, 10, 12, 44};
		
		data.StableSort();
		Assert.AreEqual(5, data[0]);
		Assert.AreEqual(10, data[1]);
		Assert.AreEqual(12, data[2]);
		Assert.AreEqual(44, data[3]);
	}
	
	[Test]
	public void ReverseOrder()	 
	{
		int[] data = new int[]{44, 12, 10, 5};
		
		data.StableSort();
		Assert.AreEqual(5, data[0]);
		Assert.AreEqual(10, data[1]);
		Assert.AreEqual(12, data[2]);
		Assert.AreEqual(44, data[3]);
	}
	
	[Test]
	public void Unsorted1() 
	{
		int[] data = new int[]{44, 12, 5, 10};
		
		data.StableSort();
		Assert.AreEqual(5, data[0]);
		Assert.AreEqual(10, data[1]);
		Assert.AreEqual(12, data[2]);
		Assert.AreEqual(44, data[3]);
	}

	[Test]
	public void Unsorted2() 
	{
		int[] data = new int[]{12, 44, 10, 5};
		
		data.StableSort();
		Assert.AreEqual(5, data[0]);
		Assert.AreEqual(10, data[1]);
		Assert.AreEqual(12, data[2]);
		Assert.AreEqual(44, data[3]);
	}

	[Test]
	public void Delegate() 
	{
		int[] data = new int[]{12, 44, 10, 5};
		
		data.StableSort((lhs, rhs) => lhs.CompareTo(rhs));
		Assert.AreEqual(5, data[0]);
		Assert.AreEqual(10, data[1]);
		Assert.AreEqual(12, data[2]);
		Assert.AreEqual(44, data[3]);
	}
}
#endif	// TEST
